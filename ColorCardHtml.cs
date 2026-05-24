namespace ColorPickerApp;

public static class ColorCardHtml
{
    public const string Content = """
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="utf-8"/>
          <style>
            * { box-sizing: border-box; margin: 0; padding: 0; }
            body {
              display: flex;
              justify-content: center;
              align-items: center;
              min-height: 100vh;
              background: #f3f4f6;
              font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
            }
            .card {
              background: white;
              border-radius: 16px;
              box-shadow: 0 4px 24px rgba(0,0,0,0.10);
              padding: 24px;
              width: 240px;
              text-align: center;
            }
            .swatch {
              width: 190px;
              height: 190px;
              border-radius: 12px;
              margin: 0 auto 16px;
              border: 1px solid rgba(0,0,0,0.08);
              background: white;
              transition: background-color 0.3s ease;
            }
            .label {
              font-size: 18px;
              font-weight: 600;
              color: #111827;
              text-transform: capitalize;
            }
            #debug-log {
              position: fixed;
              bottom: 0; left: 0; right: 0;
              max-height: 40vh;
              overflow-y: auto;
              background: rgba(0,0,0,0.85);
              color: #0f0;
              font: 10px/1.4 monospace;
              padding: 6px 8px;
              z-index: 9999;
              white-space: pre-wrap;
              word-break: break-all;
            }
          </style>
        </head>
        <body>
          <div class="card">
            <div class="swatch" id="swatch"></div>
            <div class="label" id="label">—</div>
          </div>
          <div id="debug-log"></div>
          <script>
            const dbg = document.getElementById('debug-log');
            function log(msg, obj) {
              const ts = new Date().toISOString().slice(11, 23);
              const text = obj !== undefined ? msg + ' ' + JSON.stringify(obj, null, 2) : msg;
              console.log('[ColorCard ' + ts + '] ' + text);
              const line = document.createElement('div');
              line.textContent = ts + ' ' + text;
              dbg.insertBefore(line, dbg.firstChild);
            }

            let hostMessageReceived = false;

            function applyColor(data) {
              const c = data?.params?.structuredContent
                     ?? data?.params?.input?.structuredContent
                     ?? data?.result?.structuredContent
                     ?? data?.structuredContent;
              if (c?.color) {
                log('applying color: ' + c.color);
                document.getElementById('swatch').style.backgroundColor = c.color;
                document.getElementById('label').textContent = c.color;
              } else {
                log('no color found – full payload logged above');
              }
            }

            window.addEventListener('message', (event) => {
              hostMessageReceived = true;
              log('message received from origin=' + event.origin, event.data);
              const d = event.data;
              if (d?.method) log('  method: ' + d.method);
              if (d?.result !== undefined) log('  result:', d.result);
              if (d?.error  !== undefined) log('  error:',  d.error);

              // If host replied to ui/initialize, also send the initialized notification
              if (d?.id === 1 && d?.result !== undefined) {
                log('host responded to ui/initialize — sending ui/notifications/initialized');
                window.parent.postMessage({ jsonrpc: '2.0', method: 'ui/notifications/initialized' }, '*');
              }

              // Collect tool data from any known method or structuredContent path
              if (d?.method === 'ui/notifications/tool-result' || d?.method === 'ui/input' || d?.method === 'ui/toolResult' || d?.method === 'ui/data') {
                applyColor(d);
              } else if (d?.params?.structuredContent || d?.result?.structuredContent || d?.structuredContent) {
                applyColor(d);
              }
            });

            const initMsg = {
              jsonrpc: '2.0',
              id: 1,
              method: 'ui/initialize',
              params: {
                protocolVersion: '2026-01-26',
                clientInfo: { name: 'color-card', version: '1.0.0' },
                appInfo: { name: 'color-card', version: '1.0.0' },
                appCapabilities: { availableDisplayModes: ['inline', 'fullscreen'] }
              }
            };

            // Send ui/initialize and ui/notifications/initialized immediately,
            // then retry every 300 ms until the host sends us any message.
            function sendHandshake() {
              log('sending ui/initialize' + (hostMessageReceived ? ' (host already replied, retry skipped)' : ''));
              window.parent.postMessage(initMsg, '*');
              window.parent.postMessage({ jsonrpc: '2.0', method: 'ui/notifications/initialized' }, '*');
            }

            log('page loaded — starting handshake');
            sendHandshake();
            const retryInterval = setInterval(() => {
              if (hostMessageReceived) {
                clearInterval(retryInterval);
                log('host acknowledged — stopped handshake retries');
              } else {
                log('no reply yet — retrying handshake');
                sendHandshake();
              }
            }, 300);
          </script>
        </body>
        </html>
        """;
}
