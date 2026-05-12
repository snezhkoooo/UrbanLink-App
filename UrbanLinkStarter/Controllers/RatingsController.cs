using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanLinkStarter.Data;
using UrbanLinkStarter.Models;

namespace UrbanLinkStarter.Controllers;

[Authorize]
public class RatingsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public RatingsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>Driver marks a trip as completed — triggers rating prompts for all passengers.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkDone(int tripId)
    {
        var trip = await _context.Trips.FindAsync(tripId);
        if (trip == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user == null || trip.DriverId != user.Id) return Forbid();

        trip.IsCompleted = true;
        trip.CompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Trip marked as completed. Passengers will be asked to rate you.";
        return RedirectToAction("Details", "Trips", new { id = tripId });
    }

    /// <summary>POST – passenger submits a star rating for the driver of a completed trip.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int tripId, int stars, string? comment)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var trip = await _context.Trips
            .Include(t => t.Reservations)
            .FirstOrDefaultAsync(t => t.Id == tripId);
        if (trip == null || !trip.IsCompleted) return NotFound();

        // Passenger must have a reservation on this trip
        if (!trip.Reservations.Any(r => r.UserId == user.Id)) return Forbid();

        // Prevent duplicate rating
        if (await _context.DriverRatings.AnyAsync(r => r.TripId == tripId && r.RaterId == user.Id))
        {
            TempData["Info"] = "You've already rated this trip.";
            return RedirectToAction("Details", "Trips", new { id = tripId });
        }

        stars = Math.Max(1, Math.Min(5, stars));

        _context.DriverRatings.Add(new DriverRating
        {
            TripId   = tripId,
            DriverId = trip.DriverId!,
            RaterId  = user.Id,
            Stars    = stars,
            Comment  = comment?.Trim()
        });

        // Recalculate driver's average
        await _context.SaveChangesAsync();
        await RecalcDriverAvgAsync(trip.DriverId!);

        TempData["Success"] = "Thanks for your rating!";
        return RedirectToAction("Details", "Trips", new { id = tripId });
    }

    private async Task RecalcDriverAvgAsync(string driverId)
    {
        var ratings = await _context.DriverRatings
            .Where(r => r.DriverId == driverId)
            .ToListAsync();

        var driver = await _context.Users.FindAsync(driverId);
        if (driver == null) return;

        driver.TotalRatings  = ratings.Count;
        driver.AverageRating = ratings.Count > 0 ? Math.Round(ratings.Average(r => r.Stars), 1) : 0;
        await _context.SaveChangesAsync();
    }
}
