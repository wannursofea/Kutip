using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq; 

namespace Kutip.Models
{
    public class Truck
    {
        [Key]
        public int t_ID { get; set; }

        [Required]
        [StringLength(10, ErrorMessage = "Plate Number cannot exceed 10 characters.")]
        [Display(Name = "Plate Number")]
        public string t_PlateNo { get; set; }

        [StringLength(20, ErrorMessage = "Status cannot exceed 20 characters.")]
        [Display(Name = "Status")]
        public string? t_Status { get; set; } 

        public virtual ICollection<Schedule>? Schedules { get; set; }

        [NotMapped]
        [Display(Name = "Current Status")]
        public string DisplayStatus
        {
            get
            {
                if (t_Status == "Maintenance")
                {
                    return "Maintenance";
                }

                bool isInUse = Schedules != null && Schedules.Any(s =>
                    s.s_Date.Date >= DateTime.Today.Date && 
                    !(s.PickedUpBins >= s.TotalBins && s.TotalBins > 0) 
                );

                return isInUse ? "In Use" : "Available";
            }
        }
    }
}
