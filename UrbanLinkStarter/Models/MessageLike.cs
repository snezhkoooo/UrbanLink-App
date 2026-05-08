namespace UrbanLinkStarter.Models;

public class MessageLike
{
    public int Id { get; set; }
    public int MessageId { get; set; }
    public Message? Message { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public bool IsLike { get; set; } = true; // true = like, false = dislike
}
