using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ShieldReport.Api.Authorization;
using ShieldReport.Api.Middleware;
using ShieldReport.Application;
using ShieldReport.Application.Security;
using ShieldReport.Infrastructure;
using ShieldReport.Infrastructure.Authentication;
using ShieldReport.Infrastructure.Logging;
using ShieldReport.Persistence;
using ShieldReport.Persistence.Seeding;
using ShieldReport.Scanning;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Auto-generate and trust development certificate for HTTPS
if (builder.Environment.IsDevelopment())
{
    try
    {
        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "dev-certs https --trust",
            UseShellExecute = true,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
        });
        process?.WaitForExit();
    }
    catch
    {
        // Certificate generation failed, but application can still run
    }
}

builder.Host.UseSerilog((context, _, configuration) =>
    configuration.AddApplicationSinks(context.Configuration));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("x-api-version"),
        new QueryStringApiVersionReader("api-version"));
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Add CORS
builder.Services.AddCors(options =>
{
    // Cors:AllowedOrigins lets any environment (incl. LAN demo deployments) add extra
    // allowed origins via config/env vars without losing the default dev origin.
    var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? Array.Empty<string>();

    var allowedOrigins = builder.Environment.IsDevelopment()
        ? new[] { "http://localhost:5173" }.Union(configuredOrigins).ToArray()
        : (configuredOrigins.Length > 0 ? configuredOrigins : new[] { "https://yourdomain.com" });

    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Emporio.Api",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
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
            []
        }
    });

    options.DocInclusionPredicate((docName, apiDesc) =>
        string.Equals(apiDesc.GroupName, docName, StringComparison.OrdinalIgnoreCase));
});
builder.Services.AddHealthChecks();


builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ShieldReport.Application.Common.Interfaces.ICurrentUserService, ShieldReport.Api.Startup.CurrentUserService>();
builder.Services.AddScoped<ShieldReport.Application.Common.Interfaces.Services.IRealtimeNotifier, ShieldReport.Api.Startup.SignalRRealtimeNotifier>();
builder.Services.AddSingleton<ShieldReport.Application.Common.Interfaces.Services.IScanRealtimeNotifier, ShieldReport.Api.Startup.SignalRScanRealtimeNotifier>();
builder.Services.AddScoped<ShieldReport.Application.Common.Interfaces.Services.IFileStorageService, ShieldReport.Api.Startup.LocalFileStorageService>();
builder.Services.Configure<ShieldReport.Infrastructure.Nvd.NvdOptions>(builder.Configuration.GetSection(ShieldReport.Infrastructure.Nvd.NvdOptions.SectionName));
builder.Services.AddHttpClient<ShieldReport.Application.Common.Interfaces.Services.INvdClient, ShieldReport.Infrastructure.Nvd.NvdClient>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddScanning();

// Hangfire — base wiring only for now; recurring/background jobs land in later phases.
var hangfireConnectionString = builder.Configuration.GetConnectionString("SqlServerConnection")
    ?? throw new InvalidOperationException("Missing connection string: SqlServerConnection.");
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(hangfireConnectionString));
builder.Services.AddHangfireServer();

// Rate limiting — 100 req/min for authenticated users (partitioned by user id),
// 10 req/min per IP for anonymous requests.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var userId = httpContext.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return RateLimitPartition.GetFixedWindowLimiter($"user:{userId}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            });
        }

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"ip:{ipAddress}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1)
        });
    });
});

// Caching: bind options and register the in-memory distributed cache
var cachingSection = builder.Configuration.GetSection("Caching");
builder.Services.Configure<ShieldReport.Application.Common.Configuration.CachingOptions>(cachingSection);
builder.Services.AddDistributedMemoryCache();

