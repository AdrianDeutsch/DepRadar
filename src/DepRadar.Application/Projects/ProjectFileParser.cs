using System.Text.Json;
using System.Xml.Linq;

namespace DepRadar.Application.Projects;

/// <summary>
/// Extracts the direct NuGet dependencies from a project file's content. Auto-detects
/// the format: a <c>.csproj</c> (XML <c>PackageReference</c> items) or a
/// <c>packages.lock.json</c> (entries marked <c>Direct</c>). Pure and unit-tested.
/// </summary>
public static class ProjectFileParser
{
    /// <summary>
    /// Returns the versioned package references (<c>PackageReference</c> /
    /// <c>PackageVersion</c> with both <c>Include</c> and <c>Version</c>) from a
    /// <c>.csproj</c> or <c>Directory.Packages.props</c> — what the auto-fix command bumps.
    /// </summary>
    /// <exception cref="FormatException">The content is not valid project XML.</exception>
    public static IReadOnlyList<ManifestReference> ParseReferences(string content)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(content ?? string.Empty);
        }
        catch (System.Xml.XmlException exception)
        {
            throw new FormatException("Invalid project file XML.", exception);
        }

        return document.Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "PackageVersion")
            .Select(element => new ManifestReference(
                (string?)element.Attribute("Include") ?? string.Empty,
                (string?)element.Attribute("Version") ?? string.Empty))
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Id) && !string.IsNullOrWhiteSpace(reference.Version))
            .Select(reference => new ManifestReference(reference.Id.Trim(), reference.Version.Trim()))
            .ToList();
    }

    /// <summary>Returns the distinct direct package ids found in the content.</summary>
    /// <exception cref="FormatException">The content is not recognizable XML/JSON.</exception>
    public static IReadOnlyList<string> ParseDirectPackages(string content)
    {
        var trimmed = content?.TrimStart() ?? string.Empty;

        if (trimmed.StartsWith('{'))
        {
            return ParsePackagesLock(content!);
        }

        if (trimmed.StartsWith('<'))
        {
            return ParseCsproj(content!);
        }

        throw new FormatException("Unrecognized project file: expected a .csproj (XML) or packages.lock.json.");
    }

    private static List<string> ParseCsproj(string content)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(content);
        }
        catch (System.Xml.XmlException exception)
        {
            throw new FormatException("Invalid .csproj XML.", exception);
        }

        return document.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update"))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ParsePackagesLock(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("dependencies", out var dependencies))
            {
                return [];
            }

            var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var framework in dependencies.EnumerateObject())
            {
                foreach (var package in framework.Value.EnumerateObject())
                {
                    if (package.Value.TryGetProperty("type", out var type)
                        && string.Equals(type.GetString(), "Direct", StringComparison.OrdinalIgnoreCase))
                    {
                        packages.Add(package.Name);
                    }
                }
            }

            return packages.ToList();
        }
        catch (JsonException exception)
        {
            throw new FormatException("Invalid packages.lock.json.", exception);
        }
    }
}

/// <summary>A versioned package reference declared in a project/props file.</summary>
/// <param name="Id">The package id (as written in <c>Include</c>).</param>
/// <param name="Version">The declared version.</param>
public sealed record ManifestReference(string Id, string Version);
