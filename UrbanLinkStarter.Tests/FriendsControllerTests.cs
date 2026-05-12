using Microsoft.AspNetCore.Mvc;
using UrbanLinkStarter.Controllers;
using UrbanLinkStarter.Models;
using Xunit;

namespace UrbanLinkStarter.Tests;

public class FriendsControllerTests
{
    [Fact]
    public async Task SendRequest_creates_pending_friendship()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice");
        var bob   = TestHelpers.MakeUser("bob");
        db.Users.AddRange(alice, bob);
        await db.SaveChangesAsync();

        var c = new FriendsController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        var result = await c.SendRequest(bob.Id);

        Assert.IsType<RedirectToActionResult>(result);
        var fr = Assert.Single(db.Friendships);
        Assert.Equal(alice.Id, fr.RequesterId);
        Assert.Equal(bob.Id,   fr.AddresseeId);
        Assert.Equal(FriendshipStatus.Pending, fr.Status);
        Assert.False(fr.SeenByAddressee);
    }

    [Fact]
    public async Task SendRequest_to_self_is_rejected()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice");
        db.Users.Add(alice);
        await db.SaveChangesAsync();

        var c = new FriendsController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        await c.SendRequest(alice.Id);

        Assert.Empty(db.Friendships);
        Assert.NotNull(c.TempData["Error"]);
    }

    [Fact]
    public async Task SendRequest_does_not_duplicate_existing_friendship()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice");
        var bob   = TestHelpers.MakeUser("bob");
        db.Users.AddRange(alice, bob);
        db.Friendships.Add(new Friendship
        {
            RequesterId = alice.Id, AddresseeId = bob.Id, Status = FriendshipStatus.Pending
        });
        await db.SaveChangesAsync();

        var c = new FriendsController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        await c.SendRequest(bob.Id);

        Assert.Single(db.Friendships);
        Assert.NotNull(c.TempData["Error"]);
    }

    [Fact]
    public async Task Accept_only_works_for_addressee()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice");
        var bob   = TestHelpers.MakeUser("bob");
        var eve   = TestHelpers.MakeUser("eve"); // unrelated third party
        db.Users.AddRange(alice, bob, eve);
        var fr = new Friendship { RequesterId = alice.Id, AddresseeId = bob.Id, Status = FriendshipStatus.Pending };
        db.Friendships.Add(fr);
        await db.SaveChangesAsync();

        // Eve (not the addressee) tries to accept — should 404.
        var asEve = new FriendsController(db, TestHelpers.FakeUserManager(eve, db));
        asEve.AttachContext(eve);
        var result = await asEve.Accept(fr.Id);
        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(FriendshipStatus.Pending, db.Friendships.Single().Status);

        // Bob (the addressee) accepts — should succeed.
        var asBob = new FriendsController(db, TestHelpers.FakeUserManager(bob, db));
        asBob.AttachContext(bob);
        await asBob.Accept(fr.Id);
        var after = db.Friendships.Single();
        Assert.Equal(FriendshipStatus.Accepted, after.Status);
        Assert.True(after.SeenByAddressee);
        Assert.NotNull(after.RespondedOn);
    }

    [Fact]
    public async Task Reject_removes_the_friendship_row()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice");
        var bob   = TestHelpers.MakeUser("bob");
        db.Users.AddRange(alice, bob);
        var fr = new Friendship { RequesterId = alice.Id, AddresseeId = bob.Id, Status = FriendshipStatus.Pending };
        db.Friendships.Add(fr);
        await db.SaveChangesAsync();

        var c = new FriendsController(db, TestHelpers.FakeUserManager(bob, db));
        c.AttachContext(bob);

        await c.Reject(fr.Id);

        Assert.Empty(db.Friendships);
    }

    [Fact]
    public async Task Index_marks_incoming_requests_as_seen()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice");
        var bob   = TestHelpers.MakeUser("bob");
        db.Users.AddRange(alice, bob);
        db.Friendships.Add(new Friendship
        {
            RequesterId = alice.Id, AddresseeId = bob.Id,
            Status = FriendshipStatus.Pending, SeenByAddressee = false
        });
        await db.SaveChangesAsync();

        var c = new FriendsController(db, TestHelpers.FakeUserManager(bob, db));
        c.AttachContext(bob);
        await c.Index();

        Assert.True(db.Friendships.Single().SeenByAddressee);
    }

    [Fact]
    public async Task Remove_drops_accepted_friendship_regardless_of_direction()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice");
        var bob   = TestHelpers.MakeUser("bob");
        db.Users.AddRange(alice, bob);
        // Friendship rows are unordered: bob sent the original request, alice accepted.
        db.Friendships.Add(new Friendship
        {
            RequesterId = bob.Id, AddresseeId = alice.Id, Status = FriendshipStatus.Accepted
        });
        await db.SaveChangesAsync();

        var c = new FriendsController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        await c.Remove(bob.Id);

        Assert.Empty(db.Friendships);
    }

    [Fact]
    public async Task MarkSeen_only_marks_when_addressee_matches()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice");
        var bob   = TestHelpers.MakeUser("bob");
        var eve   = TestHelpers.MakeUser("eve");
        db.Users.AddRange(alice, bob, eve);
        var fr = new Friendship { RequesterId = alice.Id, AddresseeId = bob.Id, Status = FriendshipStatus.Pending };
        db.Friendships.Add(fr);
        await db.SaveChangesAsync();

        // Eve isn't the addressee — should NOT mark seen, but still returns Ok.
        var asEve = new FriendsController(db, TestHelpers.FakeUserManager(eve, db));
        asEve.AttachContext(eve);
        await asEve.MarkSeen(fr.Id);
        Assert.False(db.Friendships.Single().SeenByAddressee);

        // Bob (the addressee) — should mark seen.
        var asBob = new FriendsController(db, TestHelpers.FakeUserManager(bob, db));
        asBob.AttachContext(bob);
        await asBob.MarkSeen(fr.Id);
        Assert.True(db.Friendships.Single().SeenByAddressee);
    }
}
