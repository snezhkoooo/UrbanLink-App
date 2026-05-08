using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanLinkStarter.Data;
using UrbanLinkStarter.Models;

namespace UrbanLinkStarter.Controllers;

[Authorize]
public class ReservationsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReservationsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // My Reservations
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var reservations = await _context.Reservations
            .Include(r => r.Trip).ThenInclude(t => t!.Event)
            .Include(r => r.Trip).ThenInclude(t => t!.Driver)
            .Where(r => r.UserId == user.Id)
            .OrderByDescending(r => r.CreatedOn)
            .ToListAsync();

        return View(reservations);
    }

    // My Trips (driver view — see who reserved on my trips)
    public async Task<IActionResult> MyTrips()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var trips = await _context.Trips
            .Include(t => t.Event)
            .Include(t => t.Reservations).ThenInclude(r => r.User)
            .Where(t => t.DriverId == user.Id)
            .OrderByDescending(t => t.TravelDate)
            .ToListAsync();

        return View(trips);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int tripId, PaymentMethod paymentMethod, int seatsCount = 1)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var trip = await _context.Trips
            .Include(t => t.Reservations)
            .FirstOrDefaultAsync(t => t.Id == tripId);
        if (trip == null) return NotFound();

        if (trip.DriverId == user.Id)
        {
            TempData["Error"] = "You can't reserve a seat on your own trip.";
            return RedirectToAction("Details", "Trips", new { id = tripId });
        }

        if (trip.Reservations.Any(r => r.UserId == user.Id))
        {
            TempData["Error"] = "You already have a reservation on this trip.";
            return RedirectToAction("Details", "Trips", new { id = tripId });
        }

        var seatsUsed = trip.Reservations.Sum(r => r.SeatsCount);
        if (seatsUsed + seatsCount > trip.AvailableSeats)
        {
            TempData["Error"] = $"Not enough seats. Only {trip.AvailableSeats - seatsUsed} left.";
            return RedirectToAction("Details", "Trips", new { id = tripId });
        }

        _context.Reservations.Add(new Reservation
        {
            TripId        = tripId,
            UserId        = user.Id,
            PaymentMethod = paymentMethod,
            SeatsCount    = Math.Max(1, Math.Min(seatsCount, trip.AvailableSeats - seatsUsed)),
            CreatedOn     = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        TempData["Success"] = "Seat(s) reserved! The driver will see your booking.";
        return RedirectToAction("Details", "Trips", new { id = tripId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? returnTo = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var reservation = await _context.Reservations
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == user.Id);
        if (reservation == null) return NotFound();

        var tripId = reservation.TripId;
        _context.Reservations.Remove(reservation);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Reservation cancelled.";
        if (returnTo == "trip") return RedirectToAction("Details", "Trips", new { id = tripId });
        return RedirectToAction(nameof(Index));
    }
}
