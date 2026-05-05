using System.ComponentModel.DataAnnotations;

namespace McpServiceHub.Models.ViewModels;

public class VerifyOtpViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    [Display(Name = "OTP Code")]
    public string OtpCode { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}