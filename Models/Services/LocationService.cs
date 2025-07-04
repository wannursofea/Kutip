using Kutip.Data;
using Kutip.Models;
using Kutip.Services.Interfaces;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Kutip.Services
{
    public class LocationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IGeocodingService _geocodingService;
        private readonly ILogger<LocationService> _logger;

        public LocationService(ApplicationDbContext context, IGeocodingService geocodingService, ILogger<LocationService> logger)
        {
            _context = context;
            _geocodingService = geocodingService;
            _logger = logger;
        }

        public async Task<Location> SaveLocationWithGeocoding(Location location)
        {
            bool needsGeocoding = (location.l_ID == 0 && location.Latitude == 0 && location.Longitude == 0);


            if (needsGeocoding)
            {
                string fullAddress = $"{location.l_Address1}, {location.l_Address2}, " +
                                     $"{location.l_Postcode} {location.l_District}, " +
                                     $"{location.l_State}, Malaysia"; 

                _logger.LogInformation($"Attempting to geocode address: {fullAddress}");
                var coordinates = await _geocodingService.GetCoordinates(fullAddress);

                if (coordinates != null)
                {
                    location.Latitude = coordinates.Latitude;
                    location.Longitude = coordinates.Longitude; 
                }
                else
                {
                    _logger.LogError($"Failed to geocode address: {fullAddress}");
                    throw new InvalidOperationException("Could not geocode the address. Please check the address details and ensure it's valid.");
                }
            }

            if (location.l_ID == 0)
            {
                _context.Locations.Add(location);
            }
            else
            {
                _context.Locations.Update(location); 
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"Location {location.l_ID} saved with coordinates: {location.Latitude}, {location.Longitude}");
            return location;
        }
        public async Task SaveLocationWithoutGeocoding(Location location)
        {
            if (location.l_ID == 0)
            {
                _context.Locations.Add(location);
            }
            else
            {
                _context.Locations.Update(location);
            }
            await _context.SaveChangesAsync();
        }
    }
}