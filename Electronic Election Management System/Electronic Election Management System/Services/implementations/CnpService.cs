using System.Text.RegularExpressions;

namespace Electronic_Election_Management_System.Services
{
    /// <summary>
    /// CNP format: S YY MM DD JJ NNN C (13 digits).
    /// S = sex+century digit, YY/MM/DD = birth date, JJ = county code, NNN = sequence, C = check digit.
    /// This is the publicly documented Romanian personal-numeric-code algorithm (same one used by
    /// banks/e-government forms for format validation), not anything election-specific.
    /// </summary>
    public class CnpService : ICnpService
    {
        private static readonly int[] ControlKey = { 2, 7, 9, 1, 4, 6, 3, 5, 8, 2, 7, 9 };

        private static readonly Dictionary<string, string> CountyNames = new()
        {
            ["01"] = "Alba", ["02"] = "Arad", ["03"] = "Argeș", ["04"] = "Bacău",
            ["05"] = "Bihor", ["06"] = "Bistrița-Năsăud", ["07"] = "Botoșani", ["08"] = "Brașov",
            ["09"] = "Brăila", ["10"] = "Buzău", ["11"] = "Caraș-Severin", ["12"] = "Cluj",
            ["13"] = "Constanța", ["14"] = "Covasna", ["15"] = "Dâmbovița", ["16"] = "Dolj",
            ["17"] = "Galați", ["18"] = "Gorj", ["19"] = "Harghita", ["20"] = "Hunedoara",
            ["21"] = "Ialomița", ["22"] = "Iași", ["23"] = "Ilfov", ["24"] = "Maramureș",
            ["25"] = "Mehedinți", ["26"] = "Mureș", ["27"] = "Neamț", ["28"] = "Olt",
            ["29"] = "Prahova", ["30"] = "Satu Mare", ["31"] = "Sălaj", ["32"] = "Sibiu",
            ["33"] = "Suceava", ["34"] = "Teleorman", ["35"] = "Timiș", ["36"] = "Tulcea",
            ["37"] = "Vaslui", ["38"] = "Vâlcea", ["39"] = "Vrancea",
            ["40"] = "București", ["41"] = "București - Sector 1", ["42"] = "București - Sector 2",
            ["43"] = "București - Sector 3", ["44"] = "București - Sector 4",
            ["45"] = "București - Sector 5", ["46"] = "București - Sector 6",
            ["51"] = "Călărași", ["52"] = "Giurgiu",
        };

        public bool IsValid(string cnp) => Parse(cnp) is not null;

        public CnpInfo? Parse(string cnp)
        {
            if (string.IsNullOrWhiteSpace(cnp) || !Regex.IsMatch(cnp, @"^\d{13}$"))
                return null;

            int[] d = cnp.Select(c => c - '0').ToArray();

            (int century, string gender) = d[0] switch
            {
                1 => (1900, "M"),
                2 => (1900, "F"),
                3 => (1800, "M"),
                4 => (1800, "F"),
                5 => (2000, "M"),
                6 => (2000, "F"),
                7 => (1900, "M"), // resident foreigner
                8 => (1900, "F"),
                _ => (0, string.Empty)
            };
            if (century == 0)
                return null;

            int year = century + d[1] * 10 + d[2];
            int month = d[3] * 10 + d[4];
            int day = d[5] * 10 + d[6];
            string countyCode = $"{d[7]}{d[8]}";

            if (!CountyNames.TryGetValue(countyCode, out var countyName))
                return null;

            DateOnly birthDate;
            try
            {
                birthDate = new DateOnly(year, month, day);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null; // e.g. 30 februarie
            }
            if (birthDate > DateOnly.FromDateTime(DateTime.UtcNow))
                return null; // birth date in the future

            int checksum = 0;
            for (int i = 0; i < 12; i++)
                checksum += d[i] * ControlKey[i];

            int remainder = checksum % 11;
            int expectedControlDigit = remainder == 10 ? 1 : remainder;
            if (expectedControlDigit != d[12])
                return null;

            return new CnpInfo(birthDate, gender, countyCode, countyName);
        }
    }
}
