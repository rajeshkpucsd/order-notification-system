using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using NotificationService.Data;
using NotificationService.Messaging;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));
builder.Services.AddHostedService<RabbitMqConsumer>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Notification Service",
        Version = "v1"
    });
});
builder.WebHost.UseUrls("http://0.0.0.0:8081");
var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/api/notifications", async (NotificationDbContext db) =>
{
    return await db.Notifications.ToListAsync();
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

    try
    {
        if (db.Database.GetPendingMigrations().Any())
        {
            db.Database.Migrate();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Migration failed: " + ex.Message);
    }
}
app.Run();