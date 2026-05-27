# Color Picker MCP App

A .NET 10 MCP server that lets ChatGPT display a color card inline in the chat. Built with the [MCP Apps](https://blog.modelcontextprotocol.io/posts/2026-01-26-mcp-apps/) pattern.

![Color card rendered inline in ChatGPT](images/demo.png)

## How it works

1. You ask ChatGPT: _"show me the color coral"_
2. ChatGPT calls the `show_color` MCP tool via a stable Azure API Management endpoint
3. APIM proxies the request through an ngrok tunnel to the local MCP servers
4. The tool returns `structuredContent: { "color": "coral" }` and declares a UI resource
5. ChatGPT fetches the HTML card and renders it in a sandboxed iframe inline in the chat
6. The card shows a colored swatch and label; the iframe completes the MCP Apps handshake (`ui/initialize` → `ui/notifications/initialized`) to receive the tool's structured output

```
ChatGPT
  │  POST https://<apim-name>.azure-api.net/mcp
  ▼
Azure APIM  (Consumption tier, passthrough)
  │  forwards to https://<ngrok-host>/mcp
  ▼
ngrok tunnel
  ▼
nginx (load balancer)
  ├─► mcp-server-1:5000
  └─► mcp-server-2:5000
```

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) with Compose
- A free [ngrok](https://ngrok.com) account and authtoken
- An [Azure](https://azure.microsoft.com/free) account with an active subscription
- A ChatGPT account — no Plus/Pro required; enable [Developer Mode](https://help.openai.com/en/articles/12584461-developer-mode-and-mcp-apps-in-chatgpt-beta) to access Connectors

## One-time setup

### 1. Configure ngrok

```bash
cp .env.example .env
# Edit .env and set NGROK_AUTHTOKEN=your_token_here
# Get your token at: https://dashboard.ngrok.com/get-started/your-authtoken
```

### 2. Build the Terraform toolchain container

All Azure and Terraform operations run inside a container — nothing needs to be installed on your machine.

```bash
docker compose build terraform
```

### 3. Log in to Azure

```bash
docker compose run --rm terraform az login --use-device-code
```

Open the printed URL in a browser, enter the code, and sign in.

### 4. Start the MCP stack and get the ngrok URL

```bash
chmod +x start.sh
./start.sh
```

Note the ngrok hostname printed (e.g. `abc-123.ngrok-free.app`) — you'll need it in the next step.

### 5. Provision Azure APIM

```bash
docker compose run --rm terraform terraform init

docker compose run --rm terraform terraform apply \
  -var="ngrok_url=<your-ngrok-host>" \
  -var="publisher_email=<your-email>" \
  -auto-approve
```

Replace `<your-ngrok-host>` with just the hostname — no `https://` prefix or `/mcp` suffix.

> First-time APIM provisioning takes **5–15 minutes**. Subsequent applies (e.g. to update the ngrok URL) complete in seconds.

The stable MCP endpoint is printed at the end:

```
mcp_endpoint = "https://apim-mcp-poc.azure-api.net/mcp"
```

## Day-to-day usage

```bash
./start.sh
```

If the ngrok URL has changed since the last run, update APIM with the command printed by `start.sh`:

```bash
docker compose run --rm terraform terraform apply \
  -var="ngrok_url=<new-ngrok-host>" \
  -var="publisher_email=<your-email>" \
  -auto-approve
```

**Useful commands:**

```bash
docker compose logs -f        # stream logs from all containers
docker compose down           # stop and remove containers
```

## ChatGPT Connector setup

> Developer Mode must be enabled first — see [Developer mode and MCP apps in ChatGPT](https://help.openai.com/en/articles/12584461-developer-mode-and-mcp-apps-in-chatgpt-beta).

1. Go to [chatgpt.com](https://chatgpt.com) → **Settings** → **Connectors** → **Create**
2. **Server URL**: `https://<apim-name>.azure-api.net/mcp` (from `terraform output mcp_endpoint`)
3. **Transport**: HTTP
4. **Authentication**: None
5. Click **Save**
6. Start a new chat and type: _"show me the color blue"_

The `show_color` tool fires and a color card appears inline in the chat.

## Trying different colors

Any CSS color name or hex value works:

- `"show me the color tomato"`
- `"display coral"`
- `"what does rebeccapurple look like"`
- `"show #ff6600"`

## Project structure

| File/Directory | Purpose |
|---|---|
| `Program.cs` | ASP.NET Core host with MCP server, stateless HTTP transport, and request logging |
| `ColorTool.cs` | `show_color` MCP tool — returns `structuredContent` and declares the UI resource |
| `ColorCardResource.cs` | MCP resource at `ui://color-card` serving the HTML widget |
| `ColorCardHtml.cs` | Inline HTML/JS card — renders the swatch and implements the MCP Apps iframe handshake |
| `Dockerfile` | Multi-stage build: .NET 10 SDK → ASP.NET 10 runtime |
| `Dockerfile.terraform` | Toolchain image: Azure CLI + Terraform (no local install required) |
| `docker-compose.yml` | MCP servers, nginx load balancer, ngrok tunnel, and terraform toolchain |
| `nginx.conf` | Round-robin load balancer across two MCP server instances |
| `start.sh` | Starts containers, waits for ngrok tunnel, prints endpoints and APIM update command |
| `terraform/` | Azure APIM infrastructure — resource group, APIM instance, API, passthrough policy |
