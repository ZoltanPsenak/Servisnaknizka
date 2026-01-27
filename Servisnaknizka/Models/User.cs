using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Servisnaknizka.Models
{
    /// <summary>
    /// Používate¾ systému - rozširuje IdentityUser o rolu
    /// </summary>
    public class User : IdentityUser<int>
    {
        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; } = UserRole.Owner;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Navigaèné vlastnosti
        public virtual ICollection<Vehicle> OwnedVehicles { get; set; } = new List<Vehicle>();
        public virtual ICollection<Permission> ServicePermissions { get; set; } = new List<Permission>();
        public virtual ICollection<ServiceRecord> CreatedServiceRecords { get; set; } = new List<ServiceRecord>();

        public string FullName => $"{FirstName} {LastName}";
    }

    /// <summary>
    /// Roly používate¾ov v systéme
    /// </summary>
    public enum UserRole
    {
        Owner = 1,      // Majite¾ vozidla
        Service = 2,    // Autoservis
        Admin = 3       // Administrátor
    }
}