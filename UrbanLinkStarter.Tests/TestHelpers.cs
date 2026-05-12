using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Moq;
using UrbanLinkStarter.Data;
using UrbanLinkStarter.Models;

namespace UrbanLinkStarter.Tests;

/// <summary>
/// Shared helpers: in-memory DbContext factory, fake UserManager, and a stub controller context
/// with TempData wired up so controllers that call TempData["..."] don't NRE.
/// </summary>
internal static class TestHelpers
{
    public static ApplicationDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // The InMemory provider throws on transactional warnings we don't care about here.
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    public static ApplicationUser MakeUser(
        string id,
        string fullName = "Test User",
        bool isVerified = false,
        bool isDriver = false,
        int age = 25)
        => new()
        {
            Id = id,
            UserName = $"{id}@test.local",
            Email    = $"{id}@test.local",
            FullName = fullName,
            Age      = age,
            IsVerified = isVerified,
            IsDriver   = isDriver
        };

    /// <summary>
    /// Builds a UserManager mock where GetUserAsync returns the given user, FindByIdAsync
    /// resolves any user that has been seeded into the DbContext, and role checks default to false.
    /// </summary>
    public static UserManager<ApplicationUser> FakeUserManager(
        ApplicationUser? currentUser,
        ApplicationDbContext? db = null,
        bool isAdmin = false)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        mgr.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(currentUser);
        mgr.Setup(m => m.IsInRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>())).ReturnsAsync(isAdmin);
        if (db != null)
        {
            mgr.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((string id) => db.Users.FirstOrDefault(u => u.Id == id));
        }
        else if (currentUser != null)
        {
            mgr.Setup(m => m.FindByIdAsync(currentUser.Id)).ReturnsAsync(currentUser);
        }
        return mgr.Object;
    }

    /// <summary>
    /// Hands the controller a ControllerContext with a real (in-memory) TempData and an HttpContext,
    /// so action methods that call TempData["..."] or ControllerContext.HttpContext.User don't crash.
    /// </summary>
    public static void AttachContext(this Controller controller, ApplicationUser? signedInAs = null)
    {
        var http = new DefaultHttpContext();
        if (signedInAs != null)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, signedInAs.Id),
                new Claim(ClaimTypes.Name, signedInAs.UserName ?? signedInAs.Id),
            }, authenticationType: "Test"));
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = http,
            RouteData = new RouteData(),
            ActionDescriptor = new ControllerActionDescriptor()
        };
        controller.TempData = new TempDataDictionary(http, new Mock<ITempDataProvider>().Object);
    }
}
