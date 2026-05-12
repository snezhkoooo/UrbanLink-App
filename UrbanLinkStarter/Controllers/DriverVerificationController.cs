using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanLinkStarter.Data;
using UrbanLinkStarter.Models;

namespace UrbanLinkStarter.Controllers;

[Authorize]
public class DriverVerificationController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DriverVerificationController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        ViewBag.Existing = await _context.DriverVerificationRequests
            .Where(v => v.UserId == user.Id)
            .OrderByDescending(v => v.CreatedOn)
            .FirstOrDefaultAsync();
        ViewBag.IsDriver        = user.IsDriver;
        ViewBag.IsAgeVerified   = user.IsVerified;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(24 * 1024 * 1024)]
    public async Task<IActionResult> Submit(IFormFile? carPhoto, IFormFile? carInteriorPhoto, IFormFile? licensePhoto, bool gdprConsent)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!user.IsVerified)
        {
            TempData["Error"] = "Please verify your age before becoming a driver.";
            return RedirectToAction("Index", "Verification");
        }

        if (!gdprConsent)
        {
            TempData["Error"] = "You must agree to the GDPR consent to submit a driver verification.";
            return RedirectToAction(nameof(Index));
        }

        if (carPhoto == null || carInteriorPhoto == null || licensePhoto == null ||
            carPhoto.Length == 0 || carInteriorPhoto.Length == 0 || licensePhoto.Length == 0)
        {
            TempData["Error"] = "Please upload all three photos: car exterior, car interior, and driver's license.";
            return RedirectToAction(nameof(Index));
        }

        var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
        foreach (var f in new[] { carPhoto, carInteriorPhoto, licensePhoto })
        {
            if (!allowed.Contains(f.ContentType.ToLower()))
            {
                TempData["Error"] = "Only JPEG, PNG, or WebP images are allowed.";
                return RedirectToAction(nameof(Index));
            }
        }

        async Task<string> ToDataUri(IFormFile f)
        {
            using var ms = new MemoryStream();
            await f.CopyToAsync(ms);
            return $"data:{f.ContentType};base64,{Convert.ToBase64String(ms.ToArray())}";
        }

        // Replace any existing pending request.
        var existing = await _context.DriverVerificationRequests
            .Where(v => v.UserId == user.Id && v.Status == VerificationStatus.Pending)
            .FirstOrDefaultAsync();
        if (existing != null) _context.DriverVerificationRequests.Remove(existing);

        _context.DriverVerificationRequests.Add(new DriverVerificationRequest
        {
            UserId            = user.Id,
            CarPhoto          = await ToDataUri(carPhoto),
            CarInteriorPhoto  = await ToDataUri(carInteriorPhoto),
            LicensePhoto      = await ToDataUri(licensePhoto),
            GdprConsent       = true,
            Status            = VerificationStatus.Pending,
            CreatedOn         = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        TempData["Success"] = "Driver verification submitted. An admin will review your documents shortly.";
        return RedirectToAction(nameof(Index));
    }
}
