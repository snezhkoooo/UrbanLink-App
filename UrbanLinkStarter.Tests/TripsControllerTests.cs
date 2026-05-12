using Microsoft.AspNetCore.Mvc;
using UrbanLinkStarter.Controllers;
using UrbanLinkStarter.Models;
using Xunit;

namespace UrbanLinkStarter.Tests;

public class TripsControllerTests
{
    [Fact]
    public async Task Create_GET_redirects_non_driver_to_driver_verification()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice", isDriver: false);
        db.Users.Add(alice);
        await db.SaveChangesAsync();

        var c = new TripsController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        var result = await c.Create();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("DriverVerification", redirect.ControllerName);
        Assert.NotNull(c.TempData["Error"]);
    }

    [Fact]
    public async Task Create_POST_blocks_non_driver_and_does_not_insert()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice", isDriver: false);
        db.Users.Add(alice);
        await db.SaveChangesAsync();

        var c = new TripsController(db, TestHelpers.FakeUserManager(alice, db));
        c.AttachContext(alice);

        var trip = new Trip
        {
            Title = "x", FromLocation = "Sofia", ToLocation = "Plovdiv",
            TravelDate = DateTime.UtcNow.AddDays(1),
            AvailableSeats = 3, CarBrand = "VW", CarModel = "Golf",
            CarYear = 2018, FuelConsumption = 6.5, FuelType = FuelType.Diesel
        };
        var result = await c.Create(trip);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("DriverVerification", redirect.ControllerName);
        Assert.Empty(db.Trips);
    }

    [Fact]
    public async Task Index_search_filters_by_route_or_driver_name()
    {
        using var db = TestHelpers.NewDb();
        var alice = TestHelpers.MakeUser("alice", fullName: "Alice Driver", isDriver: true);
        var bob   = TestHelpers.MakeUser("bob",   fullName: "Bob Wheels",   isDriver: true);
        db.Users.AddRange(alice, bob);
        db.Trips.AddRange(
            new Trip
            {
                Title = "Mountains run", FromLocation = "Sofia", ToLocation = "Bansko",
                TravelDate = DateTime.UtcNow.AddDays(1), AvailableSeats = 3,
                CarBrand = "VW", CarModel = "Golf", CarYear = 2018,
                FuelConsumption = 6.5, FuelType = FuelType.Diesel,
                DriverId = alice.Id
            },
            new Trip
            {
                Title = "Sea trip", FromLocation = "Sofia", ToLocation = "Burgas",
                TravelDate = DateTime.UtcNow.AddDays(2), AvailableSeats = 4,
                CarBrand = "Toyota", CarModel = "Corolla", CarYear = 2020,
                FuelConsumption = 5.5, FuelType = FuelType.Gasoline,
                DriverId = bob.Id
            }
        );
        await db.SaveChangesAsync();

        var c = new TripsController(db, TestHelpers.FakeUserManager(null, db));
        c.AttachContext();

        // Search by destination
        var byDestination = (ViewResult)await c.Index("burgas");
        var burgasTrips   = (List<Trip>)byDestination.Model!;
        Assert.Single(burgasTrips);
        Assert.Equal("Sea trip", burgasTrips[0].Title);

        // Search by driver name
        var byDriver = (ViewResult)await c.Index("alice");
        var aliceTrips = (List<Trip>)byDriver.Model!;
        Assert.Single(aliceTrips);
        Assert.Equal("Mountains run", aliceTrips[0].Title);

        // No filter → both trips
        var all = (ViewResult)await c.Index(null);
        var allTrips = (List<Trip>)all.Model!;
        Assert.Equal(2, allTrips.Count);
    }
}
