// Models/Schedule.cs
using Kutip.Data;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace Kutip.Models
{
    public class Schedule
    {
        // Schedule ID
        [Key]
        public int s_ID { get; set; }

       
        [Required]
        [Display(Name = "Assigned Driver and Collector")]
        [ForeignKey("AssignedUser")]
        public string AssignedUser_ID { get; set; }
        public virtual ApplicationUser AssignedUser { get; set; }


        // Bin ID (FOREIGN)
        [Required]
        [Display(Name = "Bin")]
        [ForeignKey("Bin")] 
        public int b_ID { get; set; }
        public virtual Bin Bin { get; set; }


        // Schedule Date
        [Required]
        [Display(Name = "Pickup Date")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime s_Date { get; set; }


        // Schedule Day (NotMapped)
        [NotMapped]
        [Display(Name = "Day")]
        public string s_Day => s_Date.ToString("dddd", new CultureInfo("en-US"));


        // Schedule Pickup Time
        [Required]
        [Display(Name = "Pickup Start")]
        [DataType(DataType.Time)]
        [DisplayFormat(DataFormatString = "{0:hh\\:mm}", ApplyFormatInEditMode = true)]
        public TimeSpan s_PickupTime { get; set; }
        //public string s_PickupTime { get; set; } = "";

        // Schedule Pickup End Time
        [Required]
        [Display(Name = "Pickup End")]
        [DataType(DataType.Time)]
        [DisplayFormat(DataFormatString = "{0:hh\\:mm}", ApplyFormatInEditMode = true)]
        public TimeSpan s_PickupEnd { get; set; }
        //public string s_PickupEnd { get; set; } = "";


        // Schedule Time (NotMapped)
        [NotMapped]
        [Display(Name = "Scheduled Time")]
        public string ScheduledTime => $"{s_PickupTime.ToString(@"hh\:mm")} - {s_PickupEnd.ToString(@"hh\:mm")}";


        // Location ID (FOREIGN)
        [Required]
        [ForeignKey("Location")]
        public int l_ID { get; set; }
        public virtual Location? Location { get; set; }


        // Location Address 1 and 2 (NotMapped)
        [NotMapped]
        [Display(Name = "Address Line 1")]
        public string l_Address1 => Location?.l_Address1 ?? "";

        [NotMapped]
        [Display(Name = "Address Line 2")]
        public string l_Address2 => Location?.l_Address2 ?? "";


        // PickedUp and Total bins
        [Required] 
        [Display(Name = "Picked-up Bins")]
        public int PickedUpBins { get; set; }

        [Required] 
        [Display(Name = "Total Bins")]
        public int TotalBins { get; set; }


        // Pickup Status (NotMapped)
        [NotMapped]
        [Display(Name = "Pickup Status")]
        public string PickupStatus => $"{PickedUpBins}/{TotalBins}";
    }
}