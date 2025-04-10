using Monitoring.Application.DTO;
using Monitoring.Application.Interfaces;
using Monitoring.Infrastructure.Data;
using Monitoring.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Text;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Monitoring.Infrastructure.Repositories;

namespace Monitoring.Api.Extensions
{
    /// <summary>
    /// Методы-расширения для регистрации различных сервисов в DI-контейнере.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 1) JWT Configuration + 2) Authentication
        /// </summary>
        public static IServiceCollection AddJwtAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Читаем настройки из конфигурации
            services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
            var jwtSection = configuration.GetSection("JwtSettings");
            var secretKey = jwtSection.GetValue<string>("SecretKey");
            var issuer = jwtSection.GetValue<string>("Issuer");
            var audience = jwtSection.GetValue<string>("Audience");

            // Настраиваем аутентификацию
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = issuer,
                        ValidateAudience = true,
                        ValidAudience = audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(secretKey)),
                        // На боевом сервере включаем ValidateLifetime = true
                        ValidateLifetime = false,
                        ClockSkew = TimeSpan.Zero
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            // Достаём токен из query["access_token"], если запрос к /chatHub
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            return services;
        }

        /// <summary>
        /// 3) Подключение к базе данных
        /// </summary>
        public static IServiceCollection AddDatabase(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Читаем строку подключения из конфигурации
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<MyDbContext>(options =>
                options.UseSqlServer(connectionString));

            return services;
        }

        /// <summary>
        /// 4) MemoryCache
        /// </summary>
        public static IServiceCollection AddMemoryCacheService(this IServiceCollection services)
        {
            services.AddMemoryCache();
            return services;
        }

        /// <summary>
        /// 5) Подключение прикладных сервисов (LoginService, UserSettingsService и т.д.)
        /// </summary>
        public static IServiceCollection AddAppServices(this IServiceCollection services)
        {
            services.AddScoped<ILoginService, LoginService>();
            services.AddScoped<IUserSettingsService, UserSettingsService>();
            services.AddScoped<IWorkItemAppService, WorkItemAppService>();
            services.AddScoped<IWorkRequestService, WorkRequestAppService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IPerformanceService, PerformanceService>();

            services.AddScoped<IWorkItemRepository, WorkItemRepository>();
            services.AddScoped<IWorkItemFilter, WorkItemFilter>();
            services.AddScoped<IWorkItemHighlighter, WorkItemHighlighter>();

            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IChatRepository, ChatRepository>();

            return services;
        }

        /// <summary>
        /// 6) Настройки Swagger
        /// </summary>
        public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Monitoring API",
                    Version = "v1",
                    Description = "API для системы мониторинга"
                });

                // Настройка JWT авторизации в Swagger
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Пример: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
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

            return services;
        }

        /// <summary>
        /// 7) CORS
        /// </summary>
        public static IServiceCollection AddCustomCors(this IServiceCollection services)
        {
            services.AddCors(opt =>
            {
                opt.AddPolicy("MyPolicy", policy =>
                {
                    // Для разработки иногда проще разрешить всё:
                    policy
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .SetIsOriginAllowed(_ => true) // или .WithOrigins("http://localhost:3000") если хотим конкретный origin
                        .AllowCredentials();          // обязательно для SignalR
                });
            });

            return services;
        }
    }
}