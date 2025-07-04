using System;
using System.Collections.Generic;
using Kutip.Models;
using Kutip.Data;

namespace Kutip.ViewModels
{
    public class OperatorDashboardViewModel
    {
        public int TotalCustomers { get; set; }
        public int TotalBins { get; set; }
        public int TotalLocations { get; set; }
        public int TotalSchedules { get; set; }
        public int TodaySchedules { get; set; }
        public int CompletedToday { get; set; }
        public List<Schedule> RecentSchedules { get; set; } = new List<Schedule>();
        public List<Customer> RecentCustomers { get; set; } = new List<Customer>();
        public int BinsCollected { get; set; }
        public int BinsMissed { get; set; }
        public int TotalBinsScheduled { get; set; }
        public double CollectionEfficiency { get; set; }
        public int TotalTrucks { get; set; }
        public string SelectedPeriod { get; set; } = "daily";
        public DateTime FilterStartDate { get; set; }
        public DateTime FilterEndDate { get; set; }
        public List<ChartDataPoint> CollectionTrendData { get; set; } = new List<ChartDataPoint>();
        public List<ChartDataPoint> EfficiencyTrendData { get; set; } = new List<ChartDataPoint>();
        public List<ChartDataPoint> MissedBinsTrendData { get; set; } = new List<ChartDataPoint>(); 
        public List<PieChartData> CollectionStatusData { get; set; } = new List<PieChartData>();

        public List<PeriodSummary> PeriodSummaries { get; set; } = new List<PeriodSummary>();


        public List<TruckMovementSummary> TruckMovementSummary { get; set; } = new List<TruckMovementSummary>();
        public List<DriverPerformanceSummary> DriverPerformanceSummary { get; set; } = new List<DriverPerformanceSummary>();

        public string SearchTruck { get; set; } = "";
        public string SearchDriver { get; set; } = "";

        public List<(int Id, string PlateNumber)> TruckList { get; set; } = new List<(int, string)>();
        public List<(string Id, string Name)> DriverList { get; set; } = new List<(string, string)>();
    }

    public class ChartDataPoint
    {
        public string Label { get; set; }
        public int Value { get; set; }
        public DateTime Date { get; set; }
    }

    public class PieChartData
    {
        public string Label { get; set; }
        public int Value { get; set; }
        public string Color { get; set; }
    }

    public class PeriodSummary
    {
        public string Period { get; set; }
        public int BinsCollected { get; set; }
        public int BinsMissed { get; set; }
        public int TotalScheduled { get; set; }
        public double Efficiency { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }


    public class TruckMovementSummary
    {
        public int TruckId { get; set; }
        public string PlateNumber { get; set; } = "";
        public int TotalSchedules { get; set; }
        public int CompletedSchedules { get; set; }
        public int TotalBinsCollected { get; set; }
        public int TotalBinsScheduled { get; set; }
        public int UniqueLocations { get; set; }
        public int ActiveDays { get; set; }
        public DateTime LastActivity { get; set; }

        public double CompletionRate => TotalSchedules > 0 ? (double)CompletedSchedules / TotalSchedules * 100 : 0;
        public double CollectionEfficiency => TotalBinsScheduled > 0 ? (double)TotalBinsCollected / TotalBinsScheduled * 100 : 0;
    }

    public class DriverPerformanceSummary
    {
        public string DriverId { get; set; } = "";
        public string DriverName { get; set; } = "";
        public int TotalSchedules { get; set; }
        public int CompletedSchedules { get; set; }
        public int TotalBinsCollected { get; set; }
        public int TotalBinsScheduled { get; set; }
        public int UniqueLocations { get; set; }
        public int UniqueTrucks { get; set; }
        public int ActiveDays { get; set; }
        public DateTime LastActivity { get; set; }
        public double AverageCollectionTime { get; set; }

        public double CompletionRate => TotalSchedules > 0 ? (double)CompletedSchedules / TotalSchedules * 100 : 0;
        public double CollectionEfficiency => TotalBinsScheduled > 0 ? (double)TotalBinsCollected / TotalBinsScheduled * 100 : 0;
    }



    public class DriverDashboardViewModel
    {
        public List<Schedule> TodaySchedules { get; set; } = new List<Schedule>();
        public List<Schedule> WeeklySchedules { get; set; } = new List<Schedule>();
        public int CompletedToday { get; set; }
        public int PendingToday { get; set; }
        public int TotalSchedulesToday { get; set; }

        public List<ChartDataPoint> DriverEfficiencyTrendData { get; set; } = new List<ChartDataPoint>();
        public List<ChartDataPoint> DriverDistanceTrendData { get; set; } = new List<ChartDataPoint>(); // This is the missing property
        public int TotalTripsCompleted { get; set; }
        public double AverageBinsPerTrip { get; set; }
        public double OnTimePerformance { get; set; }
        public double TotalDistanceDriven { get; set; }

        public string SelectedPeriod { get; set; }
        public DateTime FilterStartDate { get; set; }
        public DateTime FilterEndDate { get; set; }
    }

    public class CollectorDashboardViewModel
    {
        public List<Schedule> TodayCollections { get; set; } = new List<Schedule>();
        public List<Schedule> WeeklyCollections { get; set; } = new List<Schedule>();
        public int CompletedToday { get; set; }
        public int PendingToday { get; set; }
        public int WeeklyCompleted { get; set; }
    }
}
