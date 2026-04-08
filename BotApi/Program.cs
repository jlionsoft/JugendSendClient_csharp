using BotApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Custom services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<AuthentikService>();
builder.Services.AddSingleton<MatrixService>();
builder.Services.AddSingleton<MessageService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();