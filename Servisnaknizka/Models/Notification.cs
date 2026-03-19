using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Servisnaknizka.Models
{
    public class Notification
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public int? VehicleId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string Type { get; set; } = "info"; // info, warning, danger

        [MaxLength(30)]
        public string Category { get; set; } = "service"; // service, stk, emission, insurance, custom

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigačné vlastnosti
        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;

        [ForeignKey(nameof(VehicleId))]
        public virtual Vehicle? Vehicle { get; set; }
    }
}
