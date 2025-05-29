// Models/Schedule.cs
using Kutip.Data;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kutip.Models
{
    public class Schedule
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; }

        [Required]
        [StringLength(10)]
        public string Day { get; set; } // e.g., "Monday", "Tuesday", etc.

        [Required]
        [DataType(DataType.Time)]
        public TimeSpan Time { get; set; }

        [Required]
        [StringLength(20)]
        public string PickupStatus { get; set; } // e.g., "Pending", "Completed", "Cancelled"

        // Navigation property
        public virtual ApplicationUser User { get; set; }
    }
}