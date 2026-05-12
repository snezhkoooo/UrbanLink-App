using System.ComponentModel.DataAnnotations;

namespace UrbanLinkStarter.Models;

public enum PaymentMethod
{
    Cash         = 0,
    Revolut      = 1,
    BankTransfer = 2
}

public class Reservation
{
    public int Id { get; set; }

    [Required]
    public int TripId { get; set; }
    public Trip? Trip { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    [Range(1, 6)]
    public int SeatsCount { get; set; } = 1;

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
