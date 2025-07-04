using System.Collections.Generic;

namespace Kutip.Models
{
    public class ScheduleDetailsViewModel
    {
        public Schedule MainSchedule { get; set; }
        public List<Schedule> RelatedSchedules { get; set; }
        public bool IsOperator { get; set; }

        public int TotalBins => RelatedSchedules?.Count ?? 0;
        public int TotalPickedUpBins => RelatedSchedules?.Sum(s => s.PickedUpBins) ?? 0;
        public double CompletionPercentage => TotalBins > 0 ? (double)TotalPickedUpBins / TotalBins * 100 : 0;
        public string Status
        {
            get
            {
                if (TotalPickedUpBins >= TotalBins && TotalBins > 0)
                    return "Completed";
                else if (MainSchedule.s_Date.Date < DateTime.Today.Date)
                    return "Past Due";
                else if (MainSchedule.s_Date.Date == DateTime.Today.Date)
                    return "In Progress";
                else
                    return "Scheduled";
            }
        }
    }
}
