#!/usr/bin/env bash
set -e

if [ ! -f .env ] || ! grep -q "NGROK_AUTHTOKEN=.\+" .env 2>/dev/null; then
  echo "Error: NGROK_AUTHTOKEN is not set."
  echo "Copy .env.example to .env and add your token from https://dashboard.ngrok.com/get-started/your-authtoken"
  exit 1
fi

echo "Building and starting containers..."
docker compose up --build -d

echo "Waiting for ngrok tunnel..."
for i in {1..30}; do
  URL=$(curl -s http://localhost:4040/api/tunnels 2>/dev/null \
    | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['tunnels'][0]['public_url'])" 2>/dev/null) || true
  if [ -n "$URL" ]; then break; fi
  sleep 1
done

if [ -z "$URL" ]; then
  echo "Could not detect tunnel URL. Check logs: docker compose logs ngrok"
  exit 1
fi

echo ""
echo "============================================================"
echo "  NGROK TUNNEL: $URL/mcp"
echo "============================================================"
echo ""

NGROK_HOST=$(echo "$URL" | sed 's|https://||')

if command -v terraform &>/dev/null && [ -f terraform/terraform.tfstate ]; then
  APIM_URL=$(terraform -chdir=terraform output -raw mcp_endpoint 2>/dev/null) || true
  if [ -n "$APIM_URL" ]; then
    echo "============================================================"
    echo "  APIM ENDPOINT (stable): $APIM_URL"
    echo "============================================================"
    echo ""
    echo "ChatGPT setup:"
    echo "  1. chatgpt.com -> Settings -> Connectors -> Create"
    echo "  2. Server URL: $APIM_URL"
    echo "  3. Transport: HTTP  |  Authentication: None"
    echo "  4. Save, then ask: 'show me the color coral'"
    echo ""
    echo "Update APIM backend to this tunnel (run when ngrok URL changes):"
    echo "  terraform -chdir=terraform apply -var=\"ngrok_url=$NGROK_HOST\""
  else
    echo "ChatGPT setup (direct ngrok — run 'terraform apply' to set up APIM):"
    echo "  Server URL: $URL/mcp"
    echo ""
    echo "Provision APIM:"
    echo "  terraform -chdir=terraform apply \\"
    echo "    -var=\"ngrok_url=$NGROK_HOST\" \\"
    echo "    -var=\"publisher_email=<your-email>\""
  fi
else
  echo "ChatGPT setup (direct ngrok — install Terraform and apply to set up APIM):"
  echo "  Server URL: $URL/mcp"
  echo ""
  echo "Provision APIM:"
  echo "  terraform -chdir=terraform apply \\"
  echo "    -var=\"ngrok_url=$NGROK_HOST\" \\"
  echo "    -var=\"publisher_email=<your-email>\""
fi

echo ""
echo "To stop: docker compose down"
echo "Logs:    docker compose logs -f"
