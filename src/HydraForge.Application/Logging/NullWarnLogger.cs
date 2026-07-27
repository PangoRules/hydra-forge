namespace HydraForge.Application.Logging;

public sealed class NullWarnLogger : IWarnLogger
{
    public void LogWarning(string message) { }
}
