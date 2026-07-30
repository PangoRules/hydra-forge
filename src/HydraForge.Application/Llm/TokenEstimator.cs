namespace HydraForge.Application.Llm;

public static class TokenEstimator
{
    public static int EstimateTokens(string text) => text.Length / 4;
}
