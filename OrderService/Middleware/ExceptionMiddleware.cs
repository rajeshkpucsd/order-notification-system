using System.Net;
using System.Text.Json;
using OrderService.Exceptions;
using OrderService.Responses;

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
        catch (BadRequestException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await WriteResponse(context, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            await WriteResponse(context, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await WriteResponse(context, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled Exception");

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteResponse(context, "Internal server error");
        }
    }

    private static async Task WriteResponse(HttpContext context, string message)
    {
        context.Response.ContentType = "application/json";

        var response = ApiResponse<string>.FailResponse(message);
        var json = JsonSerializer.Serialize(response);

        await context.Response.WriteAsync(json);
    }
}