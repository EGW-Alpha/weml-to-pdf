using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Text.Json;
using Egw.Api.WemlToHtml;
using Egw.PubManagement.Core.Problems;
using Egw.PubManagement.Persistence;
using Egw.PubManagement.Persistence.Entities;
using EnumsNET;
using Fizzler.Systems.HtmlAgilityPack;
using Hellang.Middleware.ProblemDetails;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WemlToPdf.Extensions;
using WemlToPdf.Services;
using WemlToPdf.Types;
using WhiteEstate.DocFormat;
using WhiteEstate.DocFormat.Enums;

namespace WemlToPdf;

public class WemlToPdfConverter
{
    private readonly WemlToPdfConfig _config = new();
    private readonly PublicationDbContext _db;
    private TemplateService _template;
    private readonly ICoverFetcher _coverFetcher;
    private readonly ILoggerFactory _lf;
    private readonly WemlToHtmlConverter _wemlToHtml;

    private readonly ILogger<WemlToPdfConverter> _logger;

    // private string _tempDir = Path.Join(Path.GetTempPath(), "pdf-creator", Guid.NewGuid().ToString());
    private string _tempDir = "res/";
    private int _sectionCnt = 1;

    public WemlToPdfConverter(PublicationDbContext db, ICoverFetcher coverFetcher, ILoggerFactory lf)
    {
        _db = db;
        _coverFetcher = coverFetcher;
        _lf = lf;
        _logger = lf.CreateLogger<WemlToPdfConverter>();
        _wemlToHtml = new WemlToHtmlConverter();
    }

    public async Task<bool> Create(int publicationId, CancellationToken ct)
    {
        _tempDir = Path.Join(_tempDir, publicationId.ToString());
        _template = new TemplateService(_tempDir, _lf.CreateLogger<TemplateService>());
        Directory.CreateDirectory(_tempDir);
        _logger.LogInformation("Temporary path: {Path}", _tempDir);

        Publication? publication = await _db.Publications
            .FirstOrDefaultAsync(r => r.PublicationId == publicationId, ct);
        if (publication is null)
        {
            throw new ValidationException($"Publication {publicationId} not found.");
        }

        List<PublicationChapter> chapters = await _db.PublicationChapters.Where(r => r.PublicationId == publicationId)
            .OrderBy(r => r.Order)
            .ToListAsync(ct);

        List<Paragraph> paragraphs = await _db.Paragraphs.Where(r => r.PublicationId == publicationId)
            .OrderBy(r => r.Order)
            .ToListAsync(ct);

        if (!paragraphs.Any())
        {
            throw new ValidationException($"Publication {publicationId} does not contain paragraphs");
        }

        paragraphs = CollectOriginalPagesV2(publication.Type, paragraphs);
        CollectFootnotes(chapters, paragraphs);
        

        var tree = chapters.Where(r => r.Level <= _config.MaxChapterLevelInToc)
            .ToList().ToTree();

        BookSections content = await Prepare(tree, publication, chapters, paragraphs, ct);

        var model = new
        {
            TocPosition = _config.TocPosition.AsString(EnumFormat.Description), publicationTitle = publication.Title,
            content
        };
        await _template.RenderToFile("index.html", "base", model);

        await RenderCss();

        Console.WriteLine($"Chapters cnt: {chapters.Count}");
        Console.WriteLine($"Paragraphs cnt: {paragraphs.Count}");

        System.Diagnostics.Process.Start("pagedjs-cli", $"{_tempDir}/index.html -o {_tempDir}/1.pdf");

        return true;
    }

    private List<Paragraph> CollectOriginalPagesV2(PublicationType publicationType, List<Paragraph> paragraphs)
    {
        var result = new List<Paragraph>();
        var html = new HtmlDocument();
        var pages = new List<string>();

        for (var i = 0; i < paragraphs.Count; i++)
        {
            var content = _wemlToHtml.ToHtml(publicationType, paragraphs[i].Content, WemlToHtmlFormat.Legacy);
            paragraphs[i].Content = content;
            html.LoadHtml(content);
            var firstChild = html.DocumentNode.FirstChild;
            if (firstChild.Name.ToLower() == "span" && firstChild.HasClass("pagenumber"))
            {
                pages.Add(firstChild.OuterHtml);
                continue;
            }

            var inParaPages = html.DocumentNode.QuerySelectorAll("span.pagenumber").ToList();
            pages.AddRange(inParaPages.Select(r => r.OuterHtml));
            if (pages.Any())
            {
                if (paragraphs[i - 1].HeadingLevel is null)
                {
                    paragraphs[i - 1].Content = $"<p class=\"pages-block\">{string.Join("", pages)}</p>";
                    result.Add(paragraphs[i - 1]);
                }
                else
                {
                    inParaPages.ForEach(r => r.Remove());
                    paragraphs[i].Content =
                        $"<p class=\"pages-block\">{string.Join("", pages)}</p>{html.DocumentNode.WriteContentTo()}";
                }

                pages.Clear();
            }

            result.Add(paragraphs[i]);
        }

        if (pages.Any())
        {
            Console.WriteLine($"Pages at end found: {pages.Count}");
        }

        return result;
    }

