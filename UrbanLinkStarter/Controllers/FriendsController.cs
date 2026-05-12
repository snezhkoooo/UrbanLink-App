using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanLinkStarter.Data;
using UrbanLinkStarter.Models;

namespace UrbanLinkStarter.Controllers;

[Authorize]
public class FriendsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public FriendsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // Mark all incoming pending requests as seen — clears the badge / popup.
        var pending = await _context.Friendships
            .Where(f => f.AddresseeId == user.Id && f.Status == FriendshipStatus.Pending && !f.SeenByAddressee)
            .ToListAsync();
        foreach (var p in pending) p.SeenByAddressee = true;
        await _context.SaveChangesAsync();

        ViewBag.Incoming = await _context.Friendships
            .Include(f => f.Requester)
            .Where(f => f.AddresseeId == user.Id && f.Status == FriendshipStatus.Pending)
            .OrderByDescending(f => f.CreatedOn)
            .ToListAsync();

        ViewBag.Outgoing = await _context.Friendships
            .Include(f => f.Addressee)
            .Where(f => f.RequesterId == user.Id && f.Status == FriendshipStatus.Pending)
            .OrderByDescending(f => f.CreatedOn)
            .ToListAsync();

        // Friends: accepted in either direction
        var accepted = await _context.Friendships
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .Where(f => f.Status == FriendshipStatus.Accepted &&
                        (f.RequesterId == user.Id || f.AddresseeId == user.Id))
            .OrderByDescending(f => f.RespondedOn ?? f.CreatedOn)
            .ToListAsync();

        var friends = accepted.Select(f => f.RequesterId == user.Id ? f.Addressee! : f.Requester!).ToList();
        return View(friends);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendRequest(string targetUserId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();
        if (string.IsNullOrEmpty(targetUserId) || targetUserId == user.Id)
        {
            TempData["Error"] = "Invalid target user.";
            return RedirectToAction(nameof(Index));
        }

        var target = await _userManager.FindByIdAsync(targetUserId);
        if (target == null) return NotFound();

        var existing = await _context.Friendships
            .FirstOrDefaultAsync(f =>
                (f.RequesterId == user.Id && f.AddresseeId == targetUserId) ||
                (f.RequesterId == targetUserId && f.AddresseeId == user.Id));

        if (existing != null)
        {
            TempData["Error"] = existing.Status == FriendshipStatus.Accepted
                ? "You are already friends."
                : "A friend request already exists between you.";
            return RedirectToAction("View", "Profile", new { id = targetUserId });
        }

        _context.Friendships.Add(new Friendship
        {
            RequesterId = user.Id,
            AddresseeId = targetUserId,
            Status = FriendshipStatus.Pending,
            CreatedOn = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        TempData["Success"] = "Friend request sent.";
        return RedirectToAction("View", "Profile", new { id = targetUserId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var fr = await _context.Friendships.FirstOrDefaultAsync(f => f.Id == id);
        if (fr == null || fr.AddresseeId != user.Id) return NotFound();

        fr.Status = FriendshipStatus.Accepted;
        fr.RespondedOn = DateTime.UtcNow;
        fr.SeenByAddressee = true;

        // Auto-seed a friendly "say hi" thread starter so the new chat isn't empty.
        // System-style message — comes from the requester (so the addressee sees it as incoming)
        // and is also visible to the requester in their thread.
        var hasMessages = await _context.DirectMessages.AnyAsync(d =>
            (d.FromUserId == fr.RequesterId && d.ToUserId == fr.AddresseeId) ||
            (d.FromUserId == fr.AddresseeId && d.ToUserId == fr.RequesterId));
        if (!hasMessages)
        {
            _context.DirectMessages.Add(new DirectMessage
            {
                FromUserId = fr.RequesterId,
                ToUserId   = fr.AddresseeId,
                Body       = "👋 You're now friends — say hi!",
                SentOn     = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return RedirectToAction("Thread", "Messages", new { id = fr.RequesterId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var fr = await _context.Friendships.FirstOrDefaultAsync(f => f.Id == id);
        if (fr == null || fr.AddresseeId != user.Id) return NotFound();

        // Remove the row so the user can re-send later if they want.
        _context.Friendships.Remove(fr);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var fr = await _context.Friendships.FirstOrDefaultAsync(f =>
            f.Status == FriendshipStatus.Accepted &&
            ((f.RequesterId == user.Id && f.AddresseeId == id) ||
             (f.RequesterId == id && f.AddresseeId == user.Id)));
        if (fr == null) return NotFound();

        _context.Friendships.Remove(fr);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Marks a request as seen so the toast popup does not show again on next page load.</summary>
    [HttpPost]
    public async Task<IActionResult> MarkSeen(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var fr = await _context.Friendships.FirstOrDefaultAsync(f => f.Id == id && f.AddresseeId == user.Id);
        if (fr != null)
        {
            fr.SeenByAddressee = true;
            await _context.SaveChangesAsync();
        }
        return Ok();
    }
}
