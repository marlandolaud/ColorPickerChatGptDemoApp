variable "ngrok_url" {
  description = "Ngrok public hostname (no scheme), e.g. abc-123.ngrok-free.app"
  type        = string
}

variable "resource_group_name" {
  description = "Azure resource group to create"
  type        = string
  default     = "rg-mcp-poc"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "eastus"
}

variable "apim_name" {
  description = "Globally unique APIM instance name"
  type        = string
  default     = "apim-mcp-poc"
}

variable "publisher_name" {
  type    = string
  default = "MCP POC"
}

variable "publisher_email" {
  description = "Required by Azure APIM"
  type        = string
}
