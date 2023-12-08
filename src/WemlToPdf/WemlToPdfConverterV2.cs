using System.Diagnostics;
using Egw.Api.WemlToHtml;
using Egw.PubManagement.Core.Problems;
using Egw.PubManagement.Persistence;
using Egw.PubManagement.Persistence.Entities;
using EnumsNET;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using WemlToPdf.Extensions;
using WemlToPdf.Services;
using WemlToPdf.Types;
using WhiteEstate.DocFormat;

namespace WemlToPdf;

public class WemlToPdfConverterV2 : WemlToPdf
{
    private readonly ILogger<WemlToPdfConverterV2> _logger;
    private int _sectionCnt = 1;

    public WemlToPdfConverterV2(PublicationDbContext db, ICoverFetcher coverFetcher, ILoggerFactory lf) : base(db,
        coverFetcher, lf)
    {
        _logger = lf.CreateLogger<WemlToPdfConverterV2>();
    }

    public async Task<string> CreatePdf(WemlToPdfConfig config, CancellationToken ct)
    {
        Config = config;
        CreatePath(Config.PublicationId);
        _logger.LogInformation("Path: {path}", TempDir);

        var publication = await GetPublication(Config.PublicationId, ct);
        if (publication is null)
        {
            throw new ValidationProblemDetailsException("Publication not found.");
        }

        var chapters = await GetChapters(Config.PublicationId, ct);
        if (!chapters.Any())
        {
            throw new ValidationProblemDetailsException("No chapters");
        }

        var paragraphs = await GetParagraphs(Config.PublicationId, ct);
        if (!paragraphs.Any())
        {
            throw new ValidationProblemDetailsException("No paragraphs");
        }

        foreach (var para in paragraphs)
        {
            para.Content = WemlToHtml.ToHtml(publication.Type, para.Content, WemlToHtmlFormat.Legacy);
        }

        var chaptersTree = chapters.Where(r => r.Level <= Config.MaxChapterLevelInToc)
            .ToList()
            .ToTree();

        var sections = await CreateSections(chaptersTree, publication, chapters, paragraphs, ct);

        var model = new
        {
            TocPosition = Config.TocPosition.AsString(EnumFormat.Description),
            publicationTitle = publication.Title,
            sections = sections
        };
        await TemplateSrv.RenderToFile("index.html", "base", model);

        await RenderCss();

        Console.WriteLine($"Chapters cnt: {chapters.Count}");
        Console.WriteLine($"Paragraphs cnt: {paragraphs.Count}");

        Process.Start("pagedjs-cli", $"{TempDir}/index.html -o {TempDir}/{Config.PublicationId}.pdf");

        return "";
    }

    private async Task<BookSections> CreateSections(List<PdfTreeItem> chaptersTree, Publication publication,
        List<PublicationChapter> chapters, List<ParagraphShort> paragraphs, CancellationToken ct)
    {
        var sections = new BookSections {PublicationId = Config.PublicationId};
        Config.MinHeadingLevel = paragraphs.Where(r => r.HeadingLevel > 1).Min(r => r.HeadingLevel) ?? 0;
        sections.Cover = await CreateCoverSection(ct);
        sections.Toc = await CreateTocSection(chaptersTree, ct);
        sections.TitlePage = await CreateTitlePageSection(publication, chapters, paragraphs);
        paragraphs = AggregateOriginalPages(paragraphs);
        paragraphs = CollectFootnotes(chapters, paragraphs);
        sections.Content.Add(await CreateContentSections(chapters, paragraphs));
        return sections;
    }

