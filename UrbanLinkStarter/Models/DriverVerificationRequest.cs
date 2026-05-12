using System.ComponentModel.DataAnnotations;

namespace UrbanLinkStarter.Models;

public class DriverVerificationRequest
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    /// <summary>Car photo, front/exterior (base64 data URI).</summary>
    [Required]
    public string CarPhoto { get; set; } = string.Empty;

    /// <summary>Car interior photo (base64 data URI).</summary>
    [Required]
    public string CarInteriorPhoto { get; set; } = string.Empty;

    /// <summary>Driver's license photo (base64 data URI).</summary>
    [Required]
    public string LicensePhoto { get; set; } = string.Empty;

    /// <summary>GDPR consent — user must check this before submitting.</summary>
    public bool GdprConsent { get; set; } = false;

    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
    public string? AdminNote { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedOn { get; set; }
}
