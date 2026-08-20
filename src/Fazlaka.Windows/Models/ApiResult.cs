namespace Fazlaka.Windows.Models;

public class ApiResult<T>
{
    public bool Success { get; set; }
    public string? Timestamp { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class ApiResult
{
    public bool Success { get; set; }
    public string? Timestamp { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}