    private List<ParagraphShort> CollectFootnotes(List<PublicationChapter> chapters,
        List<ParagraphShort> paragraphs)
    {
        var orders = paragraphs.Select(r => r.Order).ToList();
        foreach (var chapter in chapters.Where(r => r.Level == Config.FootnotesLevel).OrderBy(r => r.Order))
        {
            var footnotes = new List<string>();
            var html = new HtmlDocument();
            ParagraphShort paraLast = new ();
            foreach (var para in paragraphs.Where(r => r.Order >= chapter.Order && r.Order <= chapter.EndOrder)
                         .OrderBy(r => r.Order))
            {
                html.LoadHtml(para.Content);
                var sups = html.DocumentNode.QuerySelectorAll("sup").ToList();
                if (sups.Any())
                {
                    foreach (var sup in sups)
                    {
                        var noteLink = sup.QuerySelector("a");
                        var noteContent = sup.QuerySelector("p");
                        if (noteLink is null || noteContent is null)
                        {
                            continue;
                        }
                        var noteMarker = noteLink.ChildNodes.First(r => r.NodeType == HtmlNodeType.Text).InnerText;
                        footnotes.Add($"<span class=\"footnote\"><a id=\"fn-{noteMarker}\" href=\"#ref-{noteMarker}\">[<b>{noteMarker}</b>]</a> {noteContent.InnerText}</span>");
                        sup.InnerHtml = $"<a id=\"ref-{noteMarker}\" href=\"#fn-{noteMarker}\">{noteMarker}</a>";
                    }

                    para.Content = html.DocumentNode.WriteContentTo();
                }

                paraLast = para;
            }

            if (footnotes.Any())
            {
                var order = GetNewOrderAfter(paraLast.Order, orders);
                var noBreak = false;
                var nextChapter = chapters
                    .Where(r => r.Order > chapter.Order)
                    .MinBy(r => r.Order);
                if (nextChapter is not null && nextChapter.Level == 2)
                {
                    noBreak = true;
                }
                paragraphs.Add(new ParagraphShort
                {
                    PublicationId = Config.PublicationId,
                    HeadingLevel = 0,
                    ParagraphId = 0,
                    ParaId = new ParaId(Config.PublicationId, 0),
                    Order = order,
                    Content = $"<hr><p class=\"footnotes {(noBreak ? "no-break" : "")}\">{string.Join("", footnotes)}</p>"
                });
            }
        }

        return paragraphs.OrderBy(r => r.Order).ToList();
    }

    private async Task<string> CreateCoverSection(CancellationToken ct)
    {
        var cover = await CoverFetcher.FetchCover(Config.PublicationId, ct);

        if (cover is not null)
        {
            var tpl = await TemplateSrv.Render("cover", new { });
            var path = Path.Join(TempDir, $"{Config.PublicationId}-cover.jpg");

            await File.WriteAllBytesAsync(path, cover, ct);
            await TemplateSrv.RenderToFile("cover.css", "css/cover.css",
                new { imagePath = $"{Config.PublicationId}-cover.jpg" });
            // await _template.RenderToFile("cover.css", "css/cover.css", new { imagePath = path });
            return tpl;
        }

        return "";
    }

