using BotApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Custom services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<AuthentikService>();
builder.Services.AddSingleton<MatrixService>();
builder.Services.AddSingleton<MessageService>();
builder.Services.AddSingleton<UserProvisioningService>();
builder.Services.AddSingleton<MatrixAdminService>();
builder.Services.AddSingleton<AuthentikUserService>();
builder.Services.AddHttpClient<SmsService>();

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