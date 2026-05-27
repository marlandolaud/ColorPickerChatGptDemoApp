data "azurerm_client_config" "current" {}

resource "random_uuid" "mcp_scope_id" {}

# App registration: MCP API (resource server — the .NET app validates tokens against this)
resource "azuread_application" "mcp_api" {
  display_name    = "MCP Color Picker API"
  identifier_uris = ["api://${data.azurerm_client_config.current.tenant_id}/mcp-color-picker"]

  api {
    requested_access_token_version = 2

    oauth2_permission_scope {
      admin_consent_description  = "Access the MCP Color Picker API"
      admin_consent_display_name = "Access MCP API"
      enabled                    = true
      id                         = random_uuid.mcp_scope_id.result
      type                       = "User"
      user_consent_description   = "Access the MCP Color Picker API"
      user_consent_display_name  = "Access MCP API"
      value                      = "mcp.access"
    }
  }
}

resource "azuread_service_principal" "mcp_api" {
  client_id = azuread_application.mcp_api.client_id
}

# App registration: ChatGPT client
resource "azuread_application" "chatgpt_client" {
  display_name = "ChatGPT MCP Client"

  web {
    redirect_uris = ["${azurerm_api_management.mcp.gateway_url}/oauth/callback"]
  }

  required_resource_access {
    resource_app_id = azuread_application.mcp_api.client_id
    resource_access {
      id   = random_uuid.mcp_scope_id.result
      type = "Scope"
    }
  }
}

resource "azuread_service_principal" "chatgpt_client" {
  client_id = azuread_application.chatgpt_client.client_id
}

resource "azuread_application_password" "chatgpt_client" {
  application_id = azuread_application.chatgpt_client.id
  display_name   = "mcp-poc-secret"
  end_date       = "2027-01-01T00:00:00Z"
}

# Pre-grant admin consent — avoids consent prompt when ChatGPT requests the token
resource "azuread_service_principal_delegated_permission_grant" "chatgpt_to_mcp" {
  service_principal_object_id          = azuread_service_principal.chatgpt_client.object_id
  resource_service_principal_object_id = azuread_service_principal.mcp_api.object_id
  claim_values                         = ["mcp.access"]
}

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
  path                  = ""
  protocols             = ["https"]
  service_url           = "https://${var.ngrok_url}"
  subscription_required = false
}

# POST catch-all: MCP tool calls and resource reads
resource "azurerm_api_management_api_operation" "catchall_post" {
  operation_id        = "catchall-post"
  api_name            = azurerm_api_management_api.mcp.name
  api_management_name = azurerm_api_management.mcp.name
  resource_group_name = azurerm_resource_group.mcp.name
  display_name        = "Catch-all POST"
  method              = "POST"
  url_template        = "/*"
}

# GET catch-all: iframe UI resource HTML
resource "azurerm_api_management_api_operation" "catchall_get" {
  operation_id        = "catchall-get"
  api_name            = azurerm_api_management_api.mcp.name
  api_management_name = azurerm_api_management.mcp.name
  resource_group_name = azurerm_resource_group.mcp.name
  display_name        = "Catch-all GET"
  method              = "GET"
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
