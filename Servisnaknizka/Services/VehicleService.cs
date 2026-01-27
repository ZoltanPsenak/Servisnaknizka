using Microsoft.EntityFrameworkCore;
using Servisnaknizka.Data;
using Servisnaknizka.Models;

namespace Servisnaknizka.Services
{
    public class VehicleService : IVehicleService
    {
        private readonly ApplicationDbContext _context;

        public VehicleService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<Vehicle>> GetVehiclesByOwnerAsync(int ownerId)
        {
            return await _context.Vehicles
                .Where(v => v.OwnerId == ownerId && v.IsActive)
                .Include(v => v.ServiceRecords)
                .OrderBy(v => v.Brand)
                .ThenBy(v => v.Model)
                .ToListAsync();
        }
        public async Task<IEnumerable<Vehicle>> GetVehiclesByServiceAsync(int serviceId)
        {
            return await _context.Vehicles
                .Where(v => v.IsActive && v.ServicePermissions.Any(p => p.ServiceId == serviceId && p.IsActive))
                .Include(v => v.Owner)
                .Include(v => v.ServiceRecords)
                .OrderBy(v => v.Brand)
                .ThenBy(v => v.Model)
                .ToListAsync();
        }
        public async Task<Vehicle?> GetVehicleByIdAsync(int vehicleId, int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return null;

            var query = _context.Vehicles.AsQueryable();

            // Filtrovanie pod¾a role
            query = user.Role switch
            {
                UserRole.Owner => query.Where(v => v.OwnerId == userId),
                UserRole.Service => query.Where(v => v.ServicePermissions.Any(p => p.ServiceId == userId && p.IsActive)),
                UserRole.Admin => query, 
                _ => query.Where(v => false) 
            };

            return await query
                .Where(v => v.Id == vehicleId && v.IsActive)
                .Include(v => v.Owner)
                .Include(v => v.ServiceRecords)
                .ThenInclude(sr => sr.CreatedBy)
                .FirstOrDefaultAsync();
        }

        public async Task<Vehicle?> GetVehicleByVinAsync(string vin)
        {
            return await _context.Vehicles
                .Where(v => v.VIN == vin && v.IsActive)
                .Include(v => v.Owner)
                .FirstOrDefaultAsync();
        }

        public async Task<Vehicle> CreateVehicleAsync(Vehicle vehicle)
        {
            vehicle.CreatedAt = DateTime.UtcNow;
            vehicle.IsActive = true;

            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();
            return vehicle;
        }
        public async Task<Vehicle> UpdateVehicleAsync(Vehicle vehicle)
        {
            _context.Entry(vehicle).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return vehicle;
        }

        public async Task<bool> DeleteVehicleAsync(int vehicleId, int userId)
        {
            var vehicle = await GetVehicleByIdAsync(vehicleId, userId);
            if (vehicle == null) return false;

            vehicle.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> HasAccessToVehicleAsync(int vehicleId, int userId, UserRole userRole)
        {
            return userRole switch
            {
                UserRole.Admin => true,
                UserRole.Owner => await _context.Vehicles.AnyAsync(v => v.Id == vehicleId && v.OwnerId == userId),
                UserRole.Service => await _context.Permissions.AnyAsync(p => p.VehicleId == vehicleId && p.ServiceId == userId && p.IsActive),
                _ => false
            };
        }
    }
}