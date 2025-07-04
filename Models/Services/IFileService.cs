using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Kutip.Services
{
    public interface IFileService
    {
        Tuple<int, string> SaveImage(IFormFile imageFile);
        public bool DeleteImage(string imageFileName);

    }
}
