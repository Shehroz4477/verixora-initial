using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FluentValidation;
using Identity.Infrastructure;
using MediatR;
using Identity.Application;
using Identity.Presentation;
using Devices.Infrastructure;
using Devices.Presentation;
using Devices.Application;
using SmartLocks.Infrastructure;
using SmartLocks.Presentation;
using SmartLocks.Application;
using AuditLogs.Infrastructure;
using AuditLogs.Presentation;
using AuditLogs.Application;
using Homes.Application;
using Homes.Infrastructure;
using Homes.Presentation;
using ApiHost;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
ConfigureDataProtection(builder);

// ---------------------------
// Database Provider config
// ---------------------------
// ---------------------------
// JWT Authentication
// ---------------------------
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be supplied from secret configuration and contain at least 32 characters.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Verixora";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtIssuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrWhiteSpace(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("VerixoraWeb", policy =>
{
    if (allowedCorsOrigins.Length == 0)
        throw new InvalidOperationException("Cors:AllowedOrigins must be configured.");
    policy.WithOrigins(allowedCorsOrigins).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddScoped<IAuditLogService, SignalRAuditLogService>();

// Add Identity module
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<RegisterUserCommandValidator>();
builder.Services.AddDevicesInfrastructure(builder.Configuration);
builder.Services.AddSmartLocksInfrastructure(builder.Configuration);
builder.Services.AddAuditLogsInfrastructure(builder.Configuration);
builder.Services.AddHomesInfrastructure(builder.Configuration);

// Add MediatR (scanning all application assemblies that contain handlers)
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterUserCommandHandler).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(RegisterDeviceCommandHandler).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(UnlockDoorCommandHandler).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(LogAuditCommandHandler).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(CreateHomeCommandHandler).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

builder.Services.AddControllers()
    .AddApplicationPart(typeof(AuthController).Assembly)
    .AddApplicationPart(typeof(DevicesController).Assembly)
    .AddApplicationPart(typeof(SmartLocksController).Assembly)
    .AddApplicationPart(typeof(AuditLogsController).Assembly)
    .AddApplicationPart(typeof(HomesController).Assembly);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("VerixoraWeb");
app.UseMiddleware<ApiExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MonitoringHub>("/hubs/system-monitoring");
app.MapHealthChecks("/health/live");

app.Run();

static void ConfigureDataProtection(WebApplicationBuilder builder)
{
    var dataProtection = builder.Services
        .AddDataProtection()
        .SetApplicationName("Verixora");

    if (builder.Environment.IsDevelopment())
    {
        // Local API authentication uses JWTs. Ephemeral keys prevent an unrelated
        // Windows user's stale DPAPI key ring from affecting development startup.
        dataProtection.UseEphemeralDataProtectionProvider();
        return;
    }

    var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
    var certificatePath = builder.Configuration["DataProtection:CertificatePath"];
    var certificatePassword = builder.Configuration["DataProtection:CertificatePassword"];
    if (string.IsNullOrWhiteSpace(keyRingPath)
        || string.IsNullOrWhiteSpace(certificatePath)
        || string.IsNullOrWhiteSpace(certificatePassword))
    {
        throw new InvalidOperationException(
            "Production DataProtection requires DataProtection:KeyRingPath, CertificatePath, and CertificatePassword from secret configuration.");
    }

    if (!Path.IsPathFullyQualified(keyRingPath) || !Path.IsPathFullyQualified(certificatePath))
        throw new InvalidOperationException("Production DataProtection paths must be absolute.");
    if (!File.Exists(certificatePath))
        throw new InvalidOperationException("The configured DataProtection certificate file does not exist.");

    var certificate = new X509Certificate2(
        certificatePath,
        certificatePassword,
        X509KeyStorageFlags.EphemeralKeySet);
    if (!certificate.HasPrivateKey)
        throw new InvalidOperationException("The configured DataProtection certificate must include its private key.");

    var keyDirectory = new DirectoryInfo(keyRingPath);
    keyDirectory.Create();
    dataProtection
        .PersistKeysToFileSystem(keyDirectory)
        .ProtectKeysWithCertificate(certificate);
}
