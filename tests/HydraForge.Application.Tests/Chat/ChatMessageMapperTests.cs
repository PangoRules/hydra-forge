namespace HydraForge.Application.Tests.Chat;

using HydraForge.Application.Chat;
using HydraForge.Application.Llm;

public class ChatMessageMapperTests
{
    [Fact]
    public void ToDomainImagesJson_Null_ReturnsEmptyArray()
    {
        Assert.Equal("[]", ChatMessageMapper.ToDomainImagesJson(null));
    }

    [Fact]
    public void ToDomainImagesJson_Empty_ReturnsEmptyArray()
    {
        Assert.Equal("[]", ChatMessageMapper.ToDomainImagesJson([]));
    }

    [Fact]
    public void ToDomainImagesJson_WithBlocks_ReturnsCamelCaseJson()
    {
        var json = ChatMessageMapper.ToDomainImagesJson([
            new ImageBlock("key1", "image/png"),
            new ImageBlock("key2", "image/jpeg"),
        ]);

        Assert.Equal(
            """[{"storageKey":"key1","mediaType":"image/png"},{"storageKey":"key2","mediaType":"image/jpeg"}]""",
            json
        );
    }

    [Fact]
    public void ToApplicationImages_Null_ReturnsEmptyArray()
    {
        Assert.Empty(ChatMessageMapper.ToApplicationImages(null));
    }

    [Fact]
    public void ToApplicationImages_EmptyString_ReturnsEmptyArray()
    {
        Assert.Empty(ChatMessageMapper.ToApplicationImages(""));
    }

    [Fact]
    public void ToApplicationImages_Whitespace_ReturnsEmptyArray()
    {
        Assert.Empty(ChatMessageMapper.ToApplicationImages("   "));
    }

    [Fact]
    public void ToApplicationImages_MalformedJson_ReturnsEmptyArrayWithoutThrowing()
    {
        Assert.Empty(ChatMessageMapper.ToApplicationImages("not json"));
    }

    [Fact]
    public void ToApplicationImages_EmptyArrayJson_ReturnsEmptyArray()
    {
        Assert.Empty(ChatMessageMapper.ToApplicationImages("[]"));
    }

    [Fact]
    public void RoundTrip_PreservesStorageKeyAndMediaType()
    {
        IReadOnlyList<ImageBlock> blocks =
        [
            new ImageBlock("key1", "image/png"),
            new ImageBlock("key2", "image/jpeg"),
        ];

        var json = ChatMessageMapper.ToDomainImagesJson(blocks);
        var roundTripped = ChatMessageMapper.ToApplicationImages(json);

        Assert.Equal(blocks, roundTripped);
    }
}