    private Task<string> CreateContentSections(List<PublicationChapter> chapters, List<ParagraphShort> paragraphs)
    {
        var result = new List<string>();

        var html = new HtmlDocument();

        result.Add($"<section id=\"section-{_sectionCnt++}\">");
        foreach (var para in paragraphs)
        {
            if (Config.MinHeadingLevel != 0 && para.HeadingLevel == Config.MinHeadingLevel && result.Count > 1)
            {
                result.Add("</section>");
                result.Add($"<section id=\"section-{_sectionCnt++}\">");
            }

            if (para.HeadingLevel > 1)
            {
                html.LoadHtml(para.Content);
                var rootNode = html.DocumentNode.FirstChild;
                rootNode.Id = $"chapter-{para.ParaId}";

                if (Config.MinHeadingLevel != 0 && para.HeadingLevel == Config.MinHeadingLevel)
                {
                    rootNode.AddClass("section-header");
                    var shortener =
                        $"<p class=\"shorter\" id=\"s{para.ParaId.ElementId}\">{html.DocumentNode.InnerText}</p>";
                    result.Add(shortener);
                }

                if (para.HeadingLevel > 1 && para.HeadingLevel <= Config.MaxChapterLevelInToc)
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

        return Task.FromResult(string.Join("\n", result));
    }

    private async Task<string> CreateTitlePageSection(Publication publication, List<PublicationChapter> chapters,
        List<ParagraphShort> paragraphs)
    {
        var chapter = chapters.FirstOrDefault(r => r.Level == 1);
        if (chapter is null)
        {
            throw new ValidationProblemDetailsException("Publication header not found (h1)");
        }

        List<ParagraphShort> paras;

        if (chapter.EndOrder == chapter.ContentEndOrder)
        {
            var h1 = paragraphs.FirstOrDefault(r => r.HeadingLevel == 1);
            if (h1 is null)
            {
                throw new ValidationProblemDetailsException("Publication header not found (h1)");
            }

            paras = paragraphs
                .TakeWhile(r => r.Order <= h1.Order)
                .ToList();
        }
        else
        {
            paras = paragraphs
                .Where(r => r.Order >= chapter.Order && r.Order <= chapter.ContentEndOrder)
                .ToList();
        }

        paragraphs.RemoveAll(r => paras.Select(p => p.Order).Contains(r.Order));
        paras = AggregateOriginalPages(paras.OrderBy(r => r.Order).ToList());
        Console.WriteLine(await TemplateSrv.Render("title-page", new { publication, paragraphs = paras }));
        return await TemplateSrv.Render("title-page", new { publication, paragraphs = paras });
    }

    private List<ParagraphShort> AggregateOriginalPages(List<ParagraphShort> paragraphs)
    {
        var result = new List<ParagraphShort>();
        var html = new HtmlDocument();
        var pages = new List<string>();

        var orders = paragraphs.Select(r => r.Order).ToList();

        foreach (var para in paragraphs)
        {
            html.LoadHtml(para.Content);
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
                var order = GetNewOrderBefore(para.Order, orders);
                result.Add(new ParagraphShort
                {
                    Content = $"<p class=\"pages-block\">{string.Join("", pages)}</p>",
                    Order = order,
                    ParagraphId = 0,
                    ParaId = new ParaId(Config.PublicationId, 0),
                    PublicationId = Config.PublicationId,
                    HeadingLevel = null
                });
                inParaPages.ForEach(r => r.Remove());
                para.Content = html.DocumentNode.WriteContentTo();
                pages.Clear();
            }

            result.Add(para);
        }

        if (pages.Any())
        {
            var order = GetNewOrderBefore(paragraphs.Last().Order, orders);
            result.Add(new ParagraphShort
            {
                Content = $"<p class=\"pages-block\">{string.Join("", pages)}</p>",
                Order = order,
                ParagraphId = 0,
                ParaId = new ParaId(Config.PublicationId, 0),
                PublicationId = Config.PublicationId,
                HeadingLevel = null
            });
            Console.WriteLine("Any pages");
        }

        return result;
    }

    private async Task<string> CreateTocSection(List<PdfTreeItem> tree, CancellationToken ct)
    {
        if (tree.First().Children.Any())
        {
            return await TemplateSrv.Render("toc", new { tree = tree.First().Children, config = Config });
        }

        return "";
    }

    private async Task RenderCss()
    {
        await TemplateSrv.RenderToFile("styles.css", "css/styles.css", new { config = Config });
        await TemplateSrv.RenderToFile("page.css", "css/page.css",
            new
            {
                chapterHeadingLevel = Config.MinHeadingLevel + 1,
                pageSize = Config.PageSize.AsString(EnumFormat.Description),
                pageOrientation = Config.PageOrientation.AsString(EnumFormat.Description)
            });
        await TemplateSrv.RenderToFile("toc.css", "css/toc.css", new { });
        await TemplateSrv.RenderToFile("print.css", "css/print.css",
            new
            {
                pageSize = Config.PageSize.AsString(EnumFormat.Description),
                pageOrientation = Config.PageOrientation.AsString(EnumFormat.Description)
            });
    }
}