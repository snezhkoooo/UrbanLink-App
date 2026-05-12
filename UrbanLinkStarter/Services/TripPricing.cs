using UrbanLinkStarter.Models;

namespace UrbanLinkStarter.Services;

/// <summary>
/// Splits estimated fuel cost equally among passengers (driver excluded by default).
/// Algorithm:
///   total liters = distance_km × consumption / 100
///   total cost   = total liters × price_per_liter
///   per seat     = total cost / passengers (or all in car if no separate driver)
/// </summary>
public static class TripPricing
{
    // Approximate Bulgarian fuel prices (BGN per liter / per kWh) — placeholders.
    // Tweak in a real config; here for the MVP they are constants.
    private static readonly Dictionary<FuelType, decimal> PricePerUnit = new()
    {
        { FuelType.Gasoline, 2.85m },  // BGN / L
        { FuelType.Diesel,   2.75m },  // BGN / L
        { FuelType.LPG,      1.45m },  // BGN / L
        { FuelType.Electric, 0.45m }   // BGN / kWh
    };

    /// <summary>Returns total fuel cost for the whole trip, in BGN.</summary>
    public static decimal TotalCost(double distanceKm, double consumptionPer100, FuelType fuel)
    {
        if (distanceKm <= 0 || consumptionPer100 <= 0) return 0m;
        var unitsNeeded = (decimal)((distanceKm * consumptionPer100) / 100.0);
        var rate = PricePerUnit.TryGetValue(fuel, out var p) ? p : 2.85m;
        return Math.Round(unitsNeeded * rate, 2);
    }

    /// <summary>Returns per-seat price, splitting the total fuel cost across passengers.</summary>
    public static decimal PricePerSeat(double distanceKm, double consumptionPer100, FuelType fuel, int seats)
    {
        if (seats <= 0) return 0m;
        var total = TotalCost(distanceKm, consumptionPer100, fuel);
        return Math.Round(total / seats, 2);
    }

    public static string FuelLabel(FuelType type) => type switch
    {
        FuelType.Gasoline => "Gasoline (L/100km)",
        FuelType.Diesel   => "Diesel (L/100km)",
        FuelType.LPG      => "LPG (L/100km)",
        FuelType.Electric => "Electric (kWh/100km)",
        _ => "L/100km"
    };

    public static string FuelUnit(FuelType type) => type == FuelType.Electric ? "kWh" : "L";
}
