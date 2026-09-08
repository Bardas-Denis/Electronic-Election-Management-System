namespace Electronic_Election_Management_System.Constants
{
    /// <summary>
    /// The controlled vocabulary for <see cref="Models.Label.Category"/> on geographic labels,
    /// which form a tree via <see cref="Models.Label.ParentId"/>. Labels outside the tree
    /// (an employer, an interest) carry a free-form category or none at all, and are unaffected.
    /// <para>
    /// Depth alone cannot name a level, because countries are not subdivided alike: Germany's
    /// first level is a state, France's is a department. The category says what a node *is*;
    /// the parent chain says where it sits.
    /// </para>
    /// </summary>
    public static class LabelCategories
    {
        /// <summary>A sovereign state or dependent territory. Always a root — never has a parent.</summary>
        public const string Country = "country";

        /// <summary>
        /// A first-level division of a country: county, state, province, department, region.
        /// Parent is always a <see cref="Country"/>.
        /// </summary>
        public const string Subdivision = "subdivision";

        /// <summary>
        /// A place inside a subdivision: city, town, commune, sector. Not seeded — there are
        /// millions worldwide and no free dataset small enough to ship. Administrators add the
        /// ones an election actually needs, under the right subdivision.
        /// </summary>
        public const string Locality = "locality";

        /// <summary>
        /// The geographic categories as an array, so a query can exclude the tree with a single
        /// translatable Contains rather than three separate comparisons.
        /// </summary>
        public static readonly string[] Geographic = { Country, Subdivision, Locality };
        /// <summary>True when the category places a label in the geographic tree.</summary>
        public static bool IsGeographic(string? category) =>
            category is Country or Subdivision or Locality;
    }
}
