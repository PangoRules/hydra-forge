using HydraForge.Domain.Common;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Llm;

public interface IImageClient
{
    AdapterType AdapterType { get; }

    Task<Result<GeneratedImage>> GenerateImageAsync(ImageRequest request, CancellationToken ct = default);

    Task<Result<GeneratedImage>> InpaintAsync(InpaintRequest request, CancellationToken ct = default);
}
