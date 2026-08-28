using System;

namespace Electronic_Election_Management_System.Models
{
    /// <summary>
    /// A ballot image, stored out-of-row so that reading an election does not carry its pictures.
    /// Uploads happen before the election exists, so a row starts as a draft with a null
    /// <see cref="ElectionId"/> and is claimed when the election is saved.
    /// </summary>
    public class ElectionImage
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Null while the image is an unclaimed draft.</summary>
        public Guid? ElectionId { get; set; }
        public Election? Election { get; set; }

        /// <summary>Authorizes reads of a draft, and is what claiming verifies.</summary>
        public Guid UploadedByUserId { get; set; }
        public User? UploadedByUser { get; set; }

        /// <summary>
        /// Hex-encoded SHA-256 of <see cref="Content"/>, used as the ETag. Deliberately not a
        /// deduplication key: one row shared between elections would leave no single election to
        /// authorize reads against, which closed elections rely on.
        /// </summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>Always the re-encoded output type, never whatever the client uploaded.</summary>
        public string ContentType { get; set; } = string.Empty;

        public int ByteSize { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>Re-encoded bytes, so the stored image is exactly what voters are shown.</summary>
        public byte[] Content { get; set; } = Array.Empty<byte>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
