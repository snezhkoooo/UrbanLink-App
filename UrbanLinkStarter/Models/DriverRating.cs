using System.ComponentModel.DataAnnotations;

namespace UrbanLinkStarter.Models;

public class DriverRating
{
    public int Id { get; set; }

    [Required]
    public int TripId { get; set; }
    public Trip? Trip { get; set; }

    /// <summary>Driver being rated (= Trip.DriverId)</summary>
    [Required]
    public string DriverId { get; set; } = string.Empty;
    public ApplicationUser? Driver { get; set; }

    /// <summary>Passenger who gave the rating</summary>
    [Required]
    public string RaterId { get; set; } = string.Empty;
    public ApplicationUser? Rater { get; set; }

    [Range(1, 5)]
    public int Stars { get; set; } = 5;

    [StringLength(200)]
    public string? Comment { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
