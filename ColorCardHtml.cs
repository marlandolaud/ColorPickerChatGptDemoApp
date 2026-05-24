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
          </style>
        </head>
        <body>
          <div class="card">
            <div class="swatch" id="swatch"></div>
            <div class="label" id="label">—</div>
          </div>
          <script>
            window.addEventListener('message', (event) => {
              const data = event.data?.params?.structuredContent;
              if (data?.color) {
                document.getElementById('swatch').style.backgroundColor = data.color;
                document.getElementById('label').textContent = data.color;
              }
            });
          </script>
        </body>
        </html>
        """;
}
