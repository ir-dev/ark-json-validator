namespace McpServiceHub.Services.Interfaces;

public interface IOtpService
{
    string GenerateOtp();
    bool IsOtpValid(string userOtp, string storedOtp, DateTime? expiration);
}