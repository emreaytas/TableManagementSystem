// Program.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using System.Text;
using TableManagement.API.Middleware;
using TableManagement.Application;
using TableManagement.Application.Services;
using TableManagement.Core.Entities;
using TableManagement.Core.Interfaces;
using TableManagement.Infrastructure;
using TableManagement.Infrastructure.Data;
using TableManagement.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Serilog konfigürasyonu
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "TableManagement.API")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .CreateLogger();

// Host'u Serilog kullanacak þekilde yapýlandýr
builder.Host.UseSerilog();

try
{
    Log.Information("Starting up TableManagement API");

    // Add services to the container.
    builder.Services.AddControllers();

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Table Management API", Version = "v1" });

        // JWT Authentication için Swagger konfigürasyonu
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
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

    // Database context
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Identity (int key kullanýmý için düzeltildi)
    builder.Services.AddIdentity<User, IdentityRole<int>>(options =>
    {
        // Password requirements - Çok katý olmayan ayarlar
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;

        // User settings
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

        // Email confirmation settings
        options.SignIn.RequireConfirmedEmail = true;
        options.SignIn.RequireConfirmedAccount = false;

        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

    // JWT Authentication - DÜZELTÝLDÝ
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"];
    var issuer = jwtSettings["Issuer"] ?? "TableManagementAPI";
    var audience = jwtSettings["Audience"] ?? "TableManagementClients";

    if (string.IsNullOrEmpty(secretKey))
    {
        throw new InvalidOperationException("JWT SecretKey is not configured in appsettings.json!");
    }

    Log.Information("Configuring JWT with Issuer: {Issuer}, Audience: {Audience}", issuer, audience);

    var key = Encoding.ASCII.GetBytes(secretKey); // UTF8 yerine ASCII kullan

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // Development için
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RequireExpirationTime = true
        };

        // JWT events için detaylý loglama
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Error("JWT Authentication failed: {Error} for path {Path}",
                    context.Exception.Message, context.Request.Path);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Log.Warning("JWT Challenge for {Path} from {IP}. Error: {Error}, Description: {ErrorDescription}",
                    context.Request.Path,
                    GetClientIPAddress(context.HttpContext),
                    context.Error,
                    context.ErrorDescription);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Log.Information("JWT Token validated for user {User} on path {Path}",
                    context.Principal?.Identity?.Name ?? "Unknown",
                    context.Request.Path);
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
                if (!string.IsNullOrEmpty(token))
                {
                    Log.Debug("JWT Token received for path {Path}: {TokenStart}...",
                        context.Request.Path,
                        token.Length > 20 ? token.Substring(0, 20) : token);
                }
                return Task.CompletedTask;
            }
        };
    });

    // Authorization
    builder.Services.AddAuthorization();

    // Application layer services (AutoMapper dahil) - DÜZELTÝLDÝ
    builder.Services.AddApplication();

    // Infrastructure layer services - DÜZELTÝLDÝ
    builder.Services.AddInfrastructure(builder.Configuration);

    // Additional services
    builder.Services.AddScoped<ILoggingService, LoggingService>();
    builder.Services.AddScoped<IDataDefinitionService, DataDefinitionService>();
    builder.Services.AddScoped<ISecurityLogService, SecurityLogService>();

    // CORS - Frontend URL'ini dinamik olarak al
    var frontendUrl = builder.Configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowSpecificOrigin", policy =>
        {
            policy.WithOrigins(frontendUrl, "https://localhost:5173", "http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

    // HTTP Logging (ek loglama için)
    builder.Services.AddHttpLogging(logging =>
    {
        logging.LoggingFields = Microsoft.AspNetCore.HttpLogging.HttpLoggingFields.All;
        logging.RequestHeaders.Add("X-Forwarded-For");
        logging.RequestHeaders.Add("X-Real-IP");
        logging.ResponseHeaders.Add("X-Response-Time");
        logging.MediaTypeOptions.AddText("application/json");
        logging.RequestBodyLogLimit = 4096;
        logging.ResponseBodyLogLimit = 4096;
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Table Management API V1");
            // RoutePrefix = string.Empty kaldýrýldý - standart /swagger route kullan
        });
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    // Serilog için HTTP request loglama
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.GetLevel = (httpContext, elapsed, ex) =>
        {
            if (ex != null) return LogEventLevel.Error;
            if (httpContext.Response.StatusCode > 499) return LogEventLevel.Error;
            if (httpContext.Response.StatusCode > 399) return LogEventLevel.Warning;
            if (elapsed > 10000) return LogEventLevel.Warning; // 10 saniyeden uzun istekler
            return LogEventLevel.Information;
        };
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].FirstOrDefault());
            diagnosticContext.Set("ClientIP", GetClientIPAddress(httpContext));

            if (httpContext.User.Identity?.IsAuthenticated == true)
            {
                diagnosticContext.Set("UserId", httpContext.User.Identity.Name);
            }
        };
    });

    // Middleware'larý doðru sýrayla ekle
    app.UseMiddleware<SecurityMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseHttpLogging();
    }

    // CORS middleware should be before authentication
    app.UseCors("AllowSpecificOrigin");

    // Authentication & Authorization - SIRALAMA ÖNEMLÝ
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Database initialization - EKLENDI
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            // Ensure database is created
            if (context.Database.EnsureCreated())
            {
                Log.Information("Database created successfully");
            }
            else
            {
                Log.Information("Database already exists");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error ensuring database creation");
            throw;
        }
    }

    Log.Information("TableManagement API started successfully on {Urls}",
        string.Join(", ", builder.WebHost.GetSetting("urls")?.Split(';') ?? new[] { "Unknown" }));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static string GetClientIPAddress(HttpContext context)
{
    var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    if (!string.IsNullOrEmpty(xForwardedFor))
    {
        return xForwardedFor.Split(',')[0].Trim();
    }

    var xRealIP = context.Request.Headers["X-Real-IP"].FirstOrDefault();
    if (!string.IsNullOrEmpty(xRealIP))
    {
        return xRealIP;
    }

    return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
}