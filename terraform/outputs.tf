output "apim_gateway_url" {
  value       = azurerm_api_management.mcp.gateway_url
  description = "APIM gateway base URL"
}

output "mcp_endpoint" {
  value       = "${azurerm_api_management.mcp.gateway_url}/mcp"
  description = "Stable MCP endpoint — use this in ChatGPT Connector"
}

output "tenant_id" {
  value       = data.azurerm_client_config.current.tenant_id
  description = "Azure tenant ID — set as AZURE_TENANT_ID in .env"
}

output "mcp_api_client_id" {
  value       = azuread_application.mcp_api.client_id
  description = "MCP API app client ID — set as MCP_API_CLIENT_ID in .env"
}

output "authorization_endpoint" {
  value       = "https://login.microsoftonline.com/${data.azurerm_client_config.current.tenant_id}/oauth2/v2.0/authorize"
  description = "OAuth authorization endpoint — paste into ChatGPT connector"
}

output "token_endpoint" {
  value       = "https://login.microsoftonline.com/${data.azurerm_client_config.current.tenant_id}/oauth2/v2.0/token"
  description = "OAuth token endpoint — paste into ChatGPT connector"
}

output "chatgpt_client_id" {
  value       = azuread_application.chatgpt_client.client_id
  description = "Client ID — paste into ChatGPT connector"
}

output "chatgpt_client_secret" {
  value       = azuread_application_password.chatgpt_client.value
  sensitive   = true
  description = "Client secret — retrieve with: terraform output -raw chatgpt_client_secret"
}

output "oauth_scope" {
  value       = "api://mcp-color-picker/mcp.access"
  description = "OAuth scope — paste into ChatGPT connector"
}
