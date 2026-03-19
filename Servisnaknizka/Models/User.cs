using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Servisnaknizka.Models
{
    /// <summary>
    /// Pou��vate� syst�mu - roz�iruje IdentityUser o rolu
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

        // Naviga�n� vlastnosti
        public virtual ICollection<Vehicle> OwnedVehicles { get; set; } = new List<Vehicle>();
        public virtual ICollection<Permission> ServicePermissions { get; set; } = new List<Permission>();
        public virtual ICollection<ServiceRecord> CreatedServiceRecords { get; set; } = new List<ServiceRecord>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        /// <summary>
        /// Servisná prevádzka prislúchajúca tomuto používateľovi (ak má rolu Service)
        /// </summary>
        public virtual Service? ServiceProfile { get; set; }
        public string FullName => $"{FirstName} {LastName}";
    }

    /// <summary>
    /// Roly pou��vate�ov v syst�me
    /// </summary>
    public enum UserRole
    {
        Owner = 1,      // Majite� vozidla
        Service = 2,    // Autoservis
        Admin = 3       // Administr�tor
    }
}