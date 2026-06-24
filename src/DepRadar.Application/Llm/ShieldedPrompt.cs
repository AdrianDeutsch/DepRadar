namespace DepRadar.Application.Llm;

/// <summary>A system + user prompt pair with untrusted content safely delimited.</summary>
public sealed record ShieldedPrompt(string System, string User);
