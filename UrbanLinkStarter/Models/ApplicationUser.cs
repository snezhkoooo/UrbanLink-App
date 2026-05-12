using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace UrbanLinkStarter.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? ProfilePicture { get; set; }

    [Range(18, 99)]
    public int Age { get; set; } = 18;

    // True if a moderator has reviewed and approved an ID/age document upload.
    // Required for sending messages and reserving rides.
    public bool IsVerified { get; set; } = false;

    // True once an admin has approved a Driver Verification request. Required to create trips.
    // Drives the blue checkmark next to a user's name.
    public bool IsDriver { get; set; } = false;

    // Popup flags — show once on next login
    public bool SeenVerificationResult { get; set; } = false;
    public bool SeenDriverVerificationResult { get; set; } = false;

    // Driver rating — recalculated whenever a new DriverRating is saved
    public double AverageRating { get; set; } = 0;
    public int TotalRatings { get; set; } = 0;

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
