using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Servisnaknizka.Models
{
    /// <summary>
    /// Servisný záznam - evidencia jednotlivých servisných úkonov
    /// </summary>
    public class ServiceRecord
    {
        public int Id { get; set; }

        public int VehicleId { get; set; }

        [Required]
        public DateTime ServiceDate { get; set; }

        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Range(0, 2000000)]
        public int Mileage { get; set; } // Poèet kilometrov

        [Column(TypeName = "decimal(10,2)")]
        public decimal? Cost { get; set; }

        [MaxLength(100)]
        public string? ServiceType { get; set; } // napr. "Pravidelný servis", "Oprava", "STK"

        [MaxLength(200)]
        public string? PartsUsed { get; set; } // Použité náhradné diely

        [MaxLength(1000)]
        public string? Notes { get; set; } // Poznámky

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int CreatedById { get; set; } // Kto vytvoril záznam (servis)

        // Navigaèné vlastnosti
        [ForeignKey(nameof(VehicleId))]
        public virtual Vehicle Vehicle { get; set; } = null!;

        [ForeignKey(nameof(CreatedById))]
        public virtual User CreatedBy { get; set; } = null!;
    }
}