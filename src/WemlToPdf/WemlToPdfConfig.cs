using WemlToPdf.Types;

namespace WemlToPdf;

/// <summary>
/// Generation config
/// </summary>
public class WemlToPdfConfig
{
    public int PublicationId { get; init; }
    
    /// <summary>
    /// For reading or printing
    /// </summary>
    public PdfTargetType PrintType { get; init; }
    
    /// <summary>
    /// Toc position
    /// </summary>
    public TocPositionEnum TocPosition { get; set; } = TocPositionEnum.AfterTitle;
    /// <summary>
    /// Max chapter level for toc generation
    /// </summary>
    public int MaxChapterLevelInToc { get; set; } = 3;

    /// <summary>
    /// Page size
    /// </summary>
    public PageSizeEnum PageSize { get; set; } = PageSizeEnum.A5;

    /// <summary>
    /// Page orientation
    /// </summary>
    public PageOrientationEnum PageOrientation { get; set; } = PageOrientationEnum.Portrait;
    
    /// <summary>
    /// Min html Heading in book
    /// </summary>
    public int MinHeadingLevel { get; set; }

    public int FootnotesLevel { get; set; } = 3;
    
    /// <summary>
    /// Show chapter name on left page 
    /// </summary>
    public bool UseChapterPartTitle { get; set; } = false;
}