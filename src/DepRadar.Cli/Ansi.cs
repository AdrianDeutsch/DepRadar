using DepRadar.Domain.Risk;

namespace DepRadar.Cli;

/// <summary>
/// Minimal ANSI coloring for the text report. Colors are disabled when stdout is
/// redirected (CI logs, pipes) or when the <c>NO_COLOR</c> convention
/// (https://no-color.org) is set — the output then stays byte-identical to before.
/// </summary>
internal static class Ansi
{
    private static readonly bool Enabled =
        !Console.IsOutputRedirected
        && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

    public static string Brand(string text) => Paint(text, "1;36");      // bold cyan

    public static string Ok(string text) => Paint(text, "1;32");         // bold green

    public static string Fail(string text) => Paint(text, "1;31");       // bold red

    public static string Warn(string text) => Paint(text, "33");         // yellow

    public static string Dim(string text) => Paint(text, "2");

    /// <summary>Colors a (pre-padded) severity label by its level.</summary>
    public static string Level(string paddedLabel, RiskLevel level) => level switch
    {
        RiskLevel.Critical => Paint(paddedLabel, "1;91"),                // bold bright red
        RiskLevel.High => Paint(paddedLabel, "31"),                      // red
        RiskLevel.Medium => Paint(paddedLabel, "33"),                    // yellow
        RiskLevel.Low => Paint(paddedLabel, "32"),                       // green
        _ => Paint(paddedLabel, "92"),                                   // bright green
    };

    private static string Paint(string text, string code) =>
        Enabled ? $"\e[{code}m{text}\e[0m" : text;
}
