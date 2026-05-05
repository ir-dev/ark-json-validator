using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace McpServiceHub.Models.Entities;

public class McpService
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Url]
    public string EndpointUrl { get; set; } = string.Empty;

    [StringLength(50)]
    public string Domain { get; set; } = string.Empty;

    public string AuthenticationType { get; set; } = "None"; // None, ApiKey, OAuth

    public string? ApiKeyHeader { get; set; }

    [ForeignKey("Owner")]
    public int OwnerUserId { get; set; }

    public virtual User Owner { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}