    private List<Paragraph> CollectOriginalPages(PublicationType publicationType, List<Paragraph> paragraphs)
    {
        var result = new List<Paragraph>();
        var html = new HtmlDocument();
        var pagesBlock = "<p class=\"pages-block\">";
        var isFirst = true;
        Paragraph? forReplace = null;
        foreach (var para in paragraphs)
        {
            para.Content = _wemlToHtml.ToHtml(publicationType, para.Content, WemlToHtmlFormat.Legacy);
            html.LoadHtml(para.Content);
            var fChild = html.DocumentNode.FirstChild;
            if (fChild.Name.ToLower() == "span" && fChild.HasClass("pagenumber"))
            {
                if (isFirst)
                {
                    isFirst = false;
                    pagesBlock += para.Content;
                    forReplace = para;
                }
                else
                {
                    pagesBlock += para.Content;
                }

                continue;
            }

            if (forReplace is not null)
            {
                pagesBlock += "</p>";
                forReplace.Content = pagesBlock;
                pagesBlock = "<p class=\"pages-block\">";
                result.Add(forReplace);
                forReplace = null;
            }

            var pagesSpan = html.DocumentNode.QuerySelectorAll("span.pagenumber").ToList();
            if (pagesSpan.Any())
            {
                var str = $"<p class=\"pages-block\">{string.Join("", pagesSpan.Select(r => r.OuterHtml))}</p>";
                pagesSpan.ForEach(r => r.Remove());
                para.Content = $"{str}{html.DocumentNode.WriteContentTo()}";
            }

            result.Add(para);
            isFirst = true;
        }

        // Console.WriteLine(JsonSerializer.Serialize(result.Take(30), new JsonSerializerOptions {WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
        return result;
    }

    private async Task<BookSections> Prepare(List<PdfTreeItem> tree, Publication publication,
        List<PublicationChapter> chapters,
        List<Paragraph> paragraphs, CancellationToken ct)
    {
        var result = new BookSections { PublicationId = publication.PublicationId };

        var titleChapter = chapters.FirstOrDefault(r => r.Level == 1);
        if (titleChapter is null)
        {
            throw new Exception("(chapters) H1 not found");
        }

        var isSingleChapterBook = !chapters.Any(c => c.Level > 1);

        result.Cover = await DoCoverSection(publication, ct); // +
        if (tree.First().Children.Any())
        {
            result.Toc = await DoTocSection(tree);
        }

        result.TitlePage = DoTitleSection(
            paragraphs.Where(r => r.Order >= titleChapter.Order && r.Order <= titleChapter.ContentEndOrder)
                .OrderBy(r => r.Order));

        List<Paragraph> paras;
        if (isSingleChapterBook)
        {
            var h1Order = paragraphs.FirstOrDefault(p => p.HeadingLevel == 1)?.Order;
            if (h1Order is null)
            {
                throw new ValidationException("(paragraphs) H1 not found");
            }

            paras = paragraphs.Where(r => r.Order > h1Order).OrderBy(r => r.Order).ToList();
        }
        else
        {
            paras = paragraphs.Where(r => r.Order > titleChapter.ContentEndOrder).OrderBy(r => r.Order).ToList();
        }

        result.Content.Add(DoContentSections(paras));

        return result;
    }

    private string DoTitleSection(IOrderedEnumerable<Paragraph> paras)
    {
        var result = new List<string>();
        result.Add($"<section id=\"section-titlepage\">");

        foreach (var para in paras)
        {
            if (para.Content.StartsWith("<span"))
            {
                para.Content = $"<p>{para.Content}</p>";
            }

            result.Add(para.Content);
            if (para.HeadingLevel == 1)
            {
                break;
            }
        }

        result.Add("</section>");
        return string.Join("\n", result);
    }

    private async Task<string> DoTocSection(List<PdfTreeItem> tree)
    {
        if (!tree.Any())
        {
            _logger.LogWarning("TOC empty");
            return "";
        }

        return await _template.Render("toc", new { tree = tree.First().Children });
    }

    private async Task<string> DoCoverSection(Publication publication, CancellationToken ct)
    {
        // <h1>{publication.Title}</h1>

        var tpl = $"""
                   <section id="cover" class="cover-section">
                   </section>
                   """;
        var cover = await _coverFetcher.FetchCover(publication.PublicationId, ct);

        if (cover is not null)
        {
            var path = Path.Join(_tempDir, $"{publication.PublicationId}-cover.jpg");

            await File.WriteAllBytesAsync(path, cover, ct);
            await _template.RenderToFile("cover.css", "css/cover.css",
                new { imagePath = $"{publication.PublicationId}-cover.jpg" });
            // await _template.RenderToFile("cover.css", "css/cover.css", new { imagePath = path });
            return tpl;
        }

        return "";
    }

