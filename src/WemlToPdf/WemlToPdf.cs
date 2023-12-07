using Egw.Api.WemlToHtml;
using Egw.PubManagement.Persistence;
using Egw.PubManagement.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WemlToPdf.Services;
using WemlToPdf.Types;

namespace WemlToPdf;

public class WemlToPdf
{
    private readonly PublicationDbContext _db;
    protected readonly ICoverFetcher CoverFetcher;
    private readonly ILoggerFactory _lf;

    protected readonly ILogger<WemlToPdf> Logger;
    protected readonly WemlToHtmlConverter WemlToHtml;

    protected WemlToPdfConfig Config = new();

    // protected string TempDir = Path.Join(Path.GetTempPath(), "pdf-creator", Guid.NewGuid().ToString());
    protected string TempDir = Path.Join(Path.GetTempPath(), "pdf-creator", "{publicationId}");
    protected TemplateService TemplateSrv;

    public WemlToPdf(PublicationDbContext db, ICoverFetcher coverFetcher, ILoggerFactory lf)
    {
        _db = db;
        CoverFetcher = coverFetcher;
        _lf = lf;
        Logger = lf.CreateLogger<WemlToPdf>();
        WemlToHtml = new WemlToHtmlConverter();
        TemplateSrv = new TemplateService(TempDir, lf.CreateLogger<TemplateService>());
    }

    public void CreatePath(int publicationId)
    {
        TempDir = TempDir.Replace("{publicationId}", publicationId.ToString());
        TemplateSrv = new TemplateService(TempDir, _lf.CreateLogger<TemplateService>());
        Directory.CreateDirectory(TempDir);
    }

    public async Task<Publication?> GetPublication(int publicationId, CancellationToken ct)
    {
        return await _db.Publications.Include(model => model.Author).FirstOrDefaultAsync(r => r.PublicationId == publicationId, cancellationToken: ct);
    }

    public async Task<List<PublicationChapter>> GetChapters(int publicationId, CancellationToken ct)
    {
        return await _db.PublicationChapters
            .Where(r => r.PublicationId == publicationId)
            .OrderBy(r => r.Order)
            .ToListAsync(ct);
    }
    
    public async Task<List<ParagraphShort>> GetParagraphs(int publicationId, CancellationToken ct)
    {
        return await _db.Paragraphs
            .Where(r => r.PublicationId == publicationId)
            .OrderBy(r => r.Order)
            .Select(r => new ParagraphShort
            {
                PublicationId = publicationId,
                ParaId = r.ParaId,
                Order = r.Order,
                HeadingLevel = r.HeadingLevel,
                ParagraphId = r.ParagraphId,
                Content = r.Content
            }).ToListAsync(ct);
    }

    protected double GetNewOrderBefore(double currentOrder, List<double> orders)
    {
        var newOrder = currentOrder;
        while (orders.Contains(newOrder))
        {
            newOrder -= 0.1;
        }

        return newOrder;
    }
    protected double GetNewOrderAfter(double currentOrder, List<double> orders)
    {
        var newOrder = currentOrder;
        while (orders.Contains(newOrder))
        {
            newOrder += 0.1;
        }

        return newOrder;
    }
}