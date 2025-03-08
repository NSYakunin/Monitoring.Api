using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Monitoring.Api.Extensions
{
    /// <summary>
    /// Методы-расширения для настройки конвейера обработки (middleware).
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Подключение и настройка Swagger (UseSwagger & UseSwaggerUI)
        /// </summary>
        public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Monitoring API V1");
            });

            return app;
        }
    }
}