namespace Electronic_Election_Management_System.Services
{
    public record CnpInfo(DateOnly BirthDate, string Gender, string CountyCode, string CountyName);

    /// <summary>
    /// Validates and decodes Romanian CNPs (Cod Numeric Personal).
    /// Used so voters in a Politic election only have to type their CNP once -
    /// birth date, gender and county are derived from it rather than typed by hand.
    /// </summary>
    public interface ICnpService
    {
        /// <summary>True if the CNP has a valid format and control digit.</summary>
        bool IsValid(string cnp);

        /// <summary>Parses a valid CNP. Returns null if the CNP is malformed or fails the checksum.</summary>
        CnpInfo? Parse(string cnp);
    }
}
