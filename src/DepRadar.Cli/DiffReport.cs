using System.Globalization;
using System.Text.Json;
using DepRadar.Application.Diff;

namespace DepRadar.Cli;

/// <summary>Renders an <see cref="UpgradeDiff"/> to the console (text or JSON).</summary>
internal static class DiffReport
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Writes a human-readable upgrade-impact report to stdout.</summary>
    public static void WriteText(UpgradeDiff diff)
    {
        var delta = diff.ScoreDelta.ToString("+0;-0;0", CultureInfo.InvariantCulture);

        Console.WriteLine();
        Console.WriteLine($"DepRadar — upgrade impact: {diff.Package}  {diff.FromVersion} -> {diff.ToVersion}");
        Console.WriteLine($"  health: {diff.FromScore}/100 ({diff.FromLevel}) -> {diff.ToScore}/100 ({diff.ToLevel})  ({delta})");
        Console.WriteLine();

        WriteSection("added dependencies", diff.AddedPackages);
        WriteSection("removed dependencies", diff.RemovedPackages);
        WriteSection("version changes", diff.ChangedPackages.Select(c => $"{c.Package} {c.FromVersion} -> {c.ToVersion}").ToList());
        WriteSection("new advisories", diff.NewAdvisories);
        WriteSection("cleared advisories", diff.ResolvedAdvisories);

        Console.WriteLine();
    }

    /// <summary>Writes the diff as JSON to stdout.</summary>
    public static void WriteJson(UpgradeDiff diff) =>
        Console.WriteLine(JsonSerializer.Serialize(diff, JsonOptions));

    private static void WriteSection(string title, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            Console.WriteLine($"  {title}: none");
            return;
        }

        Console.WriteLine($"  {title} ({items.Count}):");
        foreach (var item in items)
        {
            Console.WriteLine($"    - {item}");
        }
    }
}
