using System;
using System.Threading.Tasks;
using Electronic_Election_Management_System.Models;

namespace Electronic_Election_Management_System.Services
{
    /// <summary>
    /// Keeps a user's geographic labels in step with the residence on their profile: the country,
    /// the county, and the city, each as a node of the geographic tree.
    /// </summary>
    public interface IResidenceLabelSync
    {
        /// <summary>
        /// Links the user to every level of their residence and unlinks any geographic label they
        /// no longer live under. Labels outside the tree are never touched.
        /// </summary>
        /// <remarks>
        /// A level is linked only when the tree confirms it - the county has to sit under the
        /// chosen country - so a request naming an unrelated pair of codes gains nothing past the
        /// last level that holds. A city not yet in the tree is added under its county.
        /// </remarks>
        Task SyncAsync(Guid userId, UserDetails details);
    }
}
