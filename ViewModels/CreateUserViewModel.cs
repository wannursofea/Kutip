// Kutip.ViewModels/CreateUserViewModel.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering; // For SelectListItem

namespace Kutip.ViewModels
{
    public class CreateUserViewModel
    {
        [Required]
        [Display(Name = "Full Name")]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required]
        [Display(Name = "Role")]
        public string Role { get; set; }

        // Optional: If you want to populate roles directly in the ViewModel
        // public List<SelectListItem> AvailableRoles { get; set; }
    }
}