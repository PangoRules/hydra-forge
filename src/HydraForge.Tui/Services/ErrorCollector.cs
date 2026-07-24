using System.Collections.Concurrent;

namespace HydraForge.Tui.Services;

public class ErrorCollector
{
    private readonly ConcurrentBag<string> _errors = new();

    public IReadOnlyCollection<string> Errors => _errors.ToArray();

    public void AddError(string error)
    {
        _errors.Add(error);
    }
}