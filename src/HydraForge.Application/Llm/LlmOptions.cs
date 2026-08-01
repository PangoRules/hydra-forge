namespace HydraForge.Application.Llm;

public class LlmOptions
{
    public const string SectionName = "Llm";
    public string? EncryptionKey { get; set; }
    public double ContextCompressionThresholdRatio { get; set; } = 0.75;
}
