using McpServiceHub.Services.Interfaces;

namespace McpServiceHub.Services;

public class OtpService : IOtpService
{
    private readonly Random _random = new();
    
    public string GenerateOtp()
    {
        return _random.Next(100000, 999999).ToString();
    }

    public bool IsOtpValid(string userOtp, string storedOtp, DateTime? expiration)
    {
        if (string.IsNullOrEmpty(userOtp) || string.IsNullOrEmpty(storedOtp))
            return false;
            
        if (!expiration.HasValue || expiration.Value < DateTime.UtcNow)
            return false;
            
        return userOtp == storedOtp;
    }
}