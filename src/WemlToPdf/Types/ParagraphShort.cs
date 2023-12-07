using WhiteEstate.DocFormat;

namespace WemlToPdf.Types;

public class ParagraphShort
{
    public int PublicationId { get; init; }
    public ParaId ParaId { get; init; }
    public string Content { get; set; } = "";
    public int? HeadingLevel { get; init; }
    public int ParagraphId { get; init; }
    public double Order { get; set; }
}