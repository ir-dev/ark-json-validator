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