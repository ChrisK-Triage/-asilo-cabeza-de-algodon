using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Asilo.Api.Data;
using Asilo.Api.Repositories;
using Asilo.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ------------------- SERVICIOS -------------------

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Los enum viajan como texto ("Pendiente") y no como numero.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API Asilo Cabeza de Algodon",
        Version = "v1",
        Description = "Sistema de administracion del asilo. Proyecto final de Analisis y Diseno de Sistemas II."
    });

    // Sin esto, Swagger no tiene donde pegar el token y todas las
    // pruebas de los endpoints protegidos responden 401.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pegar unicamente el token que devuelve POST /api/auth/login. Swagger agrega el 'Bearer '."
    });

    // Esto es lo que hace que Swagger adjunte la cabecera a cada
    // peticion. El diccionario es
    // Dictionary<OpenApiSecuritySchemeReference, List<string>>, asi que
    // el segundo valor tiene que ser una lista y no un arreglo: la
    // lista vacia significa "este esquema no exige alcances".
    // Recibe una funcion y no el requisito armado: Swashbuckle 10 lo
    // construye cuando ya tiene el documento, porque en Microsoft.OpenApi 2
    // una referencia se resuelve contra el documento que la contiene.
    // El parametro no me hace falta, asi que lo descarto con _.
    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
    });
});

// ------------------- AUTENTICACION -------------------

string llaveJwt = builder.Configuration["Jwt:Llave"]
    ?? throw new InvalidOperationException("Falta 'Jwt:Llave' en appsettings.json.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Emisor"],
            ValidAudience = builder.Configuration["Jwt:Audiencia"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(llaveJwt)),

            // Por omision la libreria tolera cinco minutos de desfase
            // entre relojes. En un sistema que corre contra un solo
            // servidor ese margen solo sirve para que un token vencido
            // siga funcionando un rato mas.
            ClockSkew = TimeSpan.Zero,

            // Le digo explicitamente de que afirmacion sale el rol. Sin
            // esto, [Authorize(Roles = ...)] depende del mapeo que la
            // libreria aplique por omision y falla de forma silenciosa:
            // responde 403 sin decir por que.
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

// Singleton: DapperContext solo guarda la cadena de conexion, no tiene estado.
builder.Services.AddSingleton<DapperContext>();

// El servicio de token tampoco guarda estado: solo lee configuracion.
builder.Services.AddSingleton<IServicioToken, ServicioToken>();

// Scoped: un repositorio por peticion HTTP.
builder.Services.AddScoped<IResidenteRepository, ResidenteRepository>();
builder.Services.AddScoped<IRemisionRepository, RemisionRepository>();
builder.Services.AddScoped<ICitaRepository, CitaRepository>();
builder.Services.AddScoped<IAnexoRepository, AnexoRepository>();
builder.Services.AddScoped<IFamiliarRepository, FamiliarRepository>();
builder.Services.AddScoped<ICatalogoRepository, CatalogoRepository>();
builder.Services.AddScoped<IFacturaRepository, FacturaRepository>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<ICajaRepository, CajaRepository>();
builder.Services.AddScoped<IReporteRepository, ReporteRepository>();

// El canal es lo unico que cambia al pasar a produccion, y cual se usa
// lo decide la configuracion en lugar de una linea que haya que editar
// y recompilar: si estan los datos del servidor de correo, se manda
// correo de verdad; si no, se escribe en el registro.
//
// Lo prefiero asi porque el mismo binario sirve para las dos cosas. En
// mi maquina no quiero mandar correo cada vez que pruebo una cita, y en
// Azure si; con esto, la diferencia es una variable de entorno y no una
// version distinta del codigo.
builder.Services.Configure<OpcionesCorreo>(
    builder.Configuration.GetSection(OpcionesCorreo.Seccion));

var opcionesCorreo = builder.Configuration
    .GetSection(OpcionesCorreo.Seccion).Get<OpcionesCorreo>() ?? new OpcionesCorreo();

if (opcionesCorreo.EstaCompleta())
    builder.Services.AddScoped<ICanalNotificacion, CanalCorreo>();
else
    builder.Services.AddScoped<ICanalNotificacion, CanalRegistro>();

// El timbre entre quien encola un aviso y quien lo entrega. Singleton
// porque el semaforo tiene que ser el mismo para todos: si cada
// peticion tuviera el suyo, tocaria un timbre que nadie escucha.
builder.Services.AddSingleton<AvisadorOutbox>();

// El despachador del Outbox (RF-08). Corre fuera de las peticiones
// para que un servidor de correo lento no vuelva lenta la asignacion
// de una cita.
builder.Services.AddHostedService<DespachadorNotificaciones>();

