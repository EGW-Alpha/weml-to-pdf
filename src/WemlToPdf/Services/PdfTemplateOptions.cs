using Egw.PubManagement.Persistence.Entities;
using Fluid;
using Microsoft.Extensions.FileProviders;
using WemlToPdf.Types;

namespace WemlToPdf.Services;

internal class PdfTemplateOptions : TemplateOptions
{
    public PdfTemplateOptions()
    {
        Filters.AddFilter("decode", CustomTemplateFilters.Decode);
        Filters.AddFilter("html_to_utf", CustomTemplateFilters.HtmlToUtf);
        FileProvider = new EmbeddedFileProvider(
            typeof(WemlToPdfConverterV2).Assembly,
            "WemlToPdf.Templates"
        );

        MemberAccessStrategy.Register<BookSections>();
        MemberAccessStrategy.Register<WemlToPdfConfig>();
        MemberAccessStrategy.Register<PdfTreeItem>();
        MemberAccessStrategy.Register<PublicationChapter>();
        MemberAccessStrategy.Register<Publication>();
        MemberAccessStrategy.Register<PublicationAuthor>();
        MemberAccessStrategy.Register<ParagraphShort>();

        MemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.CamelCase;
    }
}