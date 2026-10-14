using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Asilo.Api.Data;
using Asilo.Api.Repositories;
using Asilo.Api.Services;
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

// CORS para que el frontend estatico pueda consumir la API.
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirFrontend", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// ------------------- ARRANQUE -------------------

// Los usuarios de la semilla quedaron con un marcador en lugar de hash,
// porque BCrypt no se puede calcular desde un script SQL. Sin esto
// nadie puede entrar y el panel que asigna contrasenas tambien exige
// sesion: el sistema no tendria forma de darse de alta a si mismo.
await SemillaSeguridad.AsignarPasswordsPendientesAsync(
    app.Services.GetRequiredService<DapperContext>(),
    app.Configuration,
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SemillaSeguridad"));

// ------------------- PIPELINE -------------------
// El orden importa: primero se resuelve quien es, despues que puede hacer.

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "API Asilo v1");
    c.RoutePrefix = "swagger";
});

// Desactivado en desarrollo: el perfil por defecto de launchSettings
// solo levanta HTTP (5177), asi que redirigir a HTTPS rompe los fetch
// del frontend. Se reactiva al desplegar en Azure, donde el
// certificado lo administra la plataforma.
// app.UseHttpsRedirection();
app.UseCors("PermitirFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
