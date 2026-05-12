using System.ComponentModel.DataAnnotations;

namespace UrbanLinkStarter.Models;

public enum VerificationStatus
{
    Pending  = 0,
    Approved = 1,
    Rejected = 2
}

public enum DocumentType
{
    NationalId    = 0,
    DriversLicense = 1,
    Passport       = 2,
    Revolut        = 3
}

public class VerificationRequest
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DocumentType DocumentType { get; set; }

    // base64 data URI of the uploaded image
    [Required]
    public string DocumentImage { get; set; } = string.Empty;

    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;

    public string? AdminNote { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedOn { get; set; }
}
