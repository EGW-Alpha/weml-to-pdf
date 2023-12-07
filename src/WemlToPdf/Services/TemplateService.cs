using System.Text.RegularExpressions;
using Fluid;
using Microsoft.Extensions.Logging;

namespace WemlToPdf.Services;

/// <summary>
/// Template worker
/// </summary>
public partial class TemplateService
{
    /// <summary>
    /// Language of the epub file
    /// </summary>
    public string Language { get; set; } = "en-us";

    /// <summary>
    /// Direction of the epub file
    /// </summary>
    public string LangDir { get; set; } = "ltr";

    private readonly string _basePath;
    private readonly ILogger<TemplateService> _logger;

    /// <summary> Default constructor </summary>
    public TemplateService(string basePath, ILogger<TemplateService> logger)
    {
        _basePath = basePath;
        _logger = logger;
    }

    /// <summary> Render template to file </summary>
    public async Task RenderToFile(string fileName, string templateName, object model)
    {
        string text = await Render(templateName, model);
        await File.WriteAllTextAsync(Path.Combine(_basePath, fileName), text);
    }

    /// <summary> Render template </summary>
    public async Task<string> Render(string templateName, object model)
    {
        var tplParser = new FluidParser();
        string? content = "";

        string template = $"{{% render \"{templateName}\" %}}";

        if (tplParser.TryParse(template, out IFluidTemplate? tpl, out string? error))
        {
            var tplOptions = new PdfTemplateOptions();
            var context = new TemplateContext(model, tplOptions);
            // context.SetValue("language", Language);
            // context.SetValue("langDir", LangDir);
            content = await tpl.RenderAsync(context);
        }
        else
        {
            _logger.LogError("Template error: {Error}", error);
        }

        return SpaceRegex().Replace(content, string.Empty);
    }


    [GeneratedRegex(@"^\s+$[\r\n]*", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex SpaceRegex();
}