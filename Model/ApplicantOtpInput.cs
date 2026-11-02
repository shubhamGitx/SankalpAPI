using System.ComponentModel.DataAnnotations;

namespace SankalpAPI.Model;

public sealed class ApplicantOtpInput
{
    [Required] public string ClientKey { get; set; } = string.Empty;
    [Required] public long PortalId { get; set; }
    [Required] public string MobileNo { get; set; } = string.Empty;
    [Required] public string OtpId { get; set; } = string.Empty;
    [Required] public string Otp { get; set; } = string.Empty;
}
