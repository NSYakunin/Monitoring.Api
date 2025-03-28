using Monitoring.Api.Extensions;
using QuestPDF.Infrastructure;


var builder = WebApplication.CreateBuilder(args);

// Добавляем все сервисы через методы расширения
builder.Services.AddJwtAuthentication(builder.Configuration);    // 1) JWT + 2) Аутентификация
builder.Services.AddDatabase(builder.Configuration);             // 3) База данных
builder.Services.AddMemoryCacheService();                        // 4) MemoryCache
builder.Services.AddAppServices();                               // 5) Сервисы (LoginService, UserSettingsService и т.д.)
builder.Services.AddSwaggerDocumentation();                      // 6) Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCustomCors();                                // 7) CORS

var app = builder.Build();

// Например, сразу укажем лицензию QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

// Настраиваем Middleware Pipeline
app.UseStaticFiles();
app.UseSwaggerDocumentation();  // Подключаем Swagger
app.UseRouting();
app.UseCors("MyPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();