using Servisnaknizka.Models;

namespace Servisnaknizka.Services
{
    public interface IVehicleService
    {
        Task<IEnumerable<Vehicle>> GetVehiclesByOwnerAsync(int ownerId);
        Task<IEnumerable<Vehicle>> GetVehiclesByServiceAsync(int serviceId);
        Task<Vehicle?> GetVehicleByIdAsync(int vehicleId, int userId);
        Task<Vehicle?> GetVehicleByVinAsync(string vin);
        Task<Vehicle> CreateVehicleAsync(Vehicle vehicle);
        Task<Vehicle> UpdateVehicleAsync(Vehicle vehicle);
        Task<bool> DeleteVehicleAsync(int vehicleId, int userId);
        Task<bool> HasAccessToVehicleAsync(int vehicleId, int userId, UserRole userRole);
    }
}