using Kutip.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Kutip.ViewModels
{
    public class CustomerBinLocationViewModel
    {
        public Customer Customer { get; set; } = new Customer();
        public Bin Bin { get; set; } = new Bin();
        public Location Location { get; set; } = new Location();

        public List<SelectListItem> ColAreaList { get; set; } = new List<SelectListItem>();
    }
}
