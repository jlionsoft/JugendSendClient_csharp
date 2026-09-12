using BotApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Custom services
builder.Services.AddHttpClient();
// Nextcloud Talk bot HTTP client (liest BASE URL aus Umgebungsvariablen)
builder.Services.AddHttpClient("nextcloud", client =>
{
	var baseUrl = Environment.GetEnvironmentVariable("NEXTCLOUD_BASE_URL")
		?? throw new InvalidOperationException("Umgebungsvariable NEXTCLOUD_BASE_URL ist nicht gesetzt.");
	client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
});
builder.Services.AddSingleton<AuthentikService>();
builder.Services.AddSingleton<MatrixService>();
builder.Services.AddSingleton<MessageService>();
builder.Services.AddSingleton<UserProvisioningService>();
builder.Services.AddSingleton<MatrixAdminService>();
builder.Services.AddSingleton<AuthentikUserService>();
builder.Services.AddHttpClient<SmsService>();

builder.Services.AddHttpClient("nextcloud", client =>
{
    var baseUrl = builder.Configuration["NextcloudTalk:BaseUrl"]
        ?? throw new InvalidOperationException("NextcloudTalk:BaseUrl ist nicht konfiguriert.");
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
});

builder.Services.AddScoped<INextcloudTalkService, NextcloudTalkService>();
var app = builder.Build();

// Enable Swagger in ALL environments
app.UseSwagger();
app.UseSwaggerUI();

// Middleware
app.UseHttpsRedirection();
app.UseAuthorization();

// Controllers
app.MapControllers();

app.Run();