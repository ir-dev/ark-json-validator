using System.ComponentModel.DataAnnotations;

namespace McpServiceHub.Models.Entities;

public class User
{
    [Key]
    public int Id { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? OtpCode { get; set; }

    public DateTime? OtpExpiration { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    // Navigation property
    public ICollection<McpService> McpServices { get; set; } = new List<McpService>();
}