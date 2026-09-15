using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SSLConfiguration.Application;
using SSLConfiguration.Application.Services;
using SSLConfiguration.Infrastructure;
using System.Configuration;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// VerisignGateway (ComodoParseCSR etc.) reads ConfigurationManager.AppSettings — not IConfiguration.
// Sync AppSettings:* from appsettings*.json so keys like Comodo_ParseCSR are available.
SyncAppSettingsToConfigurationManager(builder.Configuration);

// Same TLS posture as old Global.asax Application_Start
bool enableTls12 = builder.Configuration.GetValue("AppSettings:EnableTls12", true);
if (enableTls12)
{
    ServicePointManager.Expect100Continue = true;
    ServicePointManager.SecurityProtocol =
        SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
}

builder.Services.AddControllers();
builder.Services.AddSystemWebAdapters();
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key missing");
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

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
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = builder.Configuration["AppSettings:ApiName"] ?? "SSL Configuration Core API",
        Version = builder.Configuration["AppSettings:ApiVersion"] ?? "v1",
        Description = "ASP.NET Core 8 migration API for ManageOrder/SSLConfiguration JSON endpoints. First slice: Digicert.UpdateDigicertVMCHostingFileSetting."
    });
    // Avoid schemaId clashes between Digicert.* and Comodo.* contract types with the same class name.
    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT. Example: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
#region Rate limiting for API endpoints
builder.Services.AddRateLimiter(options =>
{
    // General: 100 requests per minute per IP
    options.AddFixedWindowLimiter("general", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.QueueLimit = 0;
    });
    // Login/captcha: 10 per minute per IP
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
        opt.QueueLimit = 0;
    });
    // Place order: 5 per minute per IP
    options.AddFixedWindowLimiter("placeorder", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5;
        opt.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Partition by client IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 200,
            QueueLimit = 0
        });
    });
});
#endregion
builder.Services.AddCors(options =>
{
    options.AddPolicy("SslConfigurationUi", policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true); // UI Framework site (IIS Express / localhost)
    });
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthorizationHandler,ClientAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "ClientAuthorization",
        policy =>
        {
            policy.AddRequirements(
                new ClientAuthorizationRequirement());
        });
});

AppConfig.DefaultComodoCredential = builder.Configuration["AppSettings:DefaultComodoCredential"];
AppConfig.DigicertWebServerList = builder.Configuration["AppSettings:DigicertWebServerList"];
AppConfig.DONOTAuthorizedCodeSign = builder.Configuration["AppSettings:DONOTAuthorizedCodeSign"];
AppConfig.AcemeApiUrl = builder.Configuration["AppSettings:AcemeApiUrl"];
AppConfig.SSL2BuyWebApiAuthKey = builder.Configuration["AppSettings:SSL2BuyWebApiAuthKey"];
AppConfig.AcmeSSL2BuyApiUrl = builder.Configuration["AppSettings:AcmeSSL2BuyApiUrl"];
AppConfig.CheapSSLShopApiUrl = builder.Configuration["AppSettings:CheapSSLShopApiUrl"];
AppConfig.GoogleTranslateApiUrl = builder.Configuration["AppSettings:GoogleTranslateApiUrl"];
AppConfig.GoogleTranslateApiKey = builder.Configuration["AppSettings:GoogleTranslateApiKey"];
AppConfig.AdminUserName = builder.Configuration["AppSettings:AdminUserName"];
AppConfig.AdminPassword = builder.Configuration["AppSettings:AdminPassword"];
AppConfig.SmtpServer = builder.Configuration["AppSettings:SmtpServer"];
AppConfig.SmtpUsername = builder.Configuration["AppSettings:SmtpUsername"];
AppConfig.SmtpPassword = builder.Configuration["AppSettings:SmtpPassword"];
AppConfig.EnableSsl = bool.TryParse(builder.Configuration["AppSettings:EnableSsl"], out bool enableSsl) && enableSsl;
AppConfig.SmtpPort = int.TryParse(builder.Configuration["AppSettings:SmtpPort"], out int smtpPort) ? smtpPort : 0;

var app = builder.Build();

// Core adaptation of old LogWriter MapPath / Request.UserHostAddress
LogWriter.ContentRoot = app.Environment.ContentRootPath;
var httpContextAccessor = app.Services.GetRequiredService<IHttpContextAccessor>();
LogWriter.GetClientIp = () =>
    httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "SSL Configuration Core API v1");
        options.DocumentTitle = "SSL Configuration Core API";
    });
}

app.UseHttpsRedirection();
app.UseSystemWebAdapters();
app.UseCors("SslConfigurationUi");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Logger.LogInformation(
    "API started. Environment={Environment}; Assembly={Assembly}; Comodo_ParseCSR={ComodoParseCsr}",
    app.Environment.EnvironmentName,
    Assembly.GetExecutingAssembly().GetName().Version,
    System.Configuration.ConfigurationManager.AppSettings["Comodo_ParseCSR"] ?? "(missing)");

app.Run();

static void SyncAppSettingsToConfigurationManager(IConfiguration configuration)
{
    foreach (var child in configuration.GetSection("AppSettings").GetChildren())
    {
        if (string.IsNullOrEmpty(child.Key) || child.Value is null)
            continue;

        // Skip nested objects; AppSettings leaves are flat string keys.
        if (child.GetChildren().Any())
            continue;

        System.Configuration.ConfigurationManager.AppSettings.Set(child.Key, child.Value);
    }
}
