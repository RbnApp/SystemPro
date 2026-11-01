using System.Collections.Generic;

namespace RbnSystemPro.Helpers
{
    public static class HardwareConstants
    {
        public static readonly Dictionary<string, string> ManufacturerMappings = new()
        {
            { "Micro-Star International Co., Ltd.", "MSI" },
            { "ASUSTeK COMPUTER INC.", "ASUS" },
            { "Gigabyte Technology Co., Ltd.", "GIGABYTE" },
            { "ASRock", "ASRock" },
            { "Colorful", "Colorful" },
            { "Biostar", "Biostar" },
            { "Hewlett-Packard", "HP" },
            { "Dell Inc.", "DELL" },
            { "Lenovo", "LENOVO" },
            { "Advanced Micro Devices, Inc.", "AMD" },
            { "Intel Corporation", "Intel" }
        };

        public static string SimplifyManufacturer(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Unknown";

            foreach (var mapping in ManufacturerMappings)
            {
                if (raw.Contains(mapping.Key, System.StringComparison.OrdinalIgnoreCase))
                    return mapping.Value;
            }

            return raw;
        }

        public static string SimplifyMotherboardProduct(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Unknown Board";

            string cleaned = raw;

            // Remove everything in parentheses (often contains model codes like MS-7C52)
            if (cleaned.Contains('(') && cleaned.Contains(')'))
            {
                int start = cleaned.IndexOf('(');
                int end = cleaned.LastIndexOf(')');
                if (end > start)
                    cleaned = cleaned.Remove(start, end - start + 1);
            }

            // Remove specific patterns like MS- followed by alphanumeric characters and dashes
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"MS-[A-Z0-9-]+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Clean up extra spaces
            while (cleaned.Contains("  ")) cleaned = cleaned.Replace("  ", " ");

            return cleaned.Trim();
        }

        public static string SimplifyCpuName(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Unknown";

            // Remove common suffixes
            string simplified = raw
                .Replace("6-Core Processor", "", System.StringComparison.OrdinalIgnoreCase)
                .Replace("8-Core Processor", "", System.StringComparison.OrdinalIgnoreCase)
                .Replace("12-Core Processor", "", System.StringComparison.OrdinalIgnoreCase)
                .Replace("16-Core Processor", "", System.StringComparison.OrdinalIgnoreCase)
                .Replace("Processor", "", System.StringComparison.OrdinalIgnoreCase)
                .Replace("Core(TM)", "", System.StringComparison.OrdinalIgnoreCase)
                .Replace("CPU", "", System.StringComparison.OrdinalIgnoreCase)
                .Replace("@", "", System.StringComparison.OrdinalIgnoreCase);

            // Clean up extra spaces
            while (simplified.Contains("  ")) simplified = simplified.Replace("  ", " ");
            
            return simplified.Trim();
        }

        public static string SimplifyRamSlot(string? raw, int fallbackIndex)
        {
            // Simplify to "Slot X" based on the order found in WMI (fallbackIndex)
            // This is the most reliable way to show Slot 1, Slot 2, etc.
            return $"Slot {fallbackIndex}";
        }
    }
}
