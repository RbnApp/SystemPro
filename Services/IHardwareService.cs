using RbnSystemPro.Models;
using System.Threading.Tasks;

namespace RbnSystemPro.Services
{
    public interface IHardwareService
    {
        Task<HardwareInfo> GetSystemInfoAsync();
    }
}
