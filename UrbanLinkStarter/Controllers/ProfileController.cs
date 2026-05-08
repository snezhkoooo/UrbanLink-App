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

    public async Task<IActionResult> View(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        var isSelf = currentUser != null && currentUser.Id == user.Id;
        var publicMessageCount = await _context.Messages.CountAsync(m => m.UserId == id && m.EventId == null);
        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

        ViewBag.IsSelf        = isSelf;
        ViewBag.IsAdmin       = isAdmin;
        ViewBag.MessagesCount = publicMessageCount;
        ViewBag.TripsCount    = await _context.Trips.CountAsync(t => t.DriverId == id);
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
