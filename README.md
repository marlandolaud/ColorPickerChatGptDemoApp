# Color Picker MCP App

A .NET 10 MCP server that lets ChatGPT display a color card inline in the chat. Built with the [MCP Apps](https://blog.modelcontextprotocol.io/posts/2026-01-26-mcp-apps/) pattern.

## How it works

1. You ask ChatGPT: _"show me the color coral"_
2. ChatGPT calls the `show_color` MCP tool
3. The tool returns `structuredContent: { "color": "coral" }` and declares a UI resource
4. ChatGPT fetches the HTML card from the MCP server and renders it in a sandboxed iframe inline in the chat
5. The card shows a coral-colored square with the label "coral"

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [ngrok](https://ngrok.com) with a free account and authtoken configured
- ChatGPT Plus/Pro account (required for Connectors)

## ngrok setup (one-time)

```bash
# Install ngrok
curl -sSL https://ngrok-agent.s3.amazonaws.com/ngrok.asc \
  | sudo tee /etc/apt/trusted.gpg.d/ngrok.asc > /dev/null
echo "deb https://ngrok-agent.s3.amazonaws.com buster main" \
  | sudo tee /etc/apt/sources.list.d/ngrok.list
sudo apt update && sudo apt install ngrok

# Authenticate with your authtoken from https://dashboard.ngrok.com
ngrok config add-authtoken YOUR_TOKEN_HERE
```

## Run

```bash
chmod +x start.sh
./start.sh
```

The script starts the MCP server and ngrok, then prints the public MCP endpoint URL.

## ChatGPT Connector setup

1. Go to [chatgpt.com](https://chatgpt.com) → **Settings** → **Connectors** → **Create**
2. **Server URL**: the `MCP ENDPOINT` URL printed by `start.sh`
3. **Transport**: HTTP
4. **Authentication**: None
5. Click **Save**
6. Start a new chat and type: _"show me the color blue"_

The `show_color` tool will fire and a color card will appear inline in the chat.

## Trying different colors

Any CSS color name or hex value works:

- `"show me the color tomato"`
- `"display coral"`
- `"what does rebeccapurple look like"`
- `"show #ff6600"`
