namespace WemlToPdf.Types;

public class BookSections
{
    public int PublicationId { get; set; }
    public string Cover { get; set; } = "";
    public string TitlePage { get; set; } = "";
    public string Toc { get; set; } = "";
    public List<string> Content { get; set; } = new List<string>();
}