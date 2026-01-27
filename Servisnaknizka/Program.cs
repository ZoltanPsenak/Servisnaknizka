using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Servisnaknizka.Components;
using Servisnaknizka.Data;
using Servisnaknizka.Models;
using Servisnaknizka.Services;

var builder = WebApplication.CreateBuilder(args);

// Pridanie DbContext s SQL Server - pripojenie z konfigurácie
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });
    
    // Logging pre development
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Konfigurácia ASP.NET Identity
builder.Services.AddIdentity<User, IdentityRole<int>>(options =>
{
    // Nastavenia hesla
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;

    // Nastavenia používate¾a
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;

    // Nastavenia uzamknutia úètu
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Konfigurácia cookies pre autentifikáciu
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Registrácia služieb
builder.Services.AddScoped<IVehicleService, VehicleService>();

// Pridanie Razor Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Pridanie autorizácie
builder.Services.AddAuthorization(options =>
{
    // Politiky pre roly
    options.AddPolicy("AdminOnly", policy => 
        policy.RequireAuthenticatedUser()
              .RequireClaim(ClaimTypes.Role, "Admin"));
    
    options.AddPolicy("ServiceOrAdmin", policy => 
        policy.RequireAuthenticatedUser()
              .RequireAssertion(context =>
                  context.User.HasClaim(ClaimTypes.Role, "Service") ||
                  context.User.HasClaim(ClaimTypes.Role, "Admin")));
    
    options.AddPolicy("OwnerOrAdmin", policy => 
        policy.RequireAuthenticatedUser()
              .RequireAssertion(context =>
                  context.User.HasClaim(ClaimTypes.Role, "Owner") ||
                  context.User.HasClaim(ClaimTypes.Role, "Admin")));
});

var app = builder.Build();

// Aplikovanie migrácií a seed dát pri spustení
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        
        // Aplikovanie migrácií
        await context.Database.MigrateAsync();
        
        // Seed dát
        await SeedDataAsync(userManager, context);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Chyba pri inicializácii databázy");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Autentifikácia a autorizácia
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

/// <summary>
/// Inicializuje testové dáta do SQL Server databázy
/// </summary>
async Task SeedDataAsync(UserManager<User> userManager, ApplicationDbContext context)
{
    // Vytvorenie admin úètu ak neexistuje
    if (await userManager.FindByEmailAsync("admin@servis.sk") == null)
    {
        var admin = new User
        {
            FirstName = "Admin",
            LastName = "Systému",
            UserName = "admin@servis.sk",
            Email = "admin@servis.sk",
            Role = UserRole.Admin,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, "Admin123!");
        if (result.Succeeded)
        {
            await userManager.AddClaimAsync(admin, new Claim(ClaimTypes.Role, "Admin"));
        }
    }

    // Vytvorenie majite¾a ak neexistuje
    if (await userManager.FindByEmailAsync("majitel@vozidlo.sk") == null)
    {
        var owner = new User
        {
            FirstName = "Ján",
            LastName = "Novák",
            UserName = "majitel@vozidlo.sk",
            Email = "majitel@vozidlo.sk",
            Role = UserRole.Owner,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(owner, "Owner123!");
        if (result.Succeeded)
        {
            await userManager.AddClaimAsync(owner, new Claim(ClaimTypes.Role, "Owner"));
            
            // Vytvorenie vzorového vozidla
            var vehicle = new Vehicle
            {
                VIN = "WVWZZZ1JZ3W386752",
                Brand = "Volkswagen",
                Model = "Golf",
                Year = 2019,
                LicensePlate = "BA123AB",
                Color = "Modrá",
                EngineType = "1.5 TSI",
                EnginePower = 110,
                OwnerId = owner.Id
            };
            
            context.Vehicles.Add(vehicle);
            await context.SaveChangesAsync();
        }
    }

    // Vytvorenie servisu ak neexistuje
    if (await userManager.FindByEmailAsync("servis@autoservis.sk") == null)
    {
        var service = new User
        {
            FirstName = "AutoServis",
            LastName = "Bratislava",
            UserName = "servis@autoservis.sk",
            Email = "servis@autoservis.sk",
            Role = UserRole.Service,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(service, "Service123!");
        if (result.Succeeded)
        {
            await userManager.AddClaimAsync(service, new Claim(ClaimTypes.Role, "Service"));
        }
    }
}
