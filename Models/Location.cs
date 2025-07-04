using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        [Display(Name = "Latitude")]
        [Column(TypeName = "decimal(18, 10)")]
        public double Latitude { get; set; }

        [Display(Name = "Longitude")]
        [Column(TypeName = "decimal(18, 10)")]
        public double Longitude { get; set; }

        public virtual ICollection<Bin> Bins { get; set; } = new List<Bin>();
    }
}
