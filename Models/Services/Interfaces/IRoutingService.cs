using Kutip.Models; 
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kutip.Services.Interfaces
{
    public interface IRoutingService
    {
        Task<double?> GetDrivingDistance(Coordinates origin, Coordinates destination);

        Task<DistanceMatrixResult?> GetDistanceMatrix(List<Coordinates> origins, List<Coordinates> destinations);
    }

    public class DistanceMatrixResult
    {
        public List<string>? OriginAddresses { get; set; }
        public List<string>? DestinationAddresses { get; set; }
        public List<Row>? Rows { get; set; }
        public string? Status { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class Row
    {
        public List<Element>? Elements { get; set; }
    }

    public class Element
    {
        public Distance? Distance { get; set; }
        public Duration? Duration { get; set; }
        public string? Status { get; set; }
    }

    public class Distance
    {
        public string? Text { get; set; }
        public int Value { get; set; }
    }

    public class Duration
    {
        public string? Text { get; set; }
        public int Value { get; set; }
    }
}