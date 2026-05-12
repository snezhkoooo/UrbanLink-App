using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanLinkStarter.Data;
using UrbanLinkStarter.Models;

namespace UrbanLinkStarter.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // ================================================================
    // Dashboard
    // ================================================================
    public async Task<IActionResult> Index()
    {
        ViewBag.UsersCount    = await _context.Users.CountAsync();
        ViewBag.TripsCount    = await _context.Trips.CountAsync();
        ViewBag.EventsCount   = await _context.Events.CountAsync();
        ViewBag.MessagesCount = await _context.Messages.CountAsync();
        ViewBag.PendingVerifications = await _context.VerificationRequests
            .CountAsync(v => v.Status == VerificationStatus.Pending);
        ViewBag.PendingDriverVerifications = await _context.DriverVerificationRequests
            .CountAsync(v => v.Status == VerificationStatus.Pending);
        return View();
    }

    // ================================================================
    // Verifications (age / identity)
    // ================================================================
    public async Task<IActionResult> Verifications()
    {
        var requests = await _context.VerificationRequests
            .Include(v => v.User)
            .OrderBy(v => v.Status)
            .ThenByDescending(v => v.CreatedOn)
            .ToListAsync();
        return View(requests);
    }

    // ================================================================
    // Driver verifications (separate tab from identity verifications)
    // ================================================================
    public async Task<IActionResult> DriverVerifications()
    {
        var requests = await _context.DriverVerificationRequests
            .Include(v => v.User)
            .OrderBy(v => v.Status)
            .ThenByDescending(v => v.CreatedOn)
            .ToListAsync();
        return View(requests);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveDriverVerification(int id)
    {
        var req = await _context.DriverVerificationRequests
            .Include(v => v.User)
            .FirstOrDefaultAsync(v => v.Id == id);
        if (req == null || req.User == null) return NotFound();

        req.Status = VerificationStatus.Approved;
        req.ReviewedOn = DateTime.UtcNow;
        req.User.IsDriver = true;
        req.User.SeenDriverVerificationResult = false;
        req.CarPhoto = "";
        req.CarInteriorPhoto = "";
        req.LicensePhoto = "";
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(DriverVerifications));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectDriverVerification(int id, string? note)
    {
        var req = await _context.DriverVerificationRequests
            .Include(v => v.User)
            .FirstOrDefaultAsync(v => v.Id == id);
        if (req == null) return NotFound();

        req.Status = VerificationStatus.Rejected;
        req.ReviewedOn = DateTime.UtcNow;
        req.AdminNote = note;
        req.User!.SeenDriverVerificationResult = false;
        req.CarPhoto = "";
        req.CarInteriorPhoto = "";
        req.LicensePhoto = "";
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(DriverVerifications));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveVerification(int id)
    {
        var req = await _context.VerificationRequests
            .Include(v => v.User)
            .FirstOrDefaultAsync(v => v.Id == id);
        if (req == null || req.User == null) return NotFound();

        req.Status = VerificationStatus.Approved;
        req.ReviewedOn = DateTime.UtcNow;
        req.User.IsVerified = true;
        req.User.SeenVerificationResult = false; // show popup on next login
        req.DocumentImage = "";
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Verifications));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectVerification(int id, string? note)
    {
        var req = await _context.VerificationRequests
            .Include(v => v.User)
            .FirstOrDefaultAsync(v => v.Id == id);
        if (req == null) return NotFound();

        req.Status = VerificationStatus.Rejected;
        req.ReviewedOn = DateTime.UtcNow;
        req.AdminNote = note;
        req.User!.SeenVerificationResult = false; // show popup on next login
        req.DocumentImage = "";
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Verifications));
    }

    // ================================================================
    // Users
    // ================================================================
    public async Task<IActionResult> Users()
    {
        var users = await _context.Users.OrderBy(u => u.FullName).ToListAsync();

        var counts = await _context.Messages
            .GroupBy(m => m.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        var adminIds = new HashSet<string>();
        if (adminRole != null)
        {
            adminIds = (await _context.UserRoles.Where(ur => ur.RoleId == adminRole.Id).Select(ur => ur.UserId).ToListAsync()).ToHashSet();
        }

        ViewBag.MessageCounts = counts;
        ViewBag.AdminUserIds  = adminIds;
        return View(users);
    }

    public async Task<IActionResult> UserDetails(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        var messages = await _context.Messages
            .Include(m => m.Event)
            .Where(m => m.UserId == id)
            .OrderByDescending(m => m.CreatedOn)
            .ToListAsync();

        ViewBag.Messages  = messages;
        ViewBag.IsAdmin   = await _userManager.IsInRoleAsync(user, "Admin");
        return View(user);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.Id == id)
        {
            TempData["Error"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(Users));
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Remove their messages and reservations first
        var msgs = _context.Messages.Where(m => m.UserId == id);
        _context.Messages.RemoveRange(msgs);

        var reservations = _context.Reservations.Where(r => r.UserId == id);
        _context.Reservations.RemoveRange(reservations);

        await _context.SaveChangesAsync();
        await _userManager.DeleteAsync(user);

        return RedirectToAction(nameof(Users));
    }

    // ================================================================
    // Trips manage
    // ================================================================
    public async Task<IActionResult> Trips()
    {
        var trips = await _context.Trips
            .Include(t => t.Event)
            .OrderByDescending(t => t.TravelDate)
            .ToListAsync();
        return View(trips);
    }

    public async Task<IActionResult> EditTrip(int id)
    {
        var trip = await _context.Trips.FirstOrDefaultAsync(t => t.Id == id);
        if (trip == null) return NotFound();
        ViewBag.UpcomingEvents = await _context.Events.OrderBy(e => e.EventDate).ToListAsync();
        return View(trip);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTrip(int id, Trip input)
    {
        if (id != input.Id) return BadRequest();
        var trip = await _context.Trips.FirstOrDefaultAsync(t => t.Id == id);
        if (trip == null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewBag.UpcomingEvents = await _context.Events.OrderBy(e => e.EventDate).ToListAsync();
            return View(input);
        }

        trip.Title          = input.Title;
        trip.FromLocation   = input.FromLocation;
        trip.ToLocation     = input.ToLocation;
        trip.TravelDate     = input.TravelDate;
        trip.AvailableSeats = input.AvailableSeats;
        trip.Price          = input.Price;
        trip.EventId        = input.EventId;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Trips));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTrip(int id)
    {
        var trip = await _context.Trips
            .Include(t => t.Reservations)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (trip == null) return NotFound();

        _context.Reservations.RemoveRange(trip.Reservations);
        _context.Trips.Remove(trip);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Trips));
    }

    // ================================================================
    // Events manage
    // ================================================================
    public async Task<IActionResult> Events()
    {
        var events = await _context.Events
            .OrderByDescending(e => e.EventDate)
            .ToListAsync();

        var tripCounts = await _context.Trips
            .Where(t => t.EventId != null)
            .GroupBy(t => t.EventId!.Value)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count);

        ViewBag.TripCounts = tripCounts;
        return View(events);
    }

    public async Task<IActionResult> EditEvent(int id)
    {
        var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null) return NotFound();
        return View(ev);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEvent(int id, Event input)
    {
        if (id != input.Id) return BadRequest();
        var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null) return NotFound();
        if (!ModelState.IsValid) return View(input);

        ev.Title       = input.Title;
        ev.Description = input.Description;
        ev.Location    = input.Location;
        ev.EventDate   = input.EventDate;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Events));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null) return NotFound();
        _context.Events.Remove(ev);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Events));
    }

    // ================================================================
    // Chat (Messages) manage
    // ================================================================
    public async Task<IActionResult> Chat()
    {
        var messages = await _context.Messages
            .Include(m => m.User)
            .Include(m => m.Event)
            .OrderByDescending(m => m.CreatedOn)
            .Take(200)
            .ToListAsync();
        return View(messages);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMessage(int id)
    {
        var msg = await _context.Messages.FirstOrDefaultAsync(m => m.Id == id);
        if (msg == null) return NotFound();
        _context.Messages.Remove(msg);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Chat));
    }
}
