namespace HydraForge.Application.Logging;

public interface IWarnLogger
{
    void LogWarning(string message);
}

public sealed class ConsoleWarnLogger : IWarnLogger
{
    public void LogWarning(string message) => Console.Error.WriteLine(message);
}

public sealed class NullWarnLogger : IWarnLogger
{
    public void LogWarning(string message) { }
}
