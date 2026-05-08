using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UrbanLinkStarter.Models;

namespace UrbanLinkStarter.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();

        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        string[] roles = ["Admin", "User"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var adminEmail = "admin@urbanlink.local";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "UrbanLink Admin",
                Bio = "Administrator account",
                Age = 30,
                IsVerified = true
            };
            await userManager.CreateAsync(admin, "admin123");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        if (!context.Trips.Any())
        {
            context.Trips.AddRange(
                new Trip
                {
                    Title = "Sofia to Plovdiv",
                    FromLocation = "Sofia",
                    ToLocation = "Plovdiv",
                    TravelDate = DateTime.Now.AddDays(2),
                    AvailableSeats = 3,
                    CarBrand = "Volkswagen",
                    CarModel = "Golf 7",
                    CarYear = 2018,
                    FuelConsumption = 6.5,
                    FuelType = FuelType.Diesel,
                    DistanceKm = 145,
                    Price = 8.65m,
                    DriverId = admin.Id
                },
                new Trip
                {
                    Title = "Sofia to Bansko",
                    FromLocation = "Sofia",
                    ToLocation = "Bansko",
                    TravelDate = DateTime.Now.AddDays(4),
                    AvailableSeats = 2,
                    CarBrand = "Toyota",
                    CarModel = "Corolla",
                    CarYear = 2020,
                    FuelConsumption = 5.5,
                    FuelType = FuelType.Gasoline,
                    DistanceKm = 160,
                    Price = 12.55m,
                    DriverId = admin.Id
                }
            );
        }

        if (!context.Events.Any())
        {
            context.Events.AddRange(
                new Event
                {
                    Title = "Music Festival",
                    Description = "Looking for people traveling together to the event.",
                    Location = "Plovdiv",
                    EventDate = DateTime.Now.AddDays(10)
                },
                new Event
                {
                    Title = "Tech Meetup",
                    Description = "Shared travel for a programming meetup.",
                    Location = "Sofia",
                    EventDate = DateTime.Now.AddDays(6)
                }
            );
        }

        await context.SaveChangesAsync();
    }
}
