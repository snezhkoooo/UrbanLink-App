using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using UrbanLinkStarter.Data;
using UrbanLinkStarter.Models;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:8080");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// ----- External auth providers (only added if config present) -----
var auth = builder.Services.AddAuthentication();

var google = builder.Configuration.GetSection("Authentication:Google");
if (!string.IsNullOrEmpty(google["ClientId"]))
{
    auth.AddGoogle(opts =>
    {
        opts.ClientId     = google["ClientId"]!;
        opts.ClientSecret = google["ClientSecret"]!;
    });
}

var fb = builder.Configuration.GetSection("Authentication:Facebook");
if (!string.IsNullOrEmpty(fb["AppId"]))
{
    auth.AddFacebook(opts =>
    {
        opts.AppId     = fb["AppId"]!;
        opts.AppSecret = fb["AppSecret"]!;
    });
}

var tw = builder.Configuration.GetSection("Authentication:Twitter");
if (!string.IsNullOrEmpty(tw["ConsumerKey"]))
{
    auth.AddTwitter(opts =>
    {
        opts.ConsumerKey    = tw["ConsumerKey"]!;
        opts.ConsumerSecret = tw["ConsumerSecret"]!;
        opts.RetrieveUserDetails = true;
    });
}

var apple = builder.Configuration.GetSection("Authentication:Apple");
if (!string.IsNullOrEmpty(apple["ClientId"]))
{
    auth.AddApple(opts =>
    {
        opts.ClientId  = apple["ClientId"]!;
        opts.KeyId     = apple["KeyId"]!;
        opts.TeamId    = apple["TeamId"]!;
        // Apple expects a private key file path or generated secret — see csproj package docs
        // opts.UsePrivateKey(_ => new PhysicalFileInfo(new FileInfo(apple["PrivateKeyPath"]!)));
    });
}

var ig = builder.Configuration.GetSection("Authentication:Instagram");
if (!string.IsNullOrEmpty(ig["ClientId"]))
{
    auth.AddInstagram(opts =>
    {
        opts.ClientId     = ig["ClientId"]!;
        opts.ClientSecret = ig["ClientSecret"]!;
    });
}

// Anonymous users are only allowed to access: Home/Index, Privacy, Terms, Contacts,
// the Identity Login/Register pages, and Events listings. Everything else requires sign-in.
builder.Services.AddControllersWithViews(options =>
{
    var policy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter(policy));
});

builder.Services.AddRazorPages();

builder.Services.AddRateLimiter(opts =>
{
    opts.AddFixedWindowLimiter("fixed", o =>
    {
        o.PermitLimit         = 100;
        o.Window              = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit          = 0;
    });
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await SeedData.InitializeAsync(services);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHealthChecks("/health").RequireRateLimiting("fixed");

app.Run();
