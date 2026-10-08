using System.Text.Json.Serialization;
using Asilo.Api.Data;
using Asilo.Api.Repositories;
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
});

// Singleton: DapperContext solo guarda la cadena de conexion, no tiene estado.
builder.Services.AddSingleton<DapperContext>();

// Scoped: un repositorio por peticion HTTP.
builder.Services.AddScoped<IResidenteRepository, ResidenteRepository>();
builder.Services.AddScoped<IRemisionRepository, RemisionRepository>();

// CORS para que el frontend estatico pueda consumir la API.
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirFrontend", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

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
app.UseAuthorization();
app.MapControllers();

app.Run();
