using Monitoring.Api.Extensions;
using QuestPDF.Infrastructure;


var builder = WebApplication.CreateBuilder(args);

// ��������� ��� ������� ����� ������ ����������
builder.Services.AddSignalR();
builder.Services.AddJwtAuthentication(builder.Configuration);    // 1) JWT + 2) ��������������
builder.Services.AddDatabase(builder.Configuration);             // 3) ���� ������
builder.Services.AddMemoryCacheService();                        // 4) MemoryCache
builder.Services.AddAppServices();                               // 5) ������� (LoginService, UserSettingsService � �.�.)
builder.Services.AddSwaggerDocumentation();                      // 6) Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCustomCors();                                // 7) CORS

var app = builder.Build();

// ��������, ����� ������ �������� QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

// ����������� Middleware Pipeline
app.MapHub<Monitoring.Api.Hubs.ChatHub>("/chatHub");
app.UseDefaultFiles();    // <-- �������� ���� �����
app.UseStaticFiles();
app.UseSwaggerDocumentation();  // ���������� Swagger
app.UseRouting();
app.UseCors("MyPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();