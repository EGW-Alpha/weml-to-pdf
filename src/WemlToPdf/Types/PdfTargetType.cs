using System.ComponentModel;

namespace WemlToPdf.Types;

public enum PdfTargetType
{
    [Description("same")]
    Same,
    [Description("book-printing")]
    BookPrinting,
}