using System.ComponentModel.DataAnnotations;

namespace UrbanLinkStarter.Models;

public class Event
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Location { get; set; } = string.Empty;

    public DateTime EventDate { get; set; }
}
