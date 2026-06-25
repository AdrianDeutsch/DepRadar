using System.Text;
using DepRadar.Application.Abstractions;
using DepRadar.Application.Messaging;
using DepRadar.Application.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Chat;

/// <summary>
/// Handles <see cref="AskGraphQuestionQuery"/>: answers deterministically over the
/// assessed graph (keyless), and — when a language model is configured — replaces the
/// text with an LLM narrative grounded in the same data. The referenced packages always
/// come from the deterministic pass, so highlighting stays reliable.
/// </summary>
public sealed class AskGraphQuestionHandler(GraphAssessmentLoader loader, ILanguageModel languageModel)
    : IRequestHandler<AskGraphQuestionQuery, ChatAnswerDto?>
{
    private const string SystemPrompt =
        "You are DepRadar's dependency assistant. Answer the user's question using ONLY the " +
        "package assessment data provided. Be concise (<=80 words). If the data does not answer it, say so.";

    /// <inheritdoc />
    public async Task<ChatAnswerDto?> Handle(AskGraphQuestionQuery request, CancellationToken cancellationToken)
    {
        var assessment = await loader.LoadAsync(PackageId.Create(request.PackageId), cancellationToken);
        if (assessment is null)
        {
            return null;
        }

        var deterministic = GraphQuestionAnswerer.Answer(request.Question, assessment.Nodes);

        var llm = await languageModel.CompleteAsync(SystemPrompt, BuildPrompt(assessment, request.Question), cancellationToken);

        return new ChatAnswerDto(llm ?? deterministic.Text, deterministic.Packages, LlmUsed: llm is not null);
    }

    private static string BuildPrompt(GraphAssessment assessment, string question)
    {
        var prompt = new StringBuilder("Packages:\n");
        foreach (var node in assessment.Nodes)
        {
            var codes = node.Assessment.Findings.Count == 0 ? "ok" : string.Join(",", node.Assessment.Findings.Select(f => f.Code));
            prompt.Append('-').Append(' ')
                .Append(node.Package.Original).Append('@').Append(node.Version.ToString())
                .Append(": ").Append(node.Assessment.Score.Level).Append(" [").Append(codes).Append("]\n");
        }

        return prompt.Append("\nQuestion: ").Append(question).ToString();
    }
}
