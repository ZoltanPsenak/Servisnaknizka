using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Servisnaknizka.Models
{
    /// <summary>
    /// Opr�vnenia servisu k vozidlu - definuje, ktor� servis m��e pristupova� k ak�mu vozidlu
    /// </summary>
    public class Permission
    {
        public int Id { get; set; }

        public int ServiceId { get; set; } // ID servisu (User s rolou Service)

        public int VehicleId { get; set; }

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        public int GrantedById { get; set; } // Kto udelil opr�vnenie (majite� alebo admin)

        public bool IsActive { get; set; } = true;

        [MaxLength(200)]
        public string? Notes { get; set; }

        // Naviga�n� vlastnosti
        [ForeignKey(nameof(ServiceId))]
        public virtual User Service { get; set; } = null!;

        [ForeignKey(nameof(VehicleId))]
        public virtual Vehicle Vehicle { get; set; } = null!;

        [ForeignKey(nameof(GrantedById))]
        public virtual User GrantedBy { get; set; } = null!;
    }
}