using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Domain.Enums;
using SampleCkWebApp.Application;
using SampleCkWebApp.Infrastructure.Data;
using SampleCkWebApp.WebApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
{
    Directory.SetCurrentDirectory(AppContext.BaseDirectory);
    builder.Configuration
        .SetBasePath(AppContext.BaseDirectory);

    // Create the logger
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Filter.ByExcluding(logEvent => 
            logEvent.Properties.TryGetValue("RequestPath", out var property)
            && property.ToString().StartsWith("\"/health"))
        .CreateLogger();
    
    // Add logger to logging pipeline
    builder.Logging
        .ClearProviders()
        .AddSerilog(Log.Logger);
    
    builder.Host.UseSerilog();
    
    builder.Services.AddControllers();

    //  Add Authentication

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            var jwtSecret = builder.Configuration["Jwt:Secret"];

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true
            };
        });
    
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy =>
            policy.RequireRole(Role.Admin.ToString()));
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Expense Tracker API",
            Version = "v1",
            Description = "A RESTful API for managing expenses, users, and categories in an expense tracking application.",
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "API Support",
                Email = "support@expensetracker.com"
            }
        });

        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\""
        });

        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                new string[] { }
            }
        });
        
        // Include XML comments
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }
        
        // Include XML comments from referenced projects
        var contractsXml = Path.Combine(AppContext.BaseDirectory, "SampleCkWebApp.Contracts.xml");
        if (File.Exists(contractsXml))
        {
            c.IncludeXmlComments(contractsXml);
        }
        
        // Use full names for better organization
        c.CustomSchemaIds(type => type.FullName);
    });    

    // Add CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins("http://localhost:5173","https://expensetracker-frontend-pe55lzh2fa-ew.a.run.app")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });


    builder.Services
        .AddApplication(builder.Configuration)
        .AddInfrastructure(builder.Configuration);
}

Log.Logger.Information("Application starting");

var app = builder.Build();
{
    app.UseRouting();
    
    app.UseCors("AllowFrontend");
    

    app.UseAuthentication();
    app.UseAuthorization();
    
    // Save service provider to static class
    ServicePool.Create(app.Services);

    // Add exception handler and request logging
    app.UseExceptionHandler("/error");

    // Redirection from HTTP to HTTPS
    app.UseHttpsRedirection();


    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.ConfigureAuditLogging(httpContext);
        };
    });
    

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Expense Tracker API v1");
        c.RoutePrefix = "swagger"; // Swagger UI available at /swagger
        c.DocumentTitle = "Expense Tracker API Documentation";
        c.DefaultModelsExpandDepth(-1); // Hide schemas by default
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.EnableFilter();
        c.ShowExtensions();
    });
    
    app.MapControllers();
}


// Start the application
app.Start();

Log.Logger.Information("Application started");
foreach (var url in app.Urls)
{
    Log.Logger.Information("Listening on: {url}", url);
}

app.WaitForShutdown();

Log.Logger.Information("Application shutdown gracefully");
