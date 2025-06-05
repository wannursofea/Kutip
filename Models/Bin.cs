using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Kutip.Models
{
    public class Bin
    {
        [Key]
        public int b_ID { get; set; }

        [Required(ErrorMessage = "Plate number is required.")]
        [Display(Name = "Bin Plate No.")]
        [MaxLength(20)]
        public string b_PlateNo { get; set; } = "";

        [Required]
        public int c_ID { get; set; }
        [ForeignKey("c_ID")]
        public virtual Customer Customer { get; set; }

        // Foreign Key to Location (example table)
        public int l_ID { get; set; }
        [ForeignKey("l_ID")]
        public virtual Location Location { get; set; }

        public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();
    }
}
