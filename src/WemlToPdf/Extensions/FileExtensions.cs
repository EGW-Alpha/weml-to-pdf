using System.Reflection;
using System.Resources;
using Egw.PubManagement.Persistence.Entities;
using WemlToPdf.Types;

namespace WemlToPdf.Extensions;

/// <summary>
/// File worker
/// </summary>
internal static class FileExtensions
{
    /// <summary> Reads a text resource from assembly </summary>
    public static async Task<string?> ReadTextResource(this Assembly assembly, string name, CancellationToken ct)
    {
        string fullName = $"{assembly.GetName().Name!}.Templates.{name}";
        await using Stream stream = assembly.GetManifestResourceStream(fullName) ??
                                    throw new MissingManifestResourceException($"Resource {fullName} not found");
        return await new StreamReader(stream).ReadToEndAsync(ct);
    }

    /// <summary> Reads a text resource from assembly </summary>
    public static bool ResourceExists(this Assembly assembly, string name)
    {
        string fullName = $"{assembly.GetName().Name!}.Templates.{name}";
        using Stream? stream = assembly.GetManifestResourceStream(fullName);
        return stream != null;
    }

    public static List<PdfTreeItem> ToTree(this List<PublicationChapter> chapters)
    {
        if (!chapters.Any() && chapters.Count <= 1)
        {
            return new List<PdfTreeItem>();
        }

        var result = new List<PdfTreeItem>();
        var minLevel = chapters.Min(r => r.Level);
        var roots = chapters.Where(r => r.Level == minLevel).ToList();

        foreach (PublicationChapter root in roots)
        {
            int rootIndex = chapters.IndexOf(root);
            var rootChildren = chapters.Skip(rootIndex + 1).TakeWhile(r => r.Level > root.Level).ToList();
            result.Add(new PdfTreeItem { Chapter = root, ElementId = root.ParaId.ElementId, Children = ToTree(rootChildren) });
        }

        return result;
    }
}