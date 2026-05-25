using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
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
    public static TextResourceContents GetColorCard() => new()
    {
        Uri = "ui://color-card",
        MimeType = "text/html;profile=mcp-app",
        Text = ColorCardHtml.Content,
        Meta = new JsonObject
        {
            ["ui"] = new JsonObject
            {
                ["csp"] = new JsonObject
                {
                    ["connectDomains"] = new JsonArray(),
                    ["resourceDomains"] = new JsonArray()
                }
            }
        }
    };
}
