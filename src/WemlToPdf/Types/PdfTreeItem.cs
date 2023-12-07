
using Egw.PubManagement.Persistence.Entities;

namespace WemlToPdf.Types;

public class PdfTreeItem
{
    public required PublicationChapter Chapter { get; init; }
    public int ElementId { get; init; }
    public List<Paragraph> Paragraphs { get; init; } = new();
    public List<PdfTreeItem> Children { get; init; } = new();
}