using System.ComponentModel.DataAnnotations;

namespace McpServiceHub.Models.ViewModels;

public class SendOtpViewModel
{
    [Required]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;
}