using Microsoft.AspNetCore.HttpOverrides;
using ShieldReport.Api.Startup;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
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
            // Lets a cross-origin frontend (dev server) read download file names.
            .WithExposedHeaders("Content-Disposition")
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
builder.Services.AddHealthChecks().AddReadinessChecks();

// Without this, ASP.NET Core falls back to its default per-container key ring (warned about at
// startup: "Storing keys in a directory '/root/.aspnet/DataProtection-Keys' that may not be
// persisted outside of the container"). Every LlmProviderConfiguration's API key is encrypted
// via DataProtectionApiKeyEncryptor, so a container recreate silently orphans all of them —
// confirmed as the cause of GET /api/v1/LlmProviderConfigurations 500ing with
// "CryptographicException: key not found in the key ring" after a `docker compose up --build`.
// DataProtection-Keys below is a named volume (see docker-compose.yml) so the ring survives
// rebuilds/recreates.
builder.Services.AddDataProtection()
    .SetApplicationName("ShieldReport")
    // <ContentRoot>/DataProtection-Keys = /app/DataProtection-Keys in the container (same path the
    // volume mounts), and a writable project-local folder when running outside Docker.
    .PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath
            ? keysPath
            : Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys")));

builder.Services.AddSignalR();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ShieldReport.Application.Common.Interfaces.ICurrentUserService, ShieldReport.Api.Startup.CurrentUserService>();
builder.Services.AddScoped<ShieldReport.Application.Common.Interfaces.Services.IRealtimeNotifier, ShieldReport.Api.Startup.SignalRRealtimeNotifier>();
builder.Services.AddSingleton<ShieldReport.Application.Common.Interfaces.Services.IScanRealtimeNotifier, ShieldReport.Api.Startup.SignalRScanRealtimeNotifier>();
builder.Services.AddScoped<ShieldReport.Application.Common.Interfaces.Services.IFileStorageService, ShieldReport.Api.Startup.LocalFileStorageService>();
builder.Services.Configure<ShieldReport.Infrastructure.Nvd.NvdOptions>(builder.Configuration.GetSection(ShieldReport.Infrastructure.Nvd.NvdOptions.SectionName));
builder.Services.AddHttpClient<ShieldReport.Application.Common.Interfaces.Services.INvdClient, ShieldReport.Infrastructure.Nvd.NvdClient>();
// LLM calls are always background or explicit tests now, but a hung provider must not hold a job
// lease or a request for the default 100s: fail after 60s and let the job retry.
builder.Services.AddHttpClient<ShieldReport.Infrastructure.Llm.OpenAiCompatibleLlmClient>(c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddHttpClient<ShieldReport.Infrastructure.Llm.GeminiLlmClient>(c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddSingleton<ShieldReport.Infrastructure.Llm.AnthropicLlmClient>();
builder.Services.AddScoped<ShieldReport.Application.Common.Interfaces.Services.ILlmClientFactory, ShieldReport.Infrastructure.Llm.LlmClientFactory>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddScanning();


// Shutdown grace period. Scans no longer need it (they detach and their containers keep running),
// but in-flight background jobs and final scan status writes get time to finish. Keep this below
// the api service's stop_grace_period in docker-compose.yml, or Docker SIGKILLs the process first.
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(40));

// Rate limiting: fixed one-minute windows, partitioned by user id for authenticated requests and
// by client IP for anonymous ones (login, refresh, public site settings). Limits come from
// RateLimiting:PerUserPerMinute / RateLimiting:AnonymousPerMinute (env: RateLimiting__*). The
// per-user default leaves room for an SPA polling several pages; keep the anonymous one low, it
// is what throttles password guessing.
var perUserPerMinute = Math.Max(1, builder.Configuration.GetValue("RateLimiting:PerUserPerMinute", 600));
var anonymousPerMinute = Math.Max(1, builder.Configuration.GetValue("RateLimiting:AnonymousPerMinute", 30));
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
                PermitLimit = perUserPerMinute,
                Window = TimeSpan.FromMinutes(1)
            });
        }

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"ip:{ipAddress}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = anonymousPerMinute,
            Window = TimeSpan.FromMinutes(1)
        });
    });
});

// Caching: bind options and register the in-memory distributed cache
var cachingSection = builder.Configuration.GetSection("Caching");
builder.Services.Configure<ShieldReport.Application.Common.Configuration.CachingOptions>(cachingSection);
builder.Services.Configure<ShieldReport.Application.Common.Configuration.FrontendOptions>(
    builder.Configuration.GetSection(ShieldReport.Application.Common.Configuration.FrontendOptions.SectionName));
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

// Behind the Caddy proxy every request arrives from the proxy's address. Trust exactly one
// X-Forwarded-For/-Proto hop so RemoteIpAddress is the real client — the anonymous rate limiter
// partitions by IP (otherwise all anonymous users would share one bucket) and the audit log
// records it. The proxy is only reachable inside the compose network, so the known-proxy lists
// are cleared rather than pinned to a container IP that changes on every recreate.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
// Request logging outside the exception handler, so it records the status code the handler
// actually returns (inside it, every handled 4xx was logged as a 500 error).
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseSecurityHeaders();
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


// Liveness: no dependency checks (container HEALTHCHECK). Readiness: database, Docker, disk.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(ReadinessHealthChecks.Tag),
    ResponseWriter = ReadinessHealthChecks.WriteJsonAsync,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});
app.MapControllers();
app.MapHub<ShieldReport.Api.Hubs.NotificationHub>("/hubs/notifications");



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
