using Microsoft.AspNetCore.Mvc;
using UrbanLinkStarter.Controllers;
using UrbanLinkStarter.Models;
using Xunit;

namespace UrbanLinkStarter.Tests;

public class ReservationsControllerTests
{
    private static Trip SeedTrip(Data.ApplicationDbContext db, string driverId, int seats = 3)
    {
        var t = new Trip
        {
            Title = "Sofia → Plovdiv",
            FromLocation = "Sofia",
            ToLocation   = "Plovdiv",
            TravelDate   = DateTime.UtcNow.AddDays(1),
            AvailableSeats = seats,
            CarBrand = "VW", CarModel = "Golf", CarYear = 2018,
            FuelConsumption = 6.5,
            FuelType = FuelType.Diesel,
            Price = 10m,
            DriverId = driverId
        };
        db.Trips.Add(t);
        db.SaveChanges();
        return t;
    }

    [Fact]
    public async Task Create_redirects_unverified_user_to_verification()
    {
        using var db = TestHelpers.NewDb();
        var driver = TestHelpers.MakeUser("driver", isDriver: true);
        var passenger = TestHelpers.MakeUser("passenger", isVerified: false);
        db.Users.AddRange(driver, passenger);
        await db.SaveChangesAsync();
        var trip = SeedTrip(db, driver.Id);

        var c = new ReservationsController(db, TestHelpers.FakeUserManager(passenger, db));
        c.AttachContext(passenger);

        var result = await c.Create(trip.Id, PaymentMethod.Cash);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Verification", redirect.ControllerName);
        Assert.Empty(db.Reservations);
    }

    [Fact]
    public async Task Create_blocks_self_reservation_on_own_trip()
    {
        using var db = TestHelpers.NewDb();
        var driver = TestHelpers.MakeUser("driver", isDriver: true, isVerified: true);
        db.Users.Add(driver);
        await db.SaveChangesAsync();
        var trip = SeedTrip(db, driver.Id);

        var c = new ReservationsController(db, TestHelpers.FakeUserManager(driver, db));
        c.AttachContext(driver);

        await c.Create(trip.Id, PaymentMethod.Cash);

        Assert.Empty(db.Reservations);
        Assert.NotNull(c.TempData["Error"]);
    }

    [Fact]
    public async Task Create_succeeds_for_age_verified_passenger()
    {
        using var db = TestHelpers.NewDb();
        var driver    = TestHelpers.MakeUser("driver", isDriver: true);
        var passenger = TestHelpers.MakeUser("passenger", isVerified: true);
        db.Users.AddRange(driver, passenger);
        await db.SaveChangesAsync();
        var trip = SeedTrip(db, driver.Id, seats: 3);

        var c = new ReservationsController(db, TestHelpers.FakeUserManager(passenger, db));
        c.AttachContext(passenger);

        await c.Create(trip.Id, PaymentMethod.Cash, seatsCount: 2);

        var r = Assert.Single(db.Reservations);
        Assert.Equal(passenger.Id, r.UserId);
        Assert.Equal(trip.Id,      r.TripId);
        Assert.Equal(2,            r.SeatsCount);
    }

    [Fact]
    public async Task Create_does_not_double_book_the_same_passenger()
    {
        using var db = TestHelpers.NewDb();
        var driver    = TestHelpers.MakeUser("driver", isDriver: true);
        var passenger = TestHelpers.MakeUser("passenger", isVerified: true);
        db.Users.AddRange(driver, passenger);
        await db.SaveChangesAsync();
        var trip = SeedTrip(db, driver.Id);
        db.Reservations.Add(new Reservation { TripId = trip.Id, UserId = passenger.Id, SeatsCount = 1 });
        await db.SaveChangesAsync();

        var c = new ReservationsController(db, TestHelpers.FakeUserManager(passenger, db));
        c.AttachContext(passenger);

        await c.Create(trip.Id, PaymentMethod.Cash);

        Assert.Single(db.Reservations); // no additional row
        Assert.NotNull(c.TempData["Error"]);
    }

    [Fact]
    public async Task Cancel_removes_only_the_owners_reservation()
    {
        using var db = TestHelpers.NewDb();
        var driver    = TestHelpers.MakeUser("driver", isDriver: true);
        var passenger = TestHelpers.MakeUser("passenger", isVerified: true);
        var other     = TestHelpers.MakeUser("other",     isVerified: true);
        db.Users.AddRange(driver, passenger, other);
        await db.SaveChangesAsync();
        var trip = SeedTrip(db, driver.Id);
        var mine    = new Reservation { TripId = trip.Id, UserId = passenger.Id, SeatsCount = 1 };
        var theirs  = new Reservation { TripId = trip.Id, UserId = other.Id,     SeatsCount = 1 };
        db.Reservations.AddRange(mine, theirs);
        await db.SaveChangesAsync();

        var c = new ReservationsController(db, TestHelpers.FakeUserManager(passenger, db));
        c.AttachContext(passenger);

        // Can cancel my own
        await c.Cancel(mine.Id);
        Assert.Single(db.Reservations);
        Assert.Equal(other.Id, db.Reservations.Single().UserId);

        // Cancelling someone else's reservation should 404 — Reservation must belong to the caller.
        var notFound = await c.Cancel(theirs.Id);
        Assert.IsType<NotFoundResult>(notFound);
        Assert.Single(db.Reservations);
    }
}
