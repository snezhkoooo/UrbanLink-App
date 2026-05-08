# UrbanLink

Ride-sharing + events platform for Bulgaria. ASP.NET Core MVC + Identity + EF Core.

## Features
- Trip listings with car/fuel-based **automatic price calculation** (split among passengers)
- Reservations with payment method selection (cash / Revolut / bank transfer)
- Events with built-in chatroom per event
- Linked trips ↔ events
- Community chat (renamed from Messages)
- Profile pictures (uploaded as base64)
- Age requirement (18-99) shown next to every name
- Verification system: users submit ID/license/passport/Revolut → admin approves → blue tick
- Admin role gets a red tick automatically
- Bulgaria-only city/town/village autocomplete (no street names)
- Apple-style scrollable 24h time picker
- 14-day window for upcoming trips/events
- Navigation launchers (Google Maps / Waze / Apple Maps) on trip details
- Geocoded OpenStreetMap on event details
- Admin dashboard with manage screens for users / trips / events / chat / verifications
- Social sign-in scaffolded for Google, Apple, Facebook, Instagram, X
  (provider keys go in `appsettings.json` under `Authentication:*`)

## Run steps
1. Open the folder in Visual Studio 2022.
2. Restore NuGet packages.
3. Open the **Package Manager Console** and run:
   - `Drop-Database` (if rebuilding)
   - `Add-Migration InitialCreate`
   - `Update-Database`
4. Start the project (F5).

## Demo admin
- Email: `admin@urbanlink.local`
- Password: `admin123`

## Adding social sign-in
Open `appsettings.json` and fill in the keys for any provider you want enabled:

```json
"Authentication": {
  "Google":    { "ClientId": "...", "ClientSecret": "..." },
  "Facebook":  { "AppId": "...", "AppSecret": "..." },
  "Twitter":   { "ConsumerKey": "...", "ConsumerSecret": "..." },
  "Apple":     { "ClientId": "...", "TeamId": "...", "KeyId": "...", "PrivateKeyPath": "..." },
  "Instagram": { "ClientId": "...", "ClientSecret": "..." }
}
```

Restart the app — providers with keys will become live; the others show a friendly "not configured" prompt.

Each provider needs you to register a callback URL with that provider, typically:
`https://localhost:PORT/signin-{provider}`  (e.g. `signin-google`).

## Notes
- Fuel prices are constants in `Services/TripPricing.cs` (BGN per L / per kWh).
- Distances are computed via Nominatim (OSM) + Haversine on the server when a trip is created.
- Per-seat price is recomputed on save: `(distance × consumption / 100) × price_per_unit / seats`.
- Verification document images are wiped from the DB the moment an admin approves or rejects.
