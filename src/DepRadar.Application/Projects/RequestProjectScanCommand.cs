using DepRadar.Application.Messaging;

namespace DepRadar.Application.Projects;

/// <summary>
/// Command: parse a project file (<c>.csproj</c> or <c>packages.lock.json</c>) and
/// queue a transitive scan for each direct dependency.
/// </summary>
/// <param name="Content">The raw project-file content.</param>
public sealed record RequestProjectScanCommand(string Content) : IRequest<ProjectScanDto>;
