using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanLinkStarter.Data;
using UrbanLinkStarter.Models;

namespace UrbanLinkStarter.Controllers;

public class ChatController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public ChatController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> Index()
    {
        // Top-level community messages only
        var messages = await _context.Messages
            .Include(m => m.User)
            .Where(m => m.EventId == null && m.ParentId == null)
            .OrderByDescending(m => m.CreatedOn)
            .Take(50)
            .ToListAsync();

        // Load replies for those messages
        var msgIds = messages.Select(m => m.Id).ToList();
        var replies = await _context.Messages
            .Include(m => m.User)
            .Where(m => m.ParentId != null && msgIds.Contains(m.ParentId!.Value))
            .OrderBy(m => m.CreatedOn)
            .ToListAsync();

        var likes = await _context.MessageLikes.ToListAsync();
        var currentUser = await _userManager.GetUserAsync(User);

        ViewBag.AdminUserIds = await GetAdminUserIdsAsync();
        ViewBag.Likes = likes.GroupBy(l => l.MessageId).ToDictionary(g => g.Key, g => g.ToList());
        ViewBag.CurrentUserId = currentUser?.Id ?? "";
        ViewBag.Replies = replies.GroupBy(r => r.ParentId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        // Check if this user has any completed trips where they haven't rated the driver yet
        ViewBag.PendingRatingTripId = null;
        if (currentUser != null)
        {
            var pendingRating = await _context.Reservations
                .Include(r => r.Trip)
                .Where(r => r.UserId == currentUser.Id
                    && r.Trip != null
                    && r.Trip.IsCompleted
                    && r.Trip.DriverId != currentUser.Id)
                .Select(r => r.Trip)
                .Where(t => !_context.DriverRatings.Any(dr => dr.TripId == t!.Id && dr.RaterId == currentUser.Id))
                .FirstOrDefaultAsync();

            ViewBag.PendingRatingTrip = pendingRating;
        }

        return View(messages);
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> Create(string? content, IFormFile? image, int? parentId = null)
    {
        if (string.IsNullOrWhiteSpace(content) && image == null)
            return RedirectToAction(nameof(Index));

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!user.IsVerified)
        {
            TempData["Error"] = "You must complete age verification before posting in chat.";
            return RedirectToAction("Index", "Verification");
        }

        string? imageData = null;
        if (image != null && image.Length > 0 && parentId == null) // images only in top-level posts
        {
            var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
            if (allowed.Contains(image.ContentType.ToLower()))
            {
                using var ms = new MemoryStream();
                await image.CopyToAsync(ms);
                imageData = $"data:{image.ContentType};base64,{Convert.ToBase64String(ms.ToArray())}";
            }
        }

        // Validate parent exists and is a community message
        int? validParentId = null;
        if (parentId.HasValue)
        {
            var parent = await _context.Messages.FindAsync(parentId.Value);
            if (parent != null && parent.EventId == null && parent.ParentId == null)
                validParentId = parentId.Value; // only one level deep
        }

        _context.Messages.Add(new Message
        {
            Content   = content?.Trim() ?? "",
            ImageData = imageData,
            UserId    = user.Id,
            EventId   = null,
            ParentId  = validParentId
        });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // ====== Delete own message ======
    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMessage(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        var msg = await _context.Messages.FirstOrDefaultAsync(m => m.Id == id);
        if (msg == null) return NotFound();

        // Only owner or admin can delete
        if (msg.UserId != user?.Id && !User.IsInRole("Admin"))
            return Forbid();

        _context.Messages.Remove(msg);
        await _context.SaveChangesAsync();

        var returnUrl = Request.Headers["Referer"].ToString();
        if (returnUrl.Contains("MyMessages")) return RedirectToAction(nameof(MyMessages));
        return RedirectToAction(nameof(Index));
    }

    // ====== My Messages page ======
    [Authorize]
    public async Task<IActionResult> MyMessages()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var messages = await _context.Messages
            .Include(m => m.User)
            .Where(m => m.UserId == user.Id && m.EventId == null)
            .OrderByDescending(m => m.CreatedOn)
            .ToListAsync();

        return View(messages);
    }

    // ====== Toggle like/dislike (AJAX) ======
    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLike(int messageId, bool isLike)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var existing = await _context.MessageLikes
            .FirstOrDefaultAsync(l => l.MessageId == messageId && l.UserId == user.Id);

        if (existing != null)
        {
            if (existing.IsLike == isLike)
            {
                // Toggle off (remove reaction)
                _context.MessageLikes.Remove(existing);
            }
            else
            {
                // Switch reaction
                existing.IsLike = isLike;
            }
        }
        else
        {
            _context.MessageLikes.Add(new MessageLike { MessageId = messageId, UserId = user.Id, IsLike = isLike });
        }

        await _context.SaveChangesAsync();

        var likes    = await _context.MessageLikes.CountAsync(l => l.MessageId == messageId && l.IsLike);
        var dislikes = await _context.MessageLikes.CountAsync(l => l.MessageId == messageId && !l.IsLike);
        var userReaction = await _context.MessageLikes
            .Where(l => l.MessageId == messageId && l.UserId == user.Id)
            .Select(l => (bool?)l.IsLike)
            .FirstOrDefaultAsync();

        return Json(new { likes, dislikes, userReaction });
    }

    private async Task<HashSet<string>> GetAdminUserIdsAsync()
    {
        var adminRole = await _roleManager.FindByNameAsync("Admin");
        if (adminRole == null) return new HashSet<string>();
        var ids = await _context.UserRoles.Where(ur => ur.RoleId == adminRole.Id).Select(ur => ur.UserId).ToListAsync();
        return new HashSet<string>(ids);
    }
}
