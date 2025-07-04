using Kutip.Data;
using Kutip.Models;
using Kutip.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Kutip.Services
{
    public class BinRoutingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IRoutingService _routingService;
        private readonly ILogger<BinRoutingService> _logger;

        public BinRoutingService(ApplicationDbContext context, IRoutingService routingService, ILogger<BinRoutingService> logger)
        {
            _context = context;
            _routingService = routingService;
            _logger = logger;
        }

        public async Task<List<Bin>> GetBinsInArea(string collectionArea)
        {
            return await _context.Bins
                                 .Include(b => b.Location)
                                 .Where(b => b.Location.l_ColArea == collectionArea)
                                 .ToListAsync();
        }

        public async Task<List<BinDistance>> GetPairwiseBinDistancesInArea(string collectionArea)
        {
            var bins = await GetBinsInArea(collectionArea);

            if (!bins.Any())
            {
                return new List<BinDistance>();
            }

            var locations = bins.Select(b => b.Location).DistinctBy(l => l.l_ID).ToList();
            var coordinates = locations.Select(l => new Coordinates { Latitude = l.Latitude, Longitude = l.Longitude }).ToList();

            if (coordinates.Count < 2) 
            {
                return new List<BinDistance>();
            }

            var distanceMatrix = await _routingService.GetDistanceMatrix(coordinates, coordinates);

            var binDistances = new List<BinDistance>();

            if (distanceMatrix?.Status == "OK" && distanceMatrix.Rows != null)
            {
                for (int i = 0; i < locations.Count; i++)
                {
                    for (int j = i + 1; j < locations.Count; j++) 
                    {
                        var originLocation = locations[i];
                        var destinationLocation = locations[j];
                        var element = distanceMatrix.Rows[i]?.Elements?[j];

                        if (element?.Status == "OK" && element.Distance != null)
                        {
                            binDistances.Add(new BinDistance
                            {
                                FromBinId = bins.First(b => b.l_ID == originLocation.l_ID).b_ID,
                                FromAddress = originLocation.l_Address1,
                                ToBinId = bins.First(b => b.l_ID == destinationLocation.l_ID).b_ID,
                                ToAddress = destinationLocation.l_Address1,
                                DistanceMeters = element.Distance.Value,
                                DistanceText = element.Distance.Text,
                                DurationSeconds = element.Duration?.Value,
                                DurationText = element.Duration?.Text
                            });
                        }
                        else
                        {
                            _logger.LogWarning($"Failed to get distance for {originLocation.l_Address1} to {destinationLocation.l_Address1}. Status: {element?.Status}");
                        }
                    }
                }
            }
            else
            {
                _logger.LogError($"Distance Matrix API call failed or returned non-OK status: {distanceMatrix?.Status}");
            }

            return binDistances;
        }
        public class BinDistance
        {
            public int FromBinId { get; set; }
            public string? FromAddress { get; set; }
            public int ToBinId { get; set; }
            public string? ToAddress { get; set; }
            public double DistanceMeters { get; set; }
            public string? DistanceText { get; set; } 
            public int? DurationSeconds { get; set; }
            public string? DurationText { get; set; } 
        }
    }
}