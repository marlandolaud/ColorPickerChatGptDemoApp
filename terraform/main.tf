resource "azurerm_resource_group" "mcp" {
  name     = var.resource_group_name
  location = var.location
}

resource "azurerm_api_management" "mcp" {
  name                = var.apim_name
  resource_group_name = azurerm_resource_group.mcp.name
  location            = azurerm_resource_group.mcp.location
  publisher_name      = var.publisher_name
  publisher_email     = var.publisher_email
  sku_name            = "Consumption_0"
}

# API with empty path so ChatGPT calls https://{apim}.azure-api.net/mcp
resource "azurerm_api_management_api" "mcp" {
  name                = "mcp-api"
  resource_group_name = azurerm_resource_group.mcp.name
  api_management_name = azurerm_api_management.mcp.name
  revision            = "1"
  display_name        = "MCP API"
  path                = ""
  protocols           = ["https"]
  service_url         = "https://${var.ngrok_url}"
}

# Catch-all operation: handles POST /mcp (tool calls) and GET /mcp/* (UI resources)
resource "azurerm_api_management_api_operation" "catchall" {
  operation_id        = "catchall"
  api_name            = azurerm_api_management_api.mcp.name
  api_management_name = azurerm_api_management.mcp.name
  resource_group_name = azurerm_resource_group.mcp.name
  display_name        = "Catch-all"
  method              = "POST"
  url_template        = "/*"
}

# Passthrough policy — no buffering so MCP headers flow through cleanly
resource "azurerm_api_management_api_policy" "passthrough" {
  api_name            = azurerm_api_management_api.mcp.name
  api_management_name = azurerm_api_management.mcp.name
  resource_group_name = azurerm_resource_group.mcp.name

  xml_content = <<XML
<policies>
  <inbound>
    <base />
  </inbound>
  <backend>
    <forward-request buffer-response="false" />
  </backend>
  <outbound>
    <base />
  </outbound>
  <on-error>
    <base />
  </on-error>
</policies>
XML
}
