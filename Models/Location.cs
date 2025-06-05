using System.ComponentModel.DataAnnotations;

namespace Kutip.Models
{
    public class Location
    {
        public Location() { }

        [Key]
        public int l_ID { get; set; }

        [Required]
        [Display(Name = "Address Line 1")]
        public string l_Address1 { get; set; } = "";

        [Display(Name = "Address Line 2")]
        public string l_Address2 { get; set; } = "";

        [Required]
        [Display(Name = "Postcode")]
        public string l_Postcode { get; set; } = "";

        [Required]
        [Display(Name = "District")]
        public string l_District { get; set; } = "";

        [Required]
        [Display(Name = "State")]
        public string l_State { get; set; } = "";

        [Required]
        [Display(Name = "Collection Area")]
        public string l_ColArea { get; set; } = "";

        // Navigation property to Bins
        public virtual ICollection<Bin> Bins { get; set; } = new List<Bin>();
    }
}

