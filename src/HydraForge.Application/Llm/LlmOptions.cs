namespace HydraForge.Application.Llm;

public class LlmOptions
{
    public const string SectionName = "Llm";
    public string? EncryptionKey { get; set; }
    public double ContextCompressionThresholdRatio { get; set; } = 0.75;
    public RagOptions Rag { get; set; } = new();
}

public class RagOptions
{
    public int TopK { get; set; } = 8;
}
