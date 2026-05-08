using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanLinkStarter.Data;
using UrbanLinkStarter.Models;

namespace UrbanLinkStarter.Controllers;

public class EventsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public EventsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> Index()
    {
        var now = DateTime.Now;
        var until = now.AddDays(14);
        var events = await _context.Events
            .Where(e => e.EventDate >= now && e.EventDate <= until)
            .OrderBy(e => e.EventDate)
            .ToListAsync();
        return View(events);
    }

    public async Task<IActionResult> Details(int id)
    {
        var ev = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);
        if (ev == null) return NotFound();

        var messages = await _context.Messages
            .Include(m => m.User)
            .Where(m => m.EventId == id)
            .OrderByDescending(m => m.CreatedOn)
            .Take(50)
            .ToListAsync();

        var trips = await _context.Trips
            .Include(t => t.Driver)
            .Where(t => t.EventId == id)
            .OrderBy(t => t.TravelDate)
            .ToListAsync();

        ViewBag.Messages = messages;
        ViewBag.Trips = trips;
        ViewBag.AdminUserIds = await GetAdminUserIdsAsync();
        return View(ev);
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> PostMessage(int id, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return RedirectToAction(nameof(Details), new { id });

        var ev = await _context.Events.FindAsync(id);
        if (ev == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        _context.Messages.Add(new Message
        {
            Content = content,
            UserId  = user.Id,
            EventId = id
        });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Create() => View();

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(Event model)
    {
        if (!ModelState.IsValid) return View(model);
        _context.Events.Add(model);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<HashSet<string>> GetAdminUserIdsAsync()
    {
        var adminRole = await _roleManager.FindByNameAsync("Admin");
        if (adminRole == null) return new HashSet<string>();
        var userIds = await _context.UserRoles
            .Where(ur => ur.RoleId == adminRole.Id)
            .Select(ur => ur.UserId)
            .ToListAsync();
        return new HashSet<string>(userIds);
    }
}
