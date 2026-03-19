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
    options.Cookie.Name = ".ServisnaKnizka.Auth";
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
        ? CookieSecurePolicy.SameAsRequest 
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Registrácia služieb
builder.Services.AddScoped<IVehicleService, VehicleService>();

// Pozadie: automatické pripomienky servisov
builder.Services.AddHostedService<ReminderService>();

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
else
{
    app.UseDeveloperExceptionPage();
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

    // ✅ KROK 4: Vytvorenie servisov
    // Servis 1: AutoServis Bratislava
    var serviceEmail1 = "servis@autoservis.sk";
    var serviceUser1 = await userManager.FindByEmailAsync(serviceEmail1);
    
    if (serviceUser1 == null)
    {
        var sUser = new User
        {
            FirstName = "AutoServis",
            LastName = "Bratislava",
            UserName = serviceEmail1,
            Email = serviceEmail1,
            Role = UserRole.Service,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(sUser, "Service123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(sUser, "Service");
            await userManager.AddClaimAsync(sUser, new Claim(ClaimTypes.Role, "Service"));

            // Vytvorenie servisného profilu
            var serviceProfile = new Service
            {
                CompanyName = "AutoServis Bratislava s.r.o.",
                ICO = "12345678",
                Address = "Hlavná 15",
                City = "Bratislava",
                PostalCode = "81101",
                Phone = "+421 2 1234 5678",
                ContactEmail = serviceEmail1,
                Description = "Autorizovaný servis pre všetky značky osobných vozidiel. Diagnostika, opravy, údržba.",
                UserId = sUser.Id,
                IsActive = true
            };
            context.Services.Add(serviceProfile);
            await context.SaveChangesAsync();
        }
    }
    else
    {
        var isInRole = await userManager.IsInRoleAsync(serviceUser1, "Service");
        if (!isInRole)
        {
            await userManager.AddToRoleAsync(serviceUser1, "Service");
            await userManager.AddClaimAsync(serviceUser1, new Claim(ClaimTypes.Role, "Service"));
        }
        // Ak nemá servisný profil, vytvoriť
        if (!await context.Services.AnyAsync(s => s.UserId == serviceUser1.Id))
        {
            context.Services.Add(new Service
            {
                CompanyName = "AutoServis Bratislava s.r.o.",
                ICO = "12345678",
                Address = "Hlavná 15",
                City = "Bratislava",
                PostalCode = "81101",
                Phone = "+421 2 1234 5678",
                ContactEmail = serviceEmail1,
                Description = "Autorizovaný servis pre všetky značky osobných vozidiel. Diagnostika, opravy, údržba.",
                UserId = serviceUser1.Id,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }
    }

    // Servis 2: Pneuservis Košice
    var serviceEmail2 = "pneuservis@kosice.sk";
    var serviceUser2 = await userManager.FindByEmailAsync(serviceEmail2);

    if (serviceUser2 == null)
    {
        var sUser2 = new User
        {
            FirstName = "Pneuservis",
            LastName = "Košice",
            UserName = serviceEmail2,
            Email = serviceEmail2,
            Role = UserRole.Service,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(sUser2, "Service123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(sUser2, "Service");
            await userManager.AddClaimAsync(sUser2, new Claim(ClaimTypes.Role, "Service"));

            context.Services.Add(new Service
            {
                CompanyName = "Pneuservis Košice s.r.o.",
                ICO = "87654321",
                Address = "Štúrova 42",
                City = "Košice",
                PostalCode = "04001",
                Phone = "+421 55 622 3344",
                ContactEmail = serviceEmail2,
                Description = "Prezúvanie pneumatík, vyvažovanie, geometria kolies, predaj pneumatík.",
                UserId = sUser2.Id,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }
    }

    // Servis 3: AutoElektrika Žilina
    var serviceEmail3 = "elektrika@zilina.sk";
    var serviceUser3 = await userManager.FindByEmailAsync(serviceEmail3);

    if (serviceUser3 == null)
    {
        var sUser3 = new User
        {
            FirstName = "AutoElektrika",
            LastName = "Žilina",
            UserName = serviceEmail3,
            Email = serviceEmail3,
            Role = UserRole.Service,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(sUser3, "Service123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(sUser3, "Service");
            await userManager.AddClaimAsync(sUser3, new Claim(ClaimTypes.Role, "Service"));

            context.Services.Add(new Service
            {
                CompanyName = "AutoElektrika Žilina",
                ICO = "55667788",
                Address = "Predmestská 8",
                City = "Žilina",
                PostalCode = "01001",
                Phone = "+421 41 555 6677",
                ContactEmail = serviceEmail3,
                Description = "Autoelektrikárske práce, diagnostika, oprava elektroniky vozidiel.",
                UserId = sUser3.Id,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }
    }
}
