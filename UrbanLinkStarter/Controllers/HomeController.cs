using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanLinkStarter.Data;
using UrbanLinkStarter.Models;

namespace UrbanLinkStarter.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(ApplicationDbContext context, RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _roleManager = roleManager;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var now   = DateTime.UtcNow;
        var until = now.AddDays(14);

        ViewBag.Trips = await _context.Trips
            .Include(t => t.Event)
            .Include(t => t.Driver)
            .Where(t => t.TravelDate >= now && t.TravelDate <= until)
            .OrderBy(t => t.TravelDate)
            .Take(5)
            .ToListAsync();

        ViewBag.Events = await _context.Events
            .Where(e => e.EventDate >= now && e.EventDate <= until)
            .OrderBy(e => e.EventDate)
            .Take(5)
            .ToListAsync();

        // Check if the logged-in user has a new verification result they haven't seen yet.
        ViewBag.VerificationPopup = null;
        ViewBag.AdminPendingCount = 0;
        ViewBag.PendingRatingTrip = null;

        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                // Verification result popup for regular users
                if (!user.SeenVerificationResult)
                {
                    var latestRequest = await _context.VerificationRequests
                        .Where(v => v.UserId == user.Id && v.Status != VerificationStatus.Pending)
                        .OrderByDescending(v => v.ReviewedOn)
                        .FirstOrDefaultAsync();

                    if (latestRequest != null)
                    {
                        ViewBag.VerificationPopup = latestRequest.Status.ToString();
                        user.SeenVerificationResult = true;
                        await _userManager.UpdateAsync(user);
                    }
                }

                // Admin: pending verification count
                if (User.IsInRole("Admin"))
                {
                    ViewBag.AdminPendingCount = await _context.VerificationRequests
                        .CountAsync(v => v.Status == VerificationStatus.Pending);
                }

                // Passenger: pending driver rating
                var pendingRating = await _context.Reservations
                    .Include(r => r.Trip)
                    .Where(r => r.UserId == user.Id
                        && r.Trip != null
                        && r.Trip.IsCompleted
                        && r.Trip.DriverId != user.Id)
                    .Select(r => r.Trip)
                    .Where(t => !_context.DriverRatings.Any(dr => dr.TripId == t!.Id && dr.RaterId == user.Id))
                    .FirstOrDefaultAsync();

                ViewBag.PendingRatingTrip = pendingRating;
            }
        }

        return View();
    }

    // Called via AJAX to dismiss the popup without page reload
    [HttpPost]
    public IActionResult DismissVerificationPopup() => Ok();
}
