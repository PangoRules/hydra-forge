namespace HydraForge.Domain.Common;

public sealed class Error
{
    public Error(string code, string message)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Error code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Error message is required.", nameof(message));
        Code = code;
        Message = message;
    }

    public string Code { get; }

    public string Message { get; }
}