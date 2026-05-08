using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanLinkStarter.Data;
using UrbanLinkStarter.Models;
using UrbanLinkStarter.Services;
namespace UrbanLinkStarter.Controllers;

public class TripsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TripsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // ====== Public index — shows all future trips ======
    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;
        var trips = await _context.Trips
            .Include(t => t.Event)
            .Include(t => t.Driver)
            .Where(t => t.TravelDate >= now)
            .OrderBy(t => t.TravelDate)
            .ToListAsync();
        return View(trips);
    }

    // ====== Details ======
    public async Task<IActionResult> Details(int id)
    {
        var trip = await _context.Trips
            .Include(t => t.Reservations).ThenInclude(r => r.User)
            .Include(t => t.Event)
            .Include(t => t.Driver)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (trip == null) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        var seatsUsed = trip.Reservations.Sum(r => r.SeatsCount);
        var alreadyReserved = currentUser != null && trip.Reservations.Any(r => r.UserId == currentUser.Id);
        var seatsLeft = Math.Max(trip.AvailableSeats - seatsUsed, 0);

        ViewBag.AlreadyReserved = alreadyReserved;
        ViewBag.SeatsLeft = seatsLeft;
        ViewBag.IsDriver = currentUser != null && currentUser.Id == trip.DriverId;
        ViewBag.CurrentUserId = currentUser?.Id;
        ViewBag.AlreadyRated = currentUser != null && await _context.DriverRatings.AnyAsync(r => r.TripId == id && r.RaterId == currentUser.Id);
        ViewBag.Ratings = await _context.DriverRatings.Include(r => r.Rater).Where(r => r.TripId == id).OrderByDescending(r => r.CreatedOn).ToListAsync();
        return View(trip);
    }

    // ====== Create ======
    [Authorize]
    public async Task<IActionResult> Create(int? eventId = null)
    {
        await PopulateEventsAsync();
        return View(new Trip { EventId = eventId, AvailableSeats = 3, CarYear = DateTime.UtcNow.Year });
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Create(Trip trip)
    {
        // Validate event
        if (trip.EventId.HasValue)
        {
            var exists = await _context.Events.AnyAsync(e => e.Id == trip.EventId.Value);
            if (!exists) trip.EventId = null;
        }

        // Remove model-state errors we'll compute
        ModelState.Remove(nameof(Trip.Price));
        ModelState.Remove(nameof(Trip.DistanceKm));
        ModelState.Remove(nameof(Trip.DriverId));

        if (!ModelState.IsValid)
        {
            await PopulateEventsAsync();
            return View(trip);
        }

        var driver = await _userManager.GetUserAsync(User);
        trip.DriverId = driver?.Id;
        trip.DistanceKm = await GeocodingHelper.GetDistanceKmAsync(trip.FromLocation, trip.ToLocation);
        trip.Price = TripPricing.PricePerSeat(trip.DistanceKm ?? 0, trip.FuelConsumption, trip.FuelType, Math.Max(trip.AvailableSeats, 1));

        _context.Trips.Add(trip);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // ====== Edit (driver only) ======
    [Authorize]
    public async Task<IActionResult> Edit(int id)
    {
        var trip = await _context.Trips.Include(t => t.Event).FirstOrDefaultAsync(t => t.Id == id);
        if (trip == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (trip.DriverId != user?.Id && !User.IsInRole("Admin"))
            return Forbid();

        await PopulateEventsAsync();
        return View(trip);
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Trip input)
    {
        var trip = await _context.Trips.FirstOrDefaultAsync(t => t.Id == id);
        if (trip == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (trip.DriverId != user?.Id && !User.IsInRole("Admin"))
            return Forbid();

        ModelState.Remove(nameof(Trip.Price));
        ModelState.Remove(nameof(Trip.DistanceKm));
        ModelState.Remove(nameof(Trip.DriverId));

        if (!ModelState.IsValid)
        {
            await PopulateEventsAsync();
            return View(input);
        }

        trip.Title          = input.Title;
        trip.FromLocation   = input.FromLocation;
        trip.ToLocation     = input.ToLocation;
        trip.TravelDate     = input.TravelDate;
        trip.AvailableSeats = input.AvailableSeats;
        trip.CarBrand       = input.CarBrand;
        trip.CarModel       = input.CarModel;
        trip.CarYear        = input.CarYear;
        trip.FuelConsumption = input.FuelConsumption;
        trip.FuelType       = input.FuelType;
        trip.EventId        = input.EventId.HasValue && await _context.Events.AnyAsync(e => e.Id == input.EventId) ? input.EventId : null;
        trip.DistanceKm     = await GeocodingHelper.GetDistanceKmAsync(trip.FromLocation, trip.ToLocation);
        trip.Price          = TripPricing.PricePerSeat(trip.DistanceKm ?? 0, trip.FuelConsumption, trip.FuelType, Math.Max(trip.AvailableSeats, 1));

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    // ====== Delete (driver only) ======
    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var trip = await _context.Trips.Include(t => t.Reservations).FirstOrDefaultAsync(t => t.Id == id);
        if (trip == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (trip.DriverId != user?.Id && !User.IsInRole("Admin"))
            return Forbid();

        _context.Reservations.RemoveRange(trip.Reservations);
        _context.Trips.Remove(trip);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // ====== Driver removes a specific passenger reservation ======
    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePassenger(int reservationId)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Trip)
            .FirstOrDefaultAsync(r => r.Id == reservationId);
        if (reservation == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (reservation.Trip?.DriverId != user?.Id && !User.IsInRole("Admin"))
            return Forbid();

        var tripId = reservation.TripId;
        _context.Reservations.Remove(reservation);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Passenger removed.";
        return RedirectToAction(nameof(Details), new { id = tripId });
    }

    private async Task PopulateEventsAsync()
    {
        ViewBag.UpcomingEvents = await _context.Events
            .Where(e => e.EventDate >= DateTime.UtcNow.AddDays(-1))
            .OrderBy(e => e.EventDate)
            .ToListAsync();
    }
}
