using UrbanLinkStarter.Models;
using UrbanLinkStarter.Services;
using Xunit;

namespace UrbanLinkStarter.Tests;

public class TripPricingTests
{
    [Fact]
    public void TotalCost_returns_zero_when_distance_is_zero()
    {
        Assert.Equal(0m, TripPricing.TotalCost(0, 7.0, FuelType.Gasoline));
    }

    [Fact]
    public void TotalCost_returns_zero_when_consumption_is_zero()
    {
        Assert.Equal(0m, TripPricing.TotalCost(100, 0, FuelType.Gasoline));
    }

    [Theory]
    [InlineData(FuelType.Gasoline, 2.85)]
    [InlineData(FuelType.Diesel,   2.75)]
    [InlineData(FuelType.LPG,      1.45)]
    public void TotalCost_uses_correct_rate_per_fuel(FuelType fuel, double expectedRate)
    {
        // 100 km × 10 L/100km = 10 L  ->  10 × rate
        var total = TripPricing.TotalCost(distanceKm: 100, consumptionPer100: 10, fuel: fuel);
        Assert.Equal(Math.Round(10m * (decimal)expectedRate, 2), total);
    }

    [Fact]
    public void TotalCost_electric_uses_kwh_rate()
    {
        // 100 km × 20 kWh/100km = 20 kWh × 0.45 BGN/kWh = 9.00
        var total = TripPricing.TotalCost(100, 20, FuelType.Electric);
        Assert.Equal(9.00m, total);
    }

    [Fact]
    public void PricePerSeat_splits_total_evenly()
    {
        // 100 km × 7 L/100km × 2.85 = 19.95 BGN ; / 3 seats = 6.65
        var perSeat = TripPricing.PricePerSeat(100, 7, FuelType.Gasoline, seats: 3);
        Assert.Equal(6.65m, perSeat);
    }

    [Fact]
    public void PricePerSeat_returns_zero_for_zero_or_negative_seats()
    {
        Assert.Equal(0m, TripPricing.PricePerSeat(100, 7, FuelType.Gasoline, 0));
        Assert.Equal(0m, TripPricing.PricePerSeat(100, 7, FuelType.Gasoline, -1));
    }

    [Fact]
    public void FuelLabel_uses_kwh_for_electric()
    {
        Assert.Equal("Electric (kWh/100km)", TripPricing.FuelLabel(FuelType.Electric));
        Assert.Equal("Gasoline (L/100km)", TripPricing.FuelLabel(FuelType.Gasoline));
    }

    [Fact]
    public void FuelUnit_differentiates_electric_from_combustion()
    {
        Assert.Equal("kWh", TripPricing.FuelUnit(FuelType.Electric));
        Assert.Equal("L",   TripPricing.FuelUnit(FuelType.Gasoline));
        Assert.Equal("L",   TripPricing.FuelUnit(FuelType.Diesel));
        Assert.Equal("L",   TripPricing.FuelUnit(FuelType.LPG));
    }
}
