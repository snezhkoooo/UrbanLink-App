using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanLinkStarter.Data;
using UrbanLinkStarter.Models;

namespace UrbanLinkStarter.Controllers;

[Authorize]
public class VerificationController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public VerificationController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var existing = await _context.VerificationRequests
            .Where(v => v.UserId == user.Id)
            .OrderByDescending(v => v.CreatedOn)
            .FirstOrDefaultAsync();

        ViewBag.Existing = existing;
        ViewBag.IsVerified = user.IsVerified;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> Submit(DocumentType documentType, IFormFile? document)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (document == null || document.Length == 0)
        {
            TempData["Error"] = "Please upload a document image.";
            return RedirectToAction(nameof(Index));
        }

        var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowed.Contains(document.ContentType.ToLower()))
        {
            TempData["Error"] = "Only JPEG, PNG, or WebP images are allowed.";
            return RedirectToAction(nameof(Index));
        }

        using var ms = new MemoryStream();
        await document.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        var dataUri = $"data:{document.ContentType};base64,{base64}";

        // Replace any existing pending request from this user.
        var existing = await _context.VerificationRequests
            .Where(v => v.UserId == user.Id && v.Status == VerificationStatus.Pending)
            .FirstOrDefaultAsync();
        if (existing != null) _context.VerificationRequests.Remove(existing);

        _context.VerificationRequests.Add(new VerificationRequest
        {
            UserId = user.Id,
            DocumentType = documentType,
            DocumentImage = dataUri,
            Status = VerificationStatus.Pending,
            CreatedOn = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        TempData["Success"] = "Your document was submitted. An admin will review it shortly.";
        return RedirectToAction(nameof(Index));
    }
}
