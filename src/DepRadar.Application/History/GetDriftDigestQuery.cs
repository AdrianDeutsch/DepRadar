using DepRadar.Application.Messaging;

namespace DepRadar.Application.History;

/// <summary>
/// Query: a Markdown drift digest across every tracked package (those with at least
/// two scans). Always returns Markdown — an empty digest when nothing has drifted.
/// </summary>
public sealed record GetDriftDigestQuery : IRequest<string>;
