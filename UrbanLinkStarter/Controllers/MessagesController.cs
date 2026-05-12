using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanLinkStarter.Data;
using UrbanLinkStarter.Models;

namespace UrbanLinkStarter.Controllers;

[Authorize]
public class MessagesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public MessagesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>Inbox — list of users you have a DM conversation with, newest first.</summary>
    public async Task<IActionResult> Index()
    {
        var me = await _userManager.GetUserAsync(User);
        if (me == null) return Challenge();

        // Pull all DMs that involve me, then collapse to a conversation list.
        var dms = await _context.DirectMessages
            .Include(d => d.FromUser)
            .Include(d => d.ToUser)
            .Where(d => d.FromUserId == me.Id || d.ToUserId == me.Id)
            .OrderByDescending(d => d.SentOn)
            .ToListAsync();

        var conversations = dms
            .GroupBy(d => d.FromUserId == me.Id ? d.ToUserId : d.FromUserId)
            .Select(g => new
            {
                Other = g.First().FromUserId == me.Id ? g.First().ToUser! : g.First().FromUser!,
                LastMessage = g.First(),
                UnreadCount = g.Count(x => x.ToUserId == me.Id && x.ReadOn == null)
            })
            .ToList();

        ViewBag.Conversations = conversations;
        return View();
    }

    /// <summary>Open the thread with a specific user.</summary>
    public async Task<IActionResult> Thread(string id)
    {
        var me = await _userManager.GetUserAsync(User);
        if (me == null) return Challenge();
        if (string.IsNullOrEmpty(id) || id == me.Id) return NotFound();

        var other = await _userManager.FindByIdAsync(id);
        if (other == null) return NotFound();

        var messages = await _context.DirectMessages
            .Where(d => (d.FromUserId == me.Id && d.ToUserId == id) ||
                        (d.FromUserId == id   && d.ToUserId == me.Id))
            .OrderBy(d => d.SentOn)
            .ToListAsync();

        // Mark incoming as read
        var unread = messages.Where(d => d.ToUserId == me.Id && d.ReadOn == null).ToList();
        foreach (var u in unread) u.ReadOn = DateTime.UtcNow;
        if (unread.Count > 0) await _context.SaveChangesAsync();

        ViewBag.Other = other;
        ViewBag.Me = me;
        return View(messages);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string toUserId, string body)
    {
        var me = await _userManager.GetUserAsync(User);
        if (me == null) return Challenge();

        // Age verification gate for sending DMs.
        if (!me.IsVerified)
        {
            TempData["Error"] = "You must complete age verification before sending direct messages.";
            return RedirectToAction("Index", "Verification");
        }

        if (string.IsNullOrEmpty(toUserId) || toUserId == me.Id || string.IsNullOrWhiteSpace(body))
            return RedirectToAction(nameof(Thread), new { id = toUserId });

        body = body.Trim();
        if (body.Length > 1000) body = body[..1000];

        _context.DirectMessages.Add(new DirectMessage
        {
            FromUserId = me.Id,
            ToUserId   = toUserId,
            Body       = body,
            SentOn     = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Thread), new { id = toUserId });
    }
}
