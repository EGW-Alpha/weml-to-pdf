using System.ComponentModel;

namespace WemlToPdf.Types;

public enum TocPositionEnum
{
    [Description("after-title")]
    AfterTitle,
    [Description("end-of-book")]
    EndOfBook,
    [Description("toc-marker")]
    TocMarker
}