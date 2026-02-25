using Microsoft.EntityFrameworkCore;
using OrderService.Exceptions;
using OrderService.Responses;
using System.Net;
using System.Text.Json;

namespace OrderService.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }      
        catch (NotFoundException ex)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await WriteResponse(context, ex.Message);
        }       
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database update failure");

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await WriteResponse(context, "Service temporarily unavailable. Please try again later.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled Exception");

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteResponse(context, "Internal server error");
        }
    }

    private static async Task WriteResponse(HttpContext context, string message, object? errors = null)
    {
        context.Response.ContentType = "application/json";

        var response = ApiResponse<string>.FailResponse(message, errors);
        var json = JsonSerializer.Serialize(response);

        await context.Response.WriteAsync(json);
    }
}
