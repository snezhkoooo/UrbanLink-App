using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanLinkStarter.Data;
using UrbanLinkStarter.Models;

namespace UrbanLinkStarter.Controllers;

public class ProfileController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [Authorize]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        return RedirectToAction(nameof(View), new { id = user.Id });
    }

    public new async Task<IActionResult> View(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        var isSelf = currentUser != null && currentUser.Id == user.Id;
        var publicMessageCount = await _context.Messages.CountAsync(m => m.UserId == id && m.EventId == null);
        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

        // Friend count (accepted friendships in either direction)
        var friendCount = await _context.Friendships.CountAsync(f =>
            f.Status == FriendshipStatus.Accepted &&
            (f.RequesterId == id || f.AddresseeId == id));

        // Past rides — completed trips this user drove
        var pastRides = await _context.Trips
            .Where(t => t.DriverId == id && t.IsCompleted)
            .OrderByDescending(t => t.CompletedAt ?? t.TravelDate)
            .Take(8)
            .ToListAsync();

        // Friendship status relative to current user
        string friendStatus = "none"; // none | self | friends | pending_outgoing | pending_incoming
        int? incomingRequestId = null;
        if (currentUser != null && !isSelf)
        {
            var rel = await _context.Friendships
                .FirstOrDefaultAsync(f =>
                    (f.RequesterId == currentUser.Id && f.AddresseeId == id) ||
                    (f.RequesterId == id && f.AddresseeId == currentUser.Id));
            if (rel != null)
            {
                if (rel.Status == FriendshipStatus.Accepted) friendStatus = "friends";
                else if (rel.RequesterId == currentUser.Id)  friendStatus = "pending_outgoing";
                else { friendStatus = "pending_incoming"; incomingRequestId = rel.Id; }
            }
        }
        else if (isSelf) friendStatus = "self";

        ViewBag.IsSelf        = isSelf;
        ViewBag.IsAdmin       = isAdmin;
        ViewBag.MessagesCount = publicMessageCount;
        ViewBag.TripsCount    = await _context.Trips.CountAsync(t => t.DriverId == id);
        ViewBag.FriendCount   = friendCount;
        ViewBag.PastRides     = pastRides;
        ViewBag.FriendStatus  = friendStatus;
        ViewBag.IncomingRequestId = incomingRequestId;
        return View(user);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        return View(user);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string fullName, string? bio)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        user.FullName = fullName ?? user.FullName;
        user.Bio      = bio;
        await _userManager.UpdateAsync(user);
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)] // 5 MB max
    public async Task<IActionResult> UploadPicture(IFormFile? picture)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (picture == null || picture.Length == 0)
        {
            TempData["Error"] = "No file selected.";
            return RedirectToAction(nameof(Index));
        }

        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
        if (!allowed.Contains(picture.ContentType.ToLower()))
        {
            TempData["Error"] = "Only JPEG, PNG, WebP or GIF images are allowed.";
            return RedirectToAction(nameof(Index));
        }

        using var ms = new MemoryStream();
        await picture.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        user.ProfilePicture = $"data:{picture.ContentType};base64,{base64}";

        await _userManager.UpdateAsync(user);
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePicture()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        user.ProfilePicture = null;
        await _userManager.UpdateAsync(user);
        return RedirectToAction(nameof(Index));
    }
}
