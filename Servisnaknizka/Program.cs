using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.RateLimiting;
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

    // Nastavenia používateľa
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;

    // Nastavenia uzamknutia účtu
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
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
        ? CookieSecurePolicy.SameAsRequest 
        : CookieSecurePolicy.Always;
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

// ✅ Rate Limiting - Opravená syntax pre .NET 8
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Request.Path.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// ✅ User Secrets pre Development
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// ✅ Pridajte Controllers
builder.Services.AddControllers();

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

// ✅ Rate Limiter middleware
app.UseRateLimiter();

// Autentifikácia a autorizácia
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

/// <summary>
/// Inicializuje testovacie dáta do SQL Server databázy
/// </summary>
async Task SeedDataAsync(UserManager<User> userManager, ApplicationDbContext context)
{
    // ✅ KROK 1: Vytvorenie Identity Roles ak neexistujú
    string[] roleNames = { "Admin", "Owner", "Service" };
    
    foreach (var roleName in roleNames)
    {
        var roleExists = await context.Roles.AnyAsync(r => r.Name == roleName);
        if (!roleExists)
        {
            var role = new IdentityRole<int>
            {
                Name = roleName,
                NormalizedName = roleName.ToUpper()
            };
            context.Roles.Add(role);
        }
    }
    await context.SaveChangesAsync();

    // ✅ KROK 2: Vytvorenie admin účtu
    var adminEmail = "admin@servis.sk";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    
    if (adminUser == null)
    {
        var admin = new User
        {
            FirstName = "Admin",
            LastName = "Systému",
            UserName = adminEmail,
            Email = adminEmail,
            Role = UserRole.Admin,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, "Admin123!");
        if (result.Succeeded)
        {
            // Pridáme do Identity Role
            await userManager.AddToRoleAsync(admin, "Admin");
            // A aj do Claims
            await userManager.AddClaimAsync(admin, new Claim(ClaimTypes.Role, "Admin"));
        }
    }
    else
    {
        // Ak už existuje, uistíme sa že má rolu
        var isInRole = await userManager.IsInRoleAsync(adminUser, "Admin");
        if (!isInRole)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            await userManager.AddClaimAsync(adminUser, new Claim(ClaimTypes.Role, "Admin"));
        }
    }

    // ✅ KROK 3: Vytvorenie majiteľa
    var ownerEmail = "majitel@vozidlo.sk";
    var ownerUser = await userManager.FindByEmailAsync(ownerEmail);
    
    if (ownerUser == null)
    {
        var owner = new User
        {
            FirstName = "Ján",
            LastName = "Novák",
            UserName = ownerEmail,
            Email = ownerEmail,
            Role = UserRole.Owner,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(owner, "Owner123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(owner, "Owner");
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
    else
    {
        var isInRole = await userManager.IsInRoleAsync(ownerUser, "Owner");
        if (!isInRole)
        {
            await userManager.AddToRoleAsync(ownerUser, "Owner");
            await userManager.AddClaimAsync(ownerUser, new Claim(ClaimTypes.Role, "Owner"));
        }
    }

    // ✅ KROK 4: Vytvorenie servisu
    var serviceEmail = "servis@autoservis.sk";
    var serviceUser = await userManager.FindByEmailAsync(serviceEmail);
    
    if (serviceUser == null)
    {
        var service = new User
        {
            FirstName = "AutoServis",
            LastName = "Bratislava",
            UserName = serviceEmail,
            Email = serviceEmail,
            Role = UserRole.Service,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(service, "Service123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(service, "Service");
            await userManager.AddClaimAsync(service, new Claim(ClaimTypes.Role, "Service"));
        }
    }
    else
    {
        var isInRole = await userManager.IsInRoleAsync(serviceUser, "Service");
        if (!isInRole)
        {
            await userManager.AddToRoleAsync(serviceUser, "Service");
            await userManager.AddClaimAsync(serviceUser, new Claim(ClaimTypes.Role, "Service"));
        }
    }
}
