using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace Kutip.ViewModels
{
    public class EditUserInputModel
    {
        [Required]
        public string Id { get; set; } 

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        [Display(Name = "Full Name")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Role is required.")]
        [Display(Name = "Role")]
        public string RoleName { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at most {1} characters long.", MinimumLength = 6)]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
        public string ConfirmNewPassword { get; set; }
    }

    public class UserViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        [Display(Name = "Email Confirmed")]
        public bool EmailConfirmed { get; set; }
    }

    public class GroupedScheduleSummaryViewModel
    {
        public DateTime ScheduleDate { get; set; }
        public TimeSpan PickupTime { get; set; }
        public TimeSpan PickupEnd { get; set; }
        public string LocationAddress2 { get; set; }
        public string TruckPlateNo { get; set; }
        public int TotalBinsScheduled { get; set; }
        public int TotalBinsCollected { get; set; }
        public string Status { get; set; }
        public string AssignedUserName { get; set; }
    }
}