// CORS solo para cuando el frontend corre aparte, que en mi maquina es
// Live Server en otro puerto. Al desplegar, el frontend sale de wwwroot
// de esta misma aplicacion: mismo origen, y entonces CORS no interviene.
// Los origenes vienen de configuracion y no van escritos aqui, porque
// AllowAnyOrigin en un servidor publico deja que cualquier pagina
// consuma la API desde el navegador de quien sea.
string[] origenesPermitidos = builder.Configuration
    .GetSection("Cors:Origenes").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirFrontend", policy =>
    {
        if (origenesPermitidos.Length == 0)
            policy.WithOrigins("https://localhost");
        else
            policy.WithOrigins(origenesPermitidos);

        policy.AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// ------------------- ARRANQUE -------------------

// Los usuarios de la semilla quedaron con un marcador en lugar de hash,
// porque BCrypt no se puede calcular desde un script SQL. Sin esto
// nadie puede entrar y el panel que asigna contrasenas tambien exige
// sesion: el sistema no tendria forma de darse de alta a si mismo.
//
// Va dentro de un try porque esto corre antes de que la aplicacion
// empiece a atender, y una excepcion aqui no la deja arrancar. En
// Azure la base es serverless y se pausa sola: despertarla tarda mas
// de los 30 segundos que espera la conexion, asi que el primer
// arranque del dia fallaba y la aplicacion entera moria con un 500.30.
//
// No tiene sentido que la aplicacion no levante por una tarea de
// arranque que es opcional. Si falla, lo anoto y sigo: /api/salud
// dira que la base no responde, y la semilla se reintenta en el
// siguiente reinicio.
try
{
    await SemillaSeguridad.AsignarPasswordsPendientesAsync(
        app.Services.GetRequiredService<DapperContext>(),
        app.Configuration,
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SemillaSeguridad"));
}
catch (Exception ex)
{
    app.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("SemillaSeguridad")
        .LogError(ex, "No se pudo asignar las contrasenas iniciales. " +
                      "La aplicacion arranca igual; se reintenta en el proximo reinicio.");
}

// Dejo constancia de por donde van a salir los avisos. Si manana no
// llega ningun correo, lo primero que quiero saber es si el canal real
// quedo activo o si la configuracion estaba incompleta y se cayo al de
// registro sin decir nada.
app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Notificaciones")
   .LogInformation(
        opcionesCorreo.EstaCompleta()
            ? "Los avisos salen por correo desde {Remitente} vía {Servidor}:{Puerto}."
            : "Los avisos se escriben en el registro: falta configurar la seccion Correo.",
        opcionesCorreo.CorreoRemitente, opcionesCorreo.Servidor, opcionesCorreo.Puerto);

// ------------------- PIPELINE -------------------
// El orden importa: primero se resuelve quien es, despues que puede hacer.

// Swagger queda tras una bandera de configuracion en lugar de atado al
// entorno, para poder apagarlo en el servidor sin recompilar. Lo dejo
// encendido para la demostracion: los endpoints ya exigen token, asi
// que sin credenciales lo unico que se alcanza es el login. Lo que
// revela es la forma de la API, no acceso a los datos.
bool swaggerVisible = app.Configuration.GetValue("Swagger:Publico", false)
                   || app.Environment.IsDevelopment();

if (swaggerVisible)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Asilo v1");
        c.RoutePrefix = "swagger";
    });
}

// No pongo UseHttpsRedirection. En App Service el certificado lo
// termina la plataforma y a la aplicacion le llega HTTP por dentro, de
// modo que esta linea veria una peticion insegura y redirigiria en
// ciclo. Lo equivalente alla es la opcion "HTTPS Only" del servicio,
// que corta la peticion antes de que llegue aqui.

// El frontend sale de wwwroot de esta misma aplicacion. Es un solo
// servicio en lugar de dos, y al compartir origen desaparece el
// problema de CORS. UseDefaultFiles va antes que UseStaticFiles porque
// lo que hace es reescribir "/" al archivo de inicio; si fuera despues,
// StaticFiles ya habria dejado pasar la peticion sin resolverla.
var archivosDeInicio = new DefaultFilesOptions();
archivosDeInicio.DefaultFileNames.Clear();
archivosDeInicio.DefaultFileNames.Add("login.html");
app.UseDefaultFiles(archivosDeInicio);
app.UseStaticFiles();

app.UseCors("PermitirFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Sonda de diagnostico. Es lo primero que consulto despues de
// desplegar: separa "la aplicacion no arranco" de "arranco pero no
// alcanza la base", que desde el navegador se ven igual.
app.MapGet("/api/salud", async (DapperContext contexto) =>
{
    try
    {
        using var conexion = contexto.CreateConnection();
        await conexion.ExecuteScalarAsync<int>("SELECT 1;");
        return Results.Ok(new { aplicacion = "arriba", baseDatos = "responde" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new
            {
                aplicacion = "arriba",
                baseDatos = "sin respuesta",
                // El detalle solo cuando Swagger esta visible: fuera de
                // la demostracion, el texto de una excepcion de SqlClient
                // nombra el servidor y el usuario de la conexion.
                detalle = swaggerVisible ? ex.Message : null
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();
