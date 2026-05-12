# UrbanLink

A **web application** for ride-sharing and travel-event coordination in Bulgaria.  
Built with **ASP.NET Core MVC 8.0 · EF Core 8 · ASP.NET Core Identity · SQL Server**.

---

## Features

### Trips & Reservations
- Post trips with **automatic per-seat price calculation** based on car type, fuel type, distance and seat count
- Distances computed via Nominatim (OpenStreetMap) + Haversine formula on the server at creation time
- Reserve seats with payment method selection — Cash / Revolut / Bank Transfer
- **Linked trips ↔ events**: attach a trip to an event so passengers know the occasion
- Navigation launchers (Google Maps / Waze / Apple Maps) on trip detail pages
- Bulgaria-only city/town/village autocomplete on all location fields (no street-level input)
- Scrollable 24 h time picker
- 14-day window shown for upcoming trips and events

### Events & Community
- Create events with geocoded **OpenStreetMap** embed
- Per-event **chatroom** — live threaded conversation for attendees
- Global community chat

### Social — Friends & Direct Messages
- Send, accept and reject friend requests
- Red notification dot on the profile button when unseen requests are waiting
- Accepting a friend request auto-seeds a *"👋 You're now friends — say hi!"* starter message and drops you straight into the chat thread
- Direct message threads between friends

### Live Notification System
- Toast popups appear on **every page** for:
  - New incoming direct messages
  - New reservations on your trips
  - Your age-verification result (approved / rejected)
  - Your driver-verification result (approved / rejected)
  - *(Admin only)* New age-verification submissions
  - *(Admin only)* New driver-verification submissions
- Cookie-based deduplication — each notification shows only once per browser session

### Verification
- **Age / identity verification**: users submit a document photo → admin approves → blue ✓ tick appears next to the user's name everywhere
- **Driver verification**: verified users submit three photos (car exterior, car interior, driving licence) → admin approves → driver status granted
  - Unverified users are redirected to age verification first
- Admin gets a red ✓ tick automatically (no submission needed)

### Profiles
- Upload a profile picture (stored as a base64 data URI)
- Age shown next to every name (18–99 range enforced)
- Public profile pages — view trips posted, mutual friends, verification status

### Admin Dashboard
- Manage users (click any row to open the user's profile)
- Manage trips, events, community chat
- Review and approve/reject both age-verification and driver-verification submissions
- Document images are purged from the database the moment a submission is approved or rejected

### Social Sign-in (scaffolded)
OAuth providers are wired up; add your keys to activate them:

```json
"Authentication": {
  "Google":    { "ClientId": "...", "ClientSecret": "..." },
  "Facebook":  { "AppId": "...",   "AppSecret": "..."   },
  "Twitter":   { "ConsumerKey": "...", "ConsumerSecret": "..." },
  "Apple":     { "ClientId": "...", "TeamId": "...", "KeyId": "...", "PrivateKeyPath": "..." },
  "Instagram": { "ClientId": "...", "ClientSecret": "..." }
}
```

Each provider needs a callback URL registered with that provider:
`https://<your-domain>/signin-{provider}` (e.g. `signin-google`).

---

## Running locally

1. Clone the repo and open the solution in **Visual Studio 2022** or `dotnet` CLI.
2. Restore NuGet packages (`dotnet restore`).
3. Set a connection string — either in `appsettings.Development.json` or via user-secrets:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=UrbanLink;Trusted_Connection=True;"
   }
   ```
4. Apply migrations:
   ```
   dotnet ef database update
   ```
   or from the **Package Manager Console**:
   ```
   Update-Database
   ```
5. Run:
   ```
   dotnet run --project UrbanLinkStarter
   ```
   Then open `https://localhost:5001` (or whatever port is printed).

**Demo admin account** (seeded automatically on first run):
- Email: `admin@urbanlink.local`
- Password: `Admin1234!`

---

## Running the tests

```
dotnet test
```

36 unit tests covering controllers, pricing logic and helper utilities.

---

## Deploying to Fly.io

The project ships with a `Dockerfile` and `fly.toml`.

```bash
# First deploy
fly launch          # follow prompts, skip auto-deploy

# Set required secrets
fly secrets set ConnectionStrings__DefaultConnection="Server=...;..."
fly secrets set ASPNETCORE_ENVIRONMENT="Production"

# Deploy
fly deploy
```

The app listens on `http://0.0.0.0:8080` inside the container (configured in `Program.cs`).  
The `Dockerfile` runs as a non-root user for security.

---

## Notes

- Fuel prices and consumption constants live in `Services/TripPricing.cs` (€ per litre / per kWh).
- Per-seat price formula: `(distance_km × consumption_per_100km / 100) × price_per_unit / seats`.
- All currency is displayed in **€ (euros)**.
- Verification document images are wiped from the DB immediately after an admin decision.
