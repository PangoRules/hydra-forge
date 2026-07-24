using System.Collections.Concurrent;

namespace HydraForge.Tui.Services;

public class ErrorCollector
{
    private readonly List<(DateTime Timestamp, string CorrelationId, string Message)> _errors = new();

    public void Add(string correlationId, string message)
    {
        _errors.Add((DateTime.UtcNow, correlationId, message));
        if (_errors.Count > 50)
            _errors.RemoveAt(0);
    }

    public IReadOnlyList<(DateTime Timestamp, string CorrelationId, string Message)> GetErrors()
        => _errors.AsReadOnly();

    public void Dismiss(int index)
    {
        if (index >= 0 && index < _errors.Count)
            _errors.RemoveAt(index);
    }

    public int Count => _errors.Count;
}
