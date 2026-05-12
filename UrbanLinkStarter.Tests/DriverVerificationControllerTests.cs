using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UrbanLinkStarter.Controllers;
using UrbanLinkStarter.Models;
using Xunit;

namespace UrbanLinkStarter.Tests;

public class DriverVerificationControllerTests
{
    private static IFormFile FakeImage(string contentType = "image/png", string name = "x.png")
    {
        var bytes = Encoding.UTF8.GetBytes("not-real-pixels-but-non-empty");
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, name, name) { Headers = new HeaderDictionary(), ContentType = contentType };
    }

    [Fact]
    public async Task Submit_rejects_when_gdpr_consent_is_missing()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice");
        db.Users.Add(alice);
        await db.SaveChangesAsync();

        var c = new DriverVerificationController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        var result = await c.Submit(FakeImage(), FakeImage(), FakeImage(), gdprConsent: false);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(db.DriverVerificationRequests);
        Assert.NotNull(c.TempData["Error"]);
    }

    [Fact]
    public async Task Submit_rejects_when_a_photo_is_missing()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice");
        db.Users.Add(alice);
        await db.SaveChangesAsync();

        var c = new DriverVerificationController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        // License photo omitted -> reject.
        var result = await c.Submit(FakeImage(), FakeImage(), null, gdprConsent: true);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(db.DriverVerificationRequests);
        Assert.NotNull(c.TempData["Error"]);
    }

    [Fact]
    public async Task Submit_rejects_non_image_content_types()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice");
        db.Users.Add(alice);
        await db.SaveChangesAsync();

        var c = new DriverVerificationController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        var result = await c.Submit(FakeImage("image/png"), FakeImage("application/pdf"), FakeImage("image/png"), gdprConsent: true);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(db.DriverVerificationRequests);
    }

    [Fact]
    public async Task Submit_stores_pending_request_with_base64_data_uris()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice", isVerified: true);
        db.Users.Add(alice);
        await db.SaveChangesAsync();

        var c = new DriverVerificationController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        await c.Submit(FakeImage(), FakeImage(), FakeImage(), gdprConsent: true);

        var req = Assert.Single(db.DriverVerificationRequests);
        Assert.Equal(alice.Id, req.UserId);
        Assert.Equal(VerificationStatus.Pending, req.Status);
        Assert.True(req.GdprConsent);
        Assert.StartsWith("data:image/png;base64,", req.CarPhoto);
        Assert.StartsWith("data:image/png;base64,", req.CarInteriorPhoto);
        Assert.StartsWith("data:image/png;base64,", req.LicensePhoto);
    }

    [Fact]
    public async Task Submit_replaces_existing_pending_request()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice", isVerified: true);
        db.Users.Add(alice);
        db.DriverVerificationRequests.Add(new DriverVerificationRequest
        {
            UserId = alice.Id,
            CarPhoto = "old", CarInteriorPhoto = "old", LicensePhoto = "old",
            Status = VerificationStatus.Pending
        });
        await db.SaveChangesAsync();

        var c = new DriverVerificationController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        await c.Submit(FakeImage(), FakeImage(), FakeImage(), gdprConsent: true);

        var req = Assert.Single(db.DriverVerificationRequests);
        Assert.NotEqual("old", req.CarPhoto); // the new submission overwrote the old one
    }

    [Fact]
    public async Task Submit_redirects_unverified_user_to_age_verification()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice", isVerified: false);
        db.Users.Add(alice);
        await db.SaveChangesAsync();

        var c = new DriverVerificationController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        var result = await c.Submit(FakeImage(), FakeImage(), FakeImage(), gdprConsent: true);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Verification", redirect.ControllerName);
        Assert.Empty(db.DriverVerificationRequests);
        Assert.NotNull(c.TempData["Error"]);
    }
}
