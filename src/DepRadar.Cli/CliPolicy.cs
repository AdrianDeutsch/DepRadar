using DepRadar.Application.Policy;

namespace DepRadar.Cli;

/// <summary>
/// Resolves the policy gate for every scan verb the same way: an explicit
/// <c>--policy</c> file, else an auto-detected <c>./depradar.json</c>, else the
/// flag-built fallback. A committed policy file governs ALL ecosystems — not just
/// the NuGet <c>scan</c>.
/// </summary>
internal static class CliPolicy
{
    /// <summary>Resolves the effective policy; an explicit but unreadable file is a usage error.</summary>
    public static bool TryResolve(string? policyPath, RiskPolicy fallback, out RiskPolicy? policy, out string? error)
    {
        error = null;
        var path = policyPath ?? (File.Exists("depradar.json") ? "depradar.json" : null);
        if (path is null)
        {
            policy = fallback;
            return true;
        }

        try
        {
            policy = PolicyFile.Parse(File.ReadAllText(path));
            return true;
        }
        catch (Exception exception) when (exception is FormatException or IOException)
        {
            policy = null;
            error = $"Could not read policy '{path}': {exception.Message}";
            return false;
        }
    }
}
