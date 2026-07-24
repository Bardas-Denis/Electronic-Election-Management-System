using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Services
{
    /// <summary>
    /// Maps <see cref="PersonalDetailsDto"/> fields onto entity targets.
    /// </summary>
    public static class PersonalDetailsMapper
    {
        /// <summary>
        /// Copies <see cref="PersonalDetailsDto"/> fields onto a <see cref="VoterDeclaration"/>.
        /// </summary>
        public static void ApplyTrimmed(PersonalDetailsDto source, VoterDeclaration target)
        {
            target.Cnp               = Trim(source.Cnp);
            target.FullName          = Trim(source.FullName);
            target.ResidenceCounty   = Trim(source.ResidenceCounty);
            target.ResidenceAddress  = Trim(source.ResidenceAddress);
            target.ResidenceCity     = Trim(source.ResidenceCity);
            target.Citizenship       = Trim(source.Citizenship);
            target.Gender            = Trim(source.Gender);
            target.WorkEmail         = Trim(source.WorkEmail);
            target.EmployeeId        = Trim(source.EmployeeId);
            target.Department        = Trim(source.Department);
            target.JobTitle          = Trim(source.JobTitle);
            target.Company           = Trim(source.Company);
        }

        /// <summary>
        /// Copies <see cref="PersonalDetailsDto"/> fields onto a <see cref="UserDetails"/>.
        /// </summary>
        public static void ApplyTrimmed(PersonalDetailsDto source, UserDetails target)
        {
            target.Cnp               = Trim(source.Cnp);
            target.FullName          = Trim(source.FullName);
            target.ResidenceCounty   = Trim(source.ResidenceCounty);
            target.ResidenceAddress  = Trim(source.ResidenceAddress);
            target.ResidenceCity     = Trim(source.ResidenceCity);
            target.Citizenship       = Trim(source.Citizenship);
            target.Gender            = Trim(source.Gender);
            target.WorkEmail         = Trim(source.WorkEmail);
            target.EmployeeId        = Trim(source.EmployeeId);
            target.Department        = Trim(source.Department);
            target.JobTitle          = Trim(source.JobTitle);
            target.Company           = Trim(source.Company);
            target.UpdatedAt         = DateTime.UtcNow;
        }

        // Null/whitespace-only strings are stored as null rather than empty strings.
        private static string? Trim(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
