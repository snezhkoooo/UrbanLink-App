using System.ComponentModel.DataAnnotations;

namespace UrbanLinkStarter.Models;

public enum FuelType
{
    Gasoline = 0,
    Diesel   = 1,
    LPG      = 2,
    Electric = 3
}

public class Trip
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string FromLocation { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ToLocation { get; set; } = string.Empty;

    [Required]
    public DateTime TravelDate { get; set; }

    [Range(1, 6)]
    public int AvailableSeats { get; set; }

    [Required, StringLength(60)]
    public string CarBrand { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string CarModel { get; set; } = string.Empty;

    [Range(1980, 2030)]
    public int CarYear { get; set; } = DateTime.UtcNow.Year;

    [Range(1.0, 30.0)]
    public double FuelConsumption { get; set; } = 7.0;

    public FuelType FuelType { get; set; } = FuelType.Gasoline;

    public double? DistanceKm { get; set; }

    [Range(0, 9999)]
    public decimal Price { get; set; }

    public int? EventId { get; set; }
    public Event? Event { get; set; }

    public string? DriverId { get; set; }
    public ApplicationUser? Driver { get; set; }

    /// <summary>Driver has marked this trip as completed. Triggers rating prompts for passengers.</summary>
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedAt { get; set; }

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
