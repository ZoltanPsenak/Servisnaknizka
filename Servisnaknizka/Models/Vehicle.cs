using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Servisnaknizka.Models
{
    /// <summary>
    /// Vozidlo - obsahuje z�kladn� inform�cie o automobile
    /// </summary>
    public class Vehicle
    {
        public int Id { get; set; }

        [Required]
        [StringLength(17, MinimumLength = 17)]
        public string VIN { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Brand { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Model { get; set; } = string.Empty;

        [Range(1900, 2100)]
        public int Year { get; set; }

        [Required]
        [MaxLength(20)]
        public string LicensePlate { get; set; } = string.Empty;

        [MaxLength(30)]
        public string? Color { get; set; }

        [MaxLength(50)]
        public string? EngineType { get; set; }

        public int? EnginePower { get; set; } // kW

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Prenosový kód na prevod vozidla na nového majiteľa
        [MaxLength(8)]
        public string? TransferCode { get; set; }
        public DateTime? TransferCodeExpiry { get; set; }

        // Cudz� k��� na majite�a
        public int OwnerId { get; set; }

        // Naviga�n� vlastnosti
        [ForeignKey(nameof(OwnerId))]
        public virtual User Owner { get; set; } = null!;

        public virtual ICollection<ServiceRecord> ServiceRecords { get; set; } = new List<ServiceRecord>();
        public virtual ICollection<Permission> ServicePermissions { get; set; } = new List<Permission>();

        public string DisplayName => $"{Brand} {Model} ({Year}) - {LicensePlate}";
    }
}