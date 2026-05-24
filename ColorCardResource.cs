using System.ComponentModel;
using ModelContextProtocol.Server;

namespace ColorPickerApp;

[McpServerResourceType]
public static class ColorCardResource
{
    [McpServerResource(
        UriTemplate = "ui://color-card",
        Name = "Color Card Widget",
        MimeType = "text/html;profile=mcp-app")]
    [Description("Renders a colored card with a square swatch and the color name")]
    public static string GetColorCard() => ColorCardHtml.Content;
}
