using System.Text.Json;

namespace UrbanLinkStarter.Services;

public static class GeocodingHelper
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(6) };

    static GeocodingHelper()
    {
        // Nominatim usage policy requires a User-Agent.
        if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
            _http.DefaultRequestHeaders.Add("User-Agent", "UrbanLinkStarter/1.0 (diploma project)");
    }

    public static async Task<(double lat, double lon)?> GeocodeAsync(string place)
    {
        if (string.IsNullOrWhiteSpace(place)) return null;
        try
        {
            var url = "https://nominatim.openstreetmap.org/search" +
                      "?format=json&limit=1&countrycodes=bg&q=" + Uri.EscapeDataString(place);
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement;
            if (arr.GetArrayLength() == 0) return null;
            var first = arr[0];
            var lat = double.Parse(first.GetProperty("lat").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            var lon = double.Parse(first.GetProperty("lon").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            return (lat, lon);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<double?> GetDistanceKmAsync(string from, string to)
    {
        var a = await GeocodeAsync(from);
        var b = await GeocodeAsync(to);
        if (a == null || b == null) return null;
        return Haversine(a.Value.lat, a.Value.lon, b.Value.lat, b.Value.lon);
    }

    public static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double ToRad(double d) => d * Math.PI / 180.0;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var aa = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                 Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                 Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(aa), Math.Sqrt(1 - aa));
        return Math.Round(R * c, 1);
    }
}
