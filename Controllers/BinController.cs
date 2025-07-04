using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Kutip.Data;
using Kutip.Models;
using Microsoft.AspNetCore.Authorization;
using System;
namespace Kutip.Controllers
{
    [Authorize]
    public class BinController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BinController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var bins = _context.Bins
                .Include(b => b.Customer)
                .Include(b => b.Location);
            return View(await bins.ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bin = await _context.Bins
                .Include(b => b.Customer)
                .Include(b => b.Location)
                .FirstOrDefaultAsync(m => m.b_ID == id);
            if (bin == null)
            {
                return NotFound();
            }

            return View(bin);
        }

        public IActionResult Create()
        {
            ViewData["c_ID"] = new SelectList(_context.Customers, "c_ID", "c_Name");
            ViewData["l_ID"] = new SelectList(_context.Locations, "l_ID", "l_Address1");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("b_ID,b_PlateNo,c_ID,l_ID")] Bin bin)
        {
            if (ModelState.IsValid)
            {
                _context.Add(bin);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["c_ID"] = new SelectList(_context.Customers, "c_ID", "c_Name", bin.c_ID);
            ViewData["l_ID"] = new SelectList(_context.Locations, "l_ID", "l_Address1", bin.l_ID);
            return View(bin);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bin = await _context.Bins
                .Include(b => b.Location) 
                .FirstOrDefaultAsync(m => m.b_ID == id);

            if (bin == null)
            {
                return NotFound();
            }

            ViewData["c_ID"] = new SelectList(_context.Customers, "c_ID", "c_Name", bin.c_ID);
            ViewBag.CurrentLatitude = bin.Location?.Latitude ?? 0; 
            ViewBag.CurrentLongitude = bin.Location?.Longitude ?? 0; 
            ViewBag.CurrentAddress = bin.Location?.l_Address1;

            return View(bin);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("b_ID,b_PlateNo,c_ID,l_ID")] Bin bin, double latitude, double longitude) 
        {
            if (id != bin.b_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(bin);

                    var locationToUpdate = await _context.Locations.FindAsync(bin.l_ID);
                    if (locationToUpdate != null)
                    {
                        locationToUpdate.Latitude = latitude;
                        locationToUpdate.Longitude = longitude;
                        _context.Update(locationToUpdate);
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BinExists(bin.b_ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["c_ID"] = new SelectList(_context.Customers, "c_ID", "c_Name", bin.c_ID);
            return View(bin);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bin = await _context.Bins
                .Include(b => b.Customer)
                .Include(b => b.Location)
                .FirstOrDefaultAsync(m => m.b_ID == id);
            if (bin == null)
            {
                return NotFound();
            }

            return View(bin);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bin = await _context.Bins.FindAsync(id);
            if (bin != null)
            {
                _context.Bins.Remove(bin);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool BinExists(int id)
        {
            return _context.Bins.Any(e => e.b_ID == id);
        }

        public async Task<IActionResult> RoutePlan(string address2 = null, double? currentLat = null, double? currentLng = null)
        {
            var addressesWithBins = await _context.Locations
                .Where(l => !string.IsNullOrEmpty(l.l_Address2))
                .Where(l => _context.Bins.Any(b => b.l_ID == l.l_ID))
                .Select(l => l.l_Address2)
                .Distinct()
                .ToListAsync();

            ViewBag.Addresses = new SelectList(addressesWithBins, address2);

            if (currentLat.HasValue && currentLng.HasValue)
            {
                ViewBag.CurrentLocation = new { Lat = currentLat.Value, Lng = currentLng.Value };
            }

            if (string.IsNullOrEmpty(address2) && !currentLat.HasValue)
            {
                return View(new List<Bin>());
            }

            List<Bin> bins;

            if (!string.IsNullOrEmpty(address2))
            {
                bins = await _context.Bins
                    .Include(b => b.Location)
                    .Where(b => b.Location.l_Address2 == address2)
                    .ToListAsync();

                ViewBag.SelectedAddress = address2;
            }
            else
            {
                bins = await _context.Bins
                    .Include(b => b.Location)
                    .OrderBy(b => Math.Pow(b.Location.Latitude - currentLat.Value, 2) +
                                Math.Pow(b.Location.Longitude - currentLng.Value, 2))
                    .Take(10)
                    .ToListAsync();

                ViewBag.SelectedAddress = "Near Your Location";
            }

            return View(bins);
        }

        public async Task<IActionResult> RouteOptimization(string selectedAddress2)
        {
            var addressesWithValidBins = await _context.Locations
                                                    .Where(l => !string.IsNullOrEmpty(l.l_Address2))
                                                    .Where(l => _context.Bins.Any(b => b.l_ID == l.l_ID && b.Location.Latitude != 0 && b.Location.Longitude != 0))
                                                    .Select(l => l.l_Address2)
                                                    .Distinct()
                                                    .OrderBy(addr => addr)
                                                    .ToListAsync();

            ViewBag.Address2List = new SelectList(addressesWithValidBins, selectedAddress2);

            List<Bin> bins = new List<Bin>(); 
            List<RouteData> routeSegments = new List<RouteData>(); 
            double totalDistance = 0;
            double totalDuration = 0;

            if (!string.IsNullOrEmpty(selectedAddress2))
            {
                bins = await _context.Bins
                                    .Include(b => b.Location)
                                    .Where(b => b.Location != null && b.Location.l_Address2 == selectedAddress2 &&
                                                b.Location.Latitude != 0 && b.Location.Longitude != 0) 
                                    .OrderBy(b => b.Location.l_Address1) 
                                    .ToListAsync();

                ViewBag.SelectedAddress = selectedAddress2;

                if (bins.Any())
                {
                    for (int i = 1; i < bins.Count; i++)
                    {
                        var prevBin = bins[i - 1];
                        var currentBin = bins[i];
                        var routeData = await GetRouteDurationAndDistance(
                            prevBin.Location.Latitude, prevBin.Location.Longitude,
                            currentBin.Location.Latitude, currentBin.Location.Longitude
                        );

                        routeSegments.Add(routeData);
                        totalDistance += routeData.DistanceMeters;
                        totalDuration += routeData.DurationSeconds;
                    }
                }
            }

            ViewBag.TotalDistance = new RouteData { DistanceMeters = totalDistance }; 
            ViewBag.TotalDuration = new RouteData { DurationSeconds = totalDuration }; 
            ViewBag.RouteSegments = routeSegments; 

            return View(bins);
        }

        [HttpGet]
        public async Task<JsonResult> GetBinsByAddress2(string address2)
        {
            if (string.IsNullOrEmpty(address2))
            {
                return Json(new List<object>());
            }

            var bins = await _context.Bins
                                    .Include(b => b.Location)
                                    .Where(b => b.Location != null && b.Location.l_Address2 == address2 &&
                                                b.Location.Latitude != 0 && b.Location.Longitude != 0) 
                                    .Select(b => new
                                    {
                                        BinId = b.b_ID,
                                        PlateNo = b.b_PlateNo,
                                        Latitude = b.Location.Latitude,
                                        Longitude = b.Location.Longitude,
                                        Address1 = b.Location.l_Address1,
                                        Address2 = b.Location.l_Address2
                                    })
                                    .ToListAsync<object>();

            return Json(bins);
        }

        public class RouteData
        {
            public double DistanceMeters { get; set; }
            public double DurationSeconds { get; set; }
            public string FormattedDistance => $"{DistanceMeters / 1000:F2} km";
            public string FormattedDuration
            {
                get
                {
                    TimeSpan t = TimeSpan.FromSeconds(DurationSeconds);
                    if (t.TotalHours >= 1)
                    {
                        return $"{(int)t.TotalHours} hr {t.Minutes} min";
                    }
                    else if (t.TotalMinutes >= 1)
                    {
                        return $"{t.Minutes} min {t.Seconds} sec";
                    }
                    else
                    {
                        return $"{t.Seconds} sec";
                    }
                }
            }
        }

        private async Task<RouteData> GetRouteDurationAndDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371e3;
            var phi1 = lat1 * Math.PI / 180;
            var phi2 = lat2 * Math.PI / 180;
            var deltaPhi = (lat2 - lat1) * Math.PI / 180;
            var deltaLambda = (lon2 - lon1) * Math.PI / 180;

            var a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) +
                    Math.Cos(phi1) * Math.Cos(phi2) *
                    Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            var distance = R * c;

            const double averageSpeedMps = 30.0 * 1000 / 3600; 
            var duration = distance / averageSpeedMps; 

            return new RouteData
            {
                DistanceMeters = distance,
                DurationSeconds = duration
            };
        }
    }
}
