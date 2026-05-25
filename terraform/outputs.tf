output "apim_gateway_url" {
  value       = azurerm_api_management.mcp.gateway_url
  description = "APIM gateway base URL"
}

output "mcp_endpoint" {
  value       = "${azurerm_api_management.mcp.gateway_url}/mcp"
  description = "Stable MCP endpoint — use this in ChatGPT Connector"
}