builder.Services.AddScoped<ShieldReport.Application.Permissions.IPermissionCache, ShieldReport.Api.Startup.DistributedPermissionCache>();
builder.Services.AddScoped<ShieldReport.Application.Common.Interfaces.IAppCache, ShieldReport.Api.Startup.DistributedAppCache>();
builder.Services.AddSingleton<ShieldReport.Api.Startup.CacheKeyRegistry>();
builder.Services.AddScoped<ShieldReport.Api.Startup.DistributedCacheAdminService>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            ClockSkew = TimeSpan.Zero
        };

        // SignalR can't attach an Authorization header to the WebSocket upgrade request, so the JS
        // client sends the token via ?access_token= instead (see accessTokenFactory in
        // useScanConsoleHub.ts). The JWT bearer handler only reads the Authorization header by
        // default, so without this, every hub negotiation 401s.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                if (path.StartsWithSegments("/hubs"))
                {
                    var hasAuthHeader = !string.IsNullOrEmpty(context.Request.Headers["Authorization"]);
                    Log.Information(
                        "Hub auth attempt: path={Path} hasAuthHeader={HasAuthHeader} hasQueryToken={HasQueryToken}",
                        path, hasAuthHeader, !string.IsNullOrEmpty(accessToken));
                }

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    Log.Warning(context.Exception, "Hub JWT authentication failed for {Path}", context.HttpContext.Request.Path);
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.All)
    {
        options.AddPolicy(permission, policy =>
            policy.Requirements.Add(new PermissionRequirement(permission)));
    }

    options.AddPolicy(Policies.UsersReadOwnOrAny, policy =>
        policy.Requirements.Add(new SelfOrPermissionRequirement(Permissions.UsersRead, "id")));
});

builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, SelfOrPermissionAuthorizationHandler>();
builder.Services.AddScoped<ShieldReport.Api.Startup.MenuPermissionValidator>();
builder.Services.AddScoped<ShieldReport.Api.Startup.PermissionSyncService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSecurityHeaders();
app.UseSerilogRequestLogging();
app.UseRouting();

// CORS must be placed before authentication/authorization middleware
app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Emporio.Api v1");
        // Add JWT Bearer input to Swagger UI
        options.ConfigObject.AdditionalItems["persistAuthorization"] = true;
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseTokenValidation();
app.UseRateLimiter();
app.UseAuthorization();


app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<ShieldReport.Api.Hubs.NotificationHub>("/hubs/notifications");

if (app.Environment.IsDevelopment())
{
    app.MapHangfireDashboard("/hangfire");
}


using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogDebug($"Starting database bootstrapper...");
    var databaseBootstrapper = scope.ServiceProvider.GetRequiredService<IDatabaseBootstrapper>();
    await databaseBootstrapper.InitializeAsync();
    logger.LogDebug($"Database bootstrapper finished.");

    // Validate menus reference existing permissions. Controlled by configuration key: Validation:MenuPermissionStrict (bool, default false)
    try
    {
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var strict = config.GetValue<bool>("Validation:MenuPermissionStrict", false);
        var validator = scope.ServiceProvider.GetRequiredService<ShieldReport.Api.Startup.MenuPermissionValidator>();
        await validator.ValidateAsync(strict);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Menu permission validation failed.");
        if (ex is InvalidOperationException)
            throw;
    }

    // Sync permission constants into the permissions table so DB reflects code constants
    try
    {
        var sync = scope.ServiceProvider.GetRequiredService<ShieldReport.Api.Startup.PermissionSyncService>();
        await sync.SyncAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Permission sync failed.");
    }

    // Verify Seq (if enabled) is reachable and Serilog is emitting
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    try
    {
        var enableSeq = configuration.GetValue<bool?>("Serilog:EnableSeq") ?? true;
        if (enableSeq)
        {
            var seqUrl = configuration["Serilog:SeqServerUrl"] ?? "http://localhost:5341";
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var resp = await http.GetAsync(seqUrl, cancellationToken: CancellationToken.None);
                if (resp.IsSuccessStatusCode)
                {
                    logger.LogInformation("Seq is reachable at {Url}", seqUrl);
                }
                else
                {
                    logger.LogWarning("Seq responded with status {Status} at {Url}", resp.StatusCode, seqUrl);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not reach Seq at {Url}", seqUrl);
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Seq verification failed.");
    }
}

var loggerApp = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
var urls = app.Urls.Any() ? string.Join(", ", app.Urls) : "default Kestrel port";
loggerApp.LogDebug($"App is running on: {urls}");
app.Run();
