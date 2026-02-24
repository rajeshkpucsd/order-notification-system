namespace OrderService.Responses;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T? Data { get; set; }
    public object? Errors { get; set; }

    public static ApiResponse<T> SuccessResponse(T data, string message = "")
        => new ApiResponse<T> { Success = true, Data = data, Message = message };

    public static ApiResponse<T> FailResponse(string message, object? errors = null)
        => new ApiResponse<T> { Success = false, Message = message, Errors = errors };
}