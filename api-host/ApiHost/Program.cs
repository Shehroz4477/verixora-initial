using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
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

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

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
    });

builder.Services.AddAuthorization();
var allowedCorsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("VerixoraWeb", policy =>
{
    if (allowedCorsOrigins.Length == 0)
        throw new InvalidOperationException("Cors:AllowedOrigins must be configured.");
    policy.WithOrigins(allowedCorsOrigins).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddScoped<IAuditLogService,AuditLogService>();

// Add Identity module
builder.Services.AddIdentityInfrastructure(builder.Configuration);
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
});

builder.Services.AddControllers()
    .AddApplicationPart(typeof(AuthController).Assembly)
    .AddApplicationPart(typeof(DevicesController).Assembly)
    .AddApplicationPart(typeof(SmartLocksController).Assembly)
    .AddApplicationPart(typeof(AuditLogsController).Assembly)
    .AddApplicationPart(typeof(HomesController).Assembly);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("VerixoraWeb");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
