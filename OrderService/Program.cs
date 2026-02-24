using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Messaging;
using OrderService.Middleware;
using OrderService.Repositories;
using OrderService.Responses;
using OrderService.Services;
using OrderService.Validators;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Customize the response for model validation errors to return a consistent API response format
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => JsonNamingPolicy.CamelCase.ConvertName(kvp.Key),
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
            );

        var response = ApiResponse<object>.FailResponse("Validation failed", errors);
        return new BadRequestObjectResult(response);
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService.Services.OrderService>();
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderValidator>();

builder.WebHost.UseUrls("http://0.0.0.0:8080");
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthorization();
app.UseMiddleware<ExceptionMiddleware>();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

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
