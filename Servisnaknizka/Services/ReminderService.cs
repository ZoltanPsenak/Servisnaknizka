using Microsoft.EntityFrameworkCore;
using Servisnaknizka.Data;
using Servisnaknizka.Models;

namespace Servisnaknizka.Services;

public class ReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(IServiceScopeFactory scopeFactory, ILogger<ReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Počkať 30 sekúnd po štarte, aby sa dokončila inicializácia
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateReminders(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chyba pri generovaní pripomienok");
            }

            // Kontrola každých 6 hodín
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task GenerateReminders(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var today = now.Date;

        var vehicles = await db.Vehicles
            .Where(v => v.IsActive)
            .Include(v => v.Owner)
            .Include(v => v.ServiceRecords)
            .ToListAsync(ct);

        foreach (var vehicle in vehicles)
        {
            var ownerId = vehicle.OwnerId;
            var label = $"{vehicle.Brand} {vehicle.Model} ({vehicle.LicensePlate})";

            // ===== 1. PRAVIDELNÝ SERVIS =====
            if (vehicle.ServiceIntervalMonths > 0)
            {
                var lastService = vehicle.ServiceRecords
                    .OrderByDescending(sr => sr.ServiceDate)
                    .FirstOrDefault();

                DateTime nextServiceDate;
                if (vehicle.NextServiceDate.HasValue)
                    nextServiceDate = vehicle.NextServiceDate.Value;
                else if (lastService != null)
                    nextServiceDate = lastService.ServiceDate.AddMonths(vehicle.ServiceIntervalMonths);
                else
                    nextServiceDate = vehicle.CreatedAt.AddMonths(vehicle.ServiceIntervalMonths);

                if (vehicle.NextServiceDate != nextServiceDate)
                    vehicle.NextServiceDate = nextServiceDate;

                await CheckAndCreateNotification(db, now, ownerId, vehicle.Id, nextServiceDate, today,
                    "service", label, "servis", ct);
            }

            // ===== 2. STK =====
            if (vehicle.StkExpiry.HasValue)
            {
                await CheckAndCreateNotification(db, now, ownerId, vehicle.Id, vehicle.StkExpiry.Value, today,
                    "stk", label, "STK", ct);
            }

            // ===== 3. EMISNÁ KONTROLA =====
            if (vehicle.EmissionExpiry.HasValue)
            {
                await CheckAndCreateNotification(db, now, ownerId, vehicle.Id, vehicle.EmissionExpiry.Value, today,
                    "emission", label, "emisná kontrola", ct);
            }

            // ===== 4. POISTENIE =====
            if (vehicle.InsuranceExpiry.HasValue)
            {
                await CheckAndCreateNotification(db, now, ownerId, vehicle.Id, vehicle.InsuranceExpiry.Value, today,
                    "insurance", label, "poistenie", ct);
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Pripomienky skontrolované pre {Count} vozidiel", vehicles.Count);
    }

    private static async Task CheckAndCreateNotification(
        ApplicationDbContext db, DateTime now, int ownerId, int vehicleId,
        DateTime expiryDate, DateTime today, string category, string vehicleLabel, string typeLabel,
        CancellationToken ct)
    {
        var days = (expiryDate.Date - today).Days;

        string? title = null;
        string? message = null;
        string type = "info";

        if (days < 0)
        {
            title = $"{char.ToUpper(typeLabel[0])}{typeLabel[1..]} po termíne!";
            message = $"Vozidlo {vehicleLabel} — {typeLabel} mal(a) byť vykonaný/á {expiryDate:dd.MM.yyyy}. Prosím, obstarajte čo najskôr.";
            type = "danger";
        }
        else if (days <= 7)
        {
            title = $"{char.ToUpper(typeLabel[0])}{typeLabel[1..]} o pár dní";
            message = $"Vozidlo {vehicleLabel} — {typeLabel} vyprší {expiryDate:dd.MM.yyyy}. Zostáva {days} dní.";
            type = "warning";
        }
        else if (days <= 30)
        {
            title = $"Blíži sa {typeLabel}";
            message = $"Vozidlo {vehicleLabel} — {typeLabel} vyprší {expiryDate:dd.MM.yyyy}. Zostáva {days} dní.";
            type = "info";
        }
        else
        {
            return;
        }

        var exists = await db.Notifications.AnyAsync(n =>
            n.UserId == ownerId &&
            n.VehicleId == vehicleId &&
            n.Category == category &&
            n.Title == title &&
            !n.IsRead &&
            n.CreatedAt > now.AddHours(-24), ct);

        if (!exists)
        {
            db.Notifications.Add(new Notification
            {
                UserId = ownerId,
                VehicleId = vehicleId,
                Title = title,
                Message = message,
                Type = type,
                Category = category
            });
        }
    }
}
