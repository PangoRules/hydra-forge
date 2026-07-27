namespace HydraForge.Application.Logging;

public sealed class ConsoleWarnLogger : IWarnLogger
{
    public void LogWarning(string message) => Console.Error.WriteLine(message);
}
