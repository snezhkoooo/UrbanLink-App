using System.ComponentModel.DataAnnotations;

namespace UrbanLinkStarter.Models;

public class DirectMessage
{
    public int Id { get; set; }

    [Required]
    public string FromUserId { get; set; } = string.Empty;
    public ApplicationUser? FromUser { get; set; }

    [Required]
    public string ToUserId { get; set; } = string.Empty;
    public ApplicationUser? ToUser { get; set; }

    [Required, StringLength(1000)]
    public string Body { get; set; } = string.Empty;

    public DateTime SentOn { get; set; } = DateTime.UtcNow;
    public DateTime? ReadOn { get; set; }
}
