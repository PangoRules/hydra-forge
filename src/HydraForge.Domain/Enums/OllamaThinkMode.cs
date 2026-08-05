namespace HydraForge.Domain.Enums;

/// <summary>
/// Per-model override for whether Ollama-routed calls request thinking/reasoning mode.
/// Thinking-capable Ollama models put reasoning in a separate response field from the
/// actual answer — a model that always thinks by default can burn its entire token
/// budget on reasoning and return empty content for short tasks like chat titles
/// (confirmed live with a local "gemma4:26b"). <see cref="Auto"/> uses the existing
/// heuristic (think only if the caller explicitly requested a reasoning effort);
/// <see cref="On"/>/<see cref="Off"/> force it either way, for admins who've found a
/// specific model needs — or breaks under — the default.
/// </summary>
public enum OllamaThinkMode
{
    Auto = 0,
    On = 1,
    Off = 2,
}
