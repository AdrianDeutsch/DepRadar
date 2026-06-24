using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DepRadar.Application.Abstractions;

namespace DepRadar.Infrastructure.Ai;

/// <summary>Model selection for <see cref="AnthropicLanguageModel"/>.</summary>
internal sealed record AnthropicOptions(string Model);

/// <summary>
/// <see cref="ILanguageModel"/> over Anthropic's Messages API (Claude), activated only
/// when an API key is configured. The untrusted changelog content reaches this model
/// already wrapped by <c>PromptShield</c>, so prompt-injection defenses are in place.
/// </summary>
internal sealed class AnthropicLanguageModel(HttpClient httpClient, AnthropicOptions options) : ILanguageModel
{
    private const int MaxTokens = 512;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var request = new AnthropicRequest(
            options.Model,
            MaxTokens,
            systemPrompt,
            [new AnthropicMessage("user", userPrompt)]);

        using var response = await httpClient.PostAsJsonAsync("v1/messages", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AnthropicResponse>(JsonOptions, cancellationToken);
        return payload?.Content?.FirstOrDefault(block => block.Type == "text")?.Text?.Trim();
    }

    private sealed record AnthropicRequest(
        string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        string System,
        IReadOnlyList<AnthropicMessage> Messages);

    private sealed record AnthropicMessage(string Role, string Content);

    private sealed record AnthropicResponse(IReadOnlyList<AnthropicContentBlock>? Content);

    private sealed record AnthropicContentBlock(string? Type, string? Text);
}
