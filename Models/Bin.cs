using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kutip.Models
{
    public class Bin
    {
        [Key]
        public int b_ID { get; set; }
        
        [Required]
        [Display(Name = "Plate Number")]
        public string b_PlateNo { get; set; }
        
        [Required]
        [Display(Name = "Customer")]
        public int c_ID { get; set; }
        
        [Required]
        [Display(Name = "Location")]
        public int l_ID { get; set; }
        
        [ForeignKey("c_ID")]
        public virtual Customer? Customer { get; set; }
        
        [ForeignKey("l_ID")]
        public virtual Location? Location { get; set; }
    }
}
