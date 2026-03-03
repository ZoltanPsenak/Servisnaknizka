using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Servisnaknizka.Models
{
    /// <summary>
    /// Autoservis – údaje o servisnej prevádzke (názov firmy, IČO, adresa, kontakt)
    /// </summary>
    public class Service
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string CompanyName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? ICO { get; set; }

        [MaxLength(200)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? City { get; set; }

        [MaxLength(10)]
        public string? PostalCode { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? ContactEmail { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // Cudzí kľúč na používateľa s rolou Service
        public int UserId { get; set; }

        // Navigačné vlastnosti
        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;

        /// <summary>
        /// Zobrazovaný názov servisu
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(CompanyName) ? CompanyName : User?.FullName ?? "Servis";
    }
}
