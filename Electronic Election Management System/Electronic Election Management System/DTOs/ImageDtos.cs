using System;

namespace Electronic_Election_Management_System.DTOs
{
    /// <summary>
    /// Result of <c>POST /api/images</c>. The dimensions are those of the re-encoded image, which
    /// may differ from the file the client sent.
    /// </summary>
    // SYNC: election-image.service.ts -> ImageUploadResultDto
    public class ImageUploadResultDto
    {
        public Guid Id { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int ByteSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Everything a read needs except the bytes, so authorization and ETag matching both happen
    /// before anything large is loaded.
    /// </summary>
    public record ImageMetadataDto(string ContentType, string Sha256);
}
