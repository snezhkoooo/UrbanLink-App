using Microsoft.AspNetCore.Mvc;
using UrbanLinkStarter.Controllers;
using UrbanLinkStarter.Models;
using Xunit;

namespace UrbanLinkStarter.Tests;

public class MessagesControllerTests
{
    [Fact]
    public async Task Send_requires_age_verification()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice", isVerified: false);
        var bob   = TestHelpers.MakeUser("bob",   isVerified: true);
        db.Users.AddRange(alice, bob);
        await db.SaveChangesAsync();

        var c = new MessagesController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        var result = await c.Send(bob.Id, "hi");

        // Unverified senders are bounced to Verification, no row is created.
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Verification", redirect.ControllerName);
        Assert.Empty(db.DirectMessages);
    }

    [Fact]
    public async Task Send_writes_a_direct_message_when_age_verified()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice", isVerified: true);
        var bob   = TestHelpers.MakeUser("bob",   isVerified: true);
        db.Users.AddRange(alice, bob);
        await db.SaveChangesAsync();

        var c = new MessagesController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        await c.Send(bob.Id, "hello there");

        var dm = Assert.Single(db.DirectMessages);
        Assert.Equal(alice.Id, dm.FromUserId);
        Assert.Equal(bob.Id,   dm.ToUserId);
        Assert.Equal("hello there", dm.Body);
        Assert.Null(dm.ReadOn);
    }

    [Fact]
    public async Task Send_trims_and_truncates_long_bodies_to_1000_chars()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice", isVerified: true);
        var bob   = TestHelpers.MakeUser("bob",   isVerified: true);
        db.Users.AddRange(alice, bob);
        await db.SaveChangesAsync();

        var c = new MessagesController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        await c.Send(bob.Id, "  " + new string('x', 1500) + "  ");

        var dm = Assert.Single(db.DirectMessages);
        Assert.Equal(1000, dm.Body.Length);
        Assert.Equal('x', dm.Body[0]); // leading whitespace trimmed
    }

    [Fact]
    public async Task Thread_marks_incoming_messages_as_read()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice", isVerified: true);
        var bob   = TestHelpers.MakeUser("bob",   isVerified: true);
        db.Users.AddRange(alice, bob);
        db.DirectMessages.AddRange(
            new DirectMessage { FromUserId = bob.Id,   ToUserId = alice.Id, Body = "1", SentOn = DateTime.UtcNow.AddMinutes(-2) },
            new DirectMessage { FromUserId = bob.Id,   ToUserId = alice.Id, Body = "2", SentOn = DateTime.UtcNow.AddMinutes(-1) },
            new DirectMessage { FromUserId = alice.Id, ToUserId = bob.Id,   Body = "3", SentOn = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var c = new MessagesController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        await c.Thread(bob.Id);

        // Only the two messages addressed to Alice should have ReadOn set.
        Assert.All(db.DirectMessages.Where(d => d.ToUserId == alice.Id), d => Assert.NotNull(d.ReadOn));
        Assert.Null(db.DirectMessages.Single(d => d.ToUserId == bob.Id).ReadOn);
    }
}
