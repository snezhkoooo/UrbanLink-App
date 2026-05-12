using System.ComponentModel.DataAnnotations;

namespace UrbanLinkStarter.Models;

public class Message
{
    public int Id { get; set; }

    [StringLength(300)]
    public string Content { get; set; } = string.Empty;

    // Optional image (base64 data URI) — only allowed in community feed (EventId == null).
    public string? ImageData { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int? EventId { get; set; }
    public Event? Event { get; set; }

    /// <summary>If set, this message is a reply to another community-feed message.</summary>
    public int? ParentId { get; set; }
    public Message? Parent { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
