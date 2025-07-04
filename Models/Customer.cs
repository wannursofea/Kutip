using Humanizer;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Kutip.Models
{
    public class Customer
    {
        public Customer() { }

        [Key]
        public int c_ID { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [Display(Name = "Customer Name")]
        public string c_Name { get; set; } = "";

        [Required(ErrorMessage = "Contact Number is required.")]
        [Display(Name = "Contact Number")]
        public string c_ContactNo { get; set; } = "";

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string c_Email { get; set; } = "";

        public virtual ICollection<Bin> Bins { get; set; } = new List<Bin>();
    }
}
