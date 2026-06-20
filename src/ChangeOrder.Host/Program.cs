using ChangeOrder.Business.Extensions;
using ChangeOrder.Data.Extensions;
using ChangeOrder.Host.Extensions;
using ChangeOrder.Presentation.Extensions;
using Scalar.AspNetCore;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration));

// Servicios
string connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")!;

builder.Services.AddDataServices(connectionString);
builder.Services.AddBusinessServices();
builder.Services.AddPresentationServices();
builder.Services.AddHostServices();

// Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "sqlserver");

// OpenAPI
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((doc, ctx, ct) =>
    {
        doc.Info.Title = "ChangeOrder API";
        doc.Info.Version = "v1";
        doc.Info.Description = "Sistema de Control de Órdenes de Cambio";
        return Task.CompletedTask;
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("InternalNetwork", policy =>
    {
        policy.WithOrigins("http://localhost:5151") // ajustar según las IPs internas reales
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

WebApplication app = builder.Build();

// Pipeline
app.UseResponseCompression();
app.UseCors("InternalNetwork");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapPresentationEndpoints();
app.MapHealthChecks("/health");

app.MapGet("/version", () =>
{
    System.Reflection.AssemblyName assemblyName =
        System.Reflection.Assembly.GetExecutingAssembly().GetName();

    return Results.Ok(new
    {
        name = assemblyName.Name,
        version = assemblyName.Version?.ToString(),
        environment = app.Environment.EnvironmentName
    });
});

app.Run();
