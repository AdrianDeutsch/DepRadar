using DepRadar.Application.Messaging;

namespace DepRadar.Application.Chat;

/// <summary>Request body for the chat endpoint.</summary>
/// <param name="Question">The natural-language question.</param>
public sealed record ChatRequest(string Question);

/// <summary>API-facing chat answer with the packages it refers to.</summary>
public sealed record ChatAnswerDto(string Answer, IReadOnlyList<string> Packages, bool LlmUsed);

/// <summary>
/// Query: answer a natural-language question about a scanned package's graph. Returns
/// <see langword="null"/> if the package has not been scanned.
/// </summary>
/// <param name="PackageId">The root package id.</param>
/// <param name="Question">The natural-language question.</param>
public sealed record AskGraphQuestionQuery(string PackageId, string Question) : IRequest<ChatAnswerDto?>;
