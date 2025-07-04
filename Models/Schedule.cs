using Kutip.Data;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace Kutip.Models
{
    public class Schedule
    {
        [Key]
        public int s_ID { get; set; }

        [Required]
        [Display(Name = "Assigned Driver")]
        [ForeignKey("AssignedUser")]
        public string AssignedUser_ID { get; set; }
        public virtual ApplicationUser AssignedUser { get; set; }

        [Required]
        [Display(Name = "Bin")]
        [ForeignKey("Bin")]
        public int b_ID { get; set; }
        public virtual Bin Bin { get; set; }

        [Required]
        [Display(Name = "Assigned Truck")]
        [ForeignKey("Truck")]
        public int t_ID { get; set; }
        public virtual Truck? Truck { get; set; } 

        [Required]
        [Display(Name = "Pickup Date")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime s_Date { get; set; }

        [NotMapped]
        [Display(Name = "Day")]
        public string s_Day => s_Date.ToString("dddd", new CultureInfo("en-US"));

        [Required]
        [Display(Name = "Pickup Start")]
        [DataType(DataType.Time)]
        [DisplayFormat(DataFormatString = "{0:hh\\:mm}", ApplyFormatInEditMode = true)]
        public TimeSpan s_PickupTime { get; set; }

        [Required]
        [Display(Name = "Pickup End")]
        [DataType(DataType.Time)]
        [DisplayFormat(DataFormatString = "{0:hh\\:mm}", ApplyFormatInEditMode = true)]
        public TimeSpan s_PickupEnd { get; set; }

        [NotMapped]
        [Display(Name = "Scheduled Time")]
        public string ScheduledTime => $"{s_PickupTime.ToString(@"hh\:mm")} - {s_PickupEnd.ToString(@"hh\:mm")}";

        [Required]
        [ForeignKey("Location")]
        public int l_ID { get; set; }
        public virtual Location? Location { get; set; }

        [NotMapped]
        [Display(Name = "Address Line 1")]
        public string l_Address1 => Location?.l_Address1 ?? "";

        [NotMapped]
        [Display(Name = "Address Line 2")]
        public string l_Address2 => Location?.l_Address2 ?? "";

        [Required]
        [Display(Name = "Picked-up Bins")]
        public int PickedUpBins { get; set; }

        [Required]
        [Display(Name = "Total Bins")]
        public int TotalBins { get; set; }

        [NotMapped]
        [Display(Name = "Pickup Status")]
        public string PickupStatus => $"{PickedUpBins}/{TotalBins}";

        [Display(Name = "Image URL")]
        public string? s_ImageUrl { get; set; }

        [Display(Name = "Actual Pickup Time")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime? s_ActualPickupTimestamp { get; set; }
    }
}
