using System.Text;
using System.Web;
using Fluid;
using Fluid.Values;

namespace WemlToPdf;

/// <summary>
/// Custom template filters
/// </summary>
public static class CustomTemplateFilters
{
    /// <summary> Decode html entities </summary>
    public static ValueTask<FluidValue> Decode(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        return new StringValue(HttpUtility.HtmlDecode(input.ToStringValue()).Replace("&", "&#38;"));
    }

    /// <summary> Convert html to utf </summary>
    public static ValueTask<FluidValue> HtmlToUtf(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        var builder = new StringBuilder();
        foreach (char c in input.ToStringValue())
        {
            if (c > 127)
            {
                builder.Append("&#");
                builder.Append((int)c);
                builder.Append(';');
            }
            else
            {
                builder.Append(c);
            }
        }

        return new StringValue(builder.ToString().Replace("&", "&#38;"));
    }
}