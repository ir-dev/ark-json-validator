using System.ComponentModel.DataAnnotations;

namespace McpServiceHub.Models.ViewModels;

public class McpServiceViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Service name is required")]
    [StringLength(100, MinimumLength = 3)]
    [Display(Name = "Service Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Endpoint URL is required")]
    [Url(ErrorMessage = "Please enter a valid URL")]
    [Display(Name = "Endpoint URL")]
    public string EndpointUrl { get; set; } = string.Empty;

    [Display(Name = "Domain/Category")]
    public string Domain { get; set; } = string.Empty;

    [Display(Name = "Authentication Type")]
    public string AuthenticationType { get; set; } = "None";

    [Display(Name = "API Key Header (if using API Key)")]
    public string? ApiKeyHeader { get; set; }

    public string? OwnerEmail { get; set; }
}

public class McpDiscoveryViewModel
{
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Pre-populated from the registered service; user can still override here.
    [Display(Name = "Authentication Type")]
    public string AuthenticationType { get; set; } = "None";

    [Display(Name = "API Key Header")]
    public string? ApiKeyHeader { get; set; }

    [Display(Name = "API Key Value")]
    public string? ApiKeyValue { get; set; }

    [Display(Name = "Bearer Token")]
    public string? BearerToken { get; set; }

    [Display(Name = "OAuth Client ID")]
    public string? OAuthClientId { get; set; }

    [Display(Name = "OAuth Client Secret")]
    public string? OAuthClientSecret { get; set; }

    [Display(Name = "OAuth Access Token")]
    public string? OAuthAccessToken { get; set; }

    [Display(Name = "Username")]
    public string? BasicUsername { get; set; }

    [Display(Name = "Password")]
    public string? BasicPassword { get; set; }

    // Discovery results
    public bool HasDiscovered { get; set; }
    public string? DiscoveryError { get; set; }
    public List<McpToolInfo> Tools { get; set; } = [];
    public List<McpResourceInfo> Resources { get; set; } = [];
    public List<McpPromptInfo> Prompts { get; set; } = [];
}

public class McpToolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InputSchema { get; set; } = string.Empty;
}

public class McpResourceInfo
{
    public string Uri { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? MimeType { get; set; }
}

public class McpPromptInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}