using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ColorPickerApp;

[McpServerToolType]
public static class ColorTool
{
    [McpServerTool(Name = "show_color", UseStructuredContent = true)]
    [Description("Display a color card in the chat with a colored square and the color name")]
    [McpMeta("ui", "", JsonValue = """{"resourceUri":"ui://color-card"}""")]
    public static CallToolResult ShowColor(
        [Description("A CSS color name (e.g. 'blue', 'coral', 'navy') or hex value (e.g. '#ff6600')")] string color)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = $"Showing color: {color}" }],
            StructuredContent = JsonSerializer.SerializeToElement(new { color })
        };
    }
}
