using System.ComponentModel.DataAnnotations;

namespace UrbanLinkStarter.Models;

public enum FriendshipStatus
{
    Pending  = 0,
    Accepted = 1,
    Rejected = 2
}

public class Friendship
{
    public int Id { get; set; }

    [Required]
    public string RequesterId { get; set; } = string.Empty;
    public ApplicationUser? Requester { get; set; }

    [Required]
    public string AddresseeId { get; set; } = string.Empty;
    public ApplicationUser? Addressee { get; set; }

    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

    /// <summary>True once the addressee has viewed (opened the My Friends page) the request.</summary>
    public bool SeenByAddressee { get; set; } = false;

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedOn { get; set; }
}
