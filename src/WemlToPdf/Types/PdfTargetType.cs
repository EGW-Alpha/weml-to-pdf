using System.ComponentModel;

namespace WemlToPdf.Types;

public enum PdfTargetType
{
    [Description("one-page")]
    OnePage,
    [Description("book-printing")]
    BookPrinting,
}