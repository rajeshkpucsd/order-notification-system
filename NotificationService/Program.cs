using Microsoft.AspNetCore.Builder;
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
var app = builder.Build();


app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/api/notifications", async (NotificationDbContext db) =>
{
    return await db.Notifications.ToListAsync();
});

app.MapGet("/api/notifications/{id}", async (Guid id, NotificationDbContext db) =>
{
    var notification = await db.Notifications.FindAsync(id);
    return notification == null ? Results.NotFound() : Results.Ok(notification);
});

app.MapGet("/api/notifications/order/{orderId}", async (Guid orderId, NotificationDbContext db) =>
{
    var list = await db.Notifications
        .Where(n => n.OrderId == orderId)
        .ToListAsync();

    return Results.Ok(list);
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
        app.Logger.LogError(ex, "Migration failed during NotificationService startup");
        throw;
    }
}
app.Run();
