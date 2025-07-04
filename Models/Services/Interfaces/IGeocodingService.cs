using Kutip.Models; 
using System.Threading.Tasks;

namespace Kutip.Services.Interfaces
{
    public interface IGeocodingService
    {
        Task<Coordinates> GetCoordinates(string address);
    }
}
