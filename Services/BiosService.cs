using System.Web;

namespace RbnSystemPro.Services
{
    public class BiosService : IBiosService
    {
        public string GetBiosSearchUrl(string manufacturer, string product)
        {
            // Create a smart search query
            var query = $"{manufacturer} {product} BIOS Update Support Download";
            return $"https://www.google.com/search?q={HttpUtility.UrlEncode(query)}";
        }
    }
}