    private string DoContentSections(List<Paragraph> paras)
    {
        var result = new List<string>();

        var html = new HtmlDocument();

        _config.MinHeadingLevel = paras.Where(r => r.HeadingLevel > 1).Min(r => r.HeadingLevel) ?? 0;

        result.Add($"<section id=\"section-{_sectionCnt++}\">");
        foreach (var para in paras)
        {
            if (_config.MinHeadingLevel != 0 && para.HeadingLevel == _config.MinHeadingLevel)
            {
                result.Add("</section>");
                result.Add($"<section id=\"section-{_sectionCnt++}\">");
            }

            if (para.HeadingLevel > 1)
            {
                html.LoadHtml(para.Content);
                var rootNode = html.DocumentNode.FirstChild;
                rootNode.Id = $"chapter-{para.ParaId}";

                if (_config.MinHeadingLevel != 0 && para.HeadingLevel == _config.MinHeadingLevel)
                {
                    rootNode.AddClass("section-header");
                    var shortener =
                        $"<p class=\"shorter\" id=\"s{para.ParaId.ElementId}\">{html.DocumentNode.InnerText}</p>";
                    result.Add(shortener);
                }

                if (para.HeadingLevel > 1 && para.HeadingLevel <= _config.MaxChapterLevelInToc)
                {
                    rootNode.AddClass("chapter-header");
                    var tocLine =
                        $"<p class=\"toc-line\" id=\"t{para.ParaId.ElementId}\">{html.DocumentNode.InnerText}</p>";
                    result.Add(tocLine);
                }

                result.Add(html.DocumentNode.WriteContentTo());
            }
            else
            {
                result.Add(para.Content);
            }
        }

        result.Add("</section>");

        return string.Join("\n", result);
    }

    private async Task RenderCss()
    {
        await _template.RenderToFile("styles.css", "css/styles.css", new { });
        await _template.RenderToFile("page.css", "css/page.css",
            new
            {
                chapterHeadingLevel = _config.MinHeadingLevel + 1,
                pageSize = _config.PageSize.AsString(EnumFormat.Description),
                pageOrientation = _config.PageOrientation.AsString(EnumFormat.Description)
            });
        await _template.RenderToFile("toc.css", "css/toc.css", new { });
        await _template.RenderToFile("print.css", "css/print.css",
            new
            {
                pageSize = _config.PageSize.AsString(EnumFormat.Description),
                pageOrientation = _config.PageOrientation.AsString(EnumFormat.Description)
            });
    }

    private void CollectFootnotes(List<PublicationChapter> chapters, List<Paragraph> paragraphs)
    {
        var p = paragraphs.FirstOrDefault(r => r.HeadingLevel == _config.FootnotesLevel);
        if (p is null)
        {
            throw new NotFoundProblemDetailsException("Footnote heading level not found");
        }

        Console.WriteLine(JsonSerializer.Serialize(p));
        
        var chapterLevel = chapters.FirstOrDefault(r => r.ParaId == p.ParaId)?.Level;
        
        if (chapterLevel is null)
        {
            throw new NotFoundProblemDetailsException("Footnote heading level not found");
        }

        var requiredChapters = chapters
            .Where(r => r.Level == chapterLevel)
            .OrderBy(r => r.Order)
            .ToList();

        var footnotes = new List<string>();
        var html = new HtmlDocument();
        foreach (var chapter in requiredChapters)
        {
            var paras = paragraphs
                .Where(r => r.Order >= chapter.Order && r.Order <= chapter.EndOrder)
                .OrderBy(r => r.Order)
                .ToList();
            foreach (var para in paras)
            {
                html.LoadHtml(para.Content);
                var sups = html.DocumentNode.QuerySelectorAll("sup").ToList();
                if (sups.Any())
                {
                    foreach (var sup in sups)
                    {
                        var noteLink = sup.QuerySelector("a");
                        var noteContent = sup.QuerySelector("p");
                        var noteMarker = noteLink.ChildNodes.First(r => r.NodeType == HtmlNodeType.Text).InnerText;
                        noteLink.SetAttributeValue("href", $"#fn-{noteMarker}");
                        noteLink.Id = $"ref-{noteMarker}";
                        footnotes.Add($"<a id=\"fn-{noteMarker}\" href=\"#ref-{noteMarker}\">[{noteMarker}]</a> {noteContent.InnerText}");
                        // sup.InnerHtml = noteLink.WriteContentTo();
                    }
                    // footnotes.AddRange(sups.Select(r => $"<note>{r.InnerText}</note>"));
                }
            }

            if (footnotes.Any())
            {
                paras.Last().Content += $"<div class=\"footnotes\">{string.Join("<br/>", footnotes)}</div>";
                footnotes.Clear();
            }
        }
    }

    private static IEnumerable<int> GetIntRange(int start, int end)
    {
        if (start < end)
        {
            return Enumerable.Range(start, end - start + 1);
        }

        if (start > end)
        {
            return Enumerable.Range(end, start - end + 1);
        }

        return new[] { start };
    }
}