using System.Buffers.Binary;
using System.IO.Compression;
using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services.implementations;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SkiaSharp;

namespace Electronic_Election_Management_System.Tests.Services;

public class ImageServiceTests
{
    private readonly IElectionImageRepository _images = Substitute.For<IElectionImageRepository>();
    private readonly IElectionRepository _elections = Substitute.For<IElectionRepository>();
    private readonly ILogger<ImageService> _logger = Substitute.For<ILogger<ImageService>>();
    private readonly ImageService _service;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly Guid ElectionId = Guid.NewGuid();

    public ImageServiceTests()
    {
        _service = new ImageService(_images, _elections, _logger);
    }

    // ── Upload ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_WithValidPng_StoresReEncodedWebp()
    {
        var png = CreatePng(200, 100);
        ElectionImage? stored = null;
        _images.AddAsync(Arg.Do<ElectionImage>(argument => stored = argument));

        var result = await _service.UploadAsync(new MemoryStream(png), png.Length, UserId);

        result.Success.Should().BeTrue();
        result.Data!.ContentType.Should().Be("image/webp");

        stored.Should().NotBeNull();
        var image = stored!;
        image.ContentType.Should().Be("image/webp");
        image.UploadedByUserId.Should().Be(UserId);
        // A draft until the election referencing it is saved.
        image.ElectionId.Should().BeNull();
        image.ByteSize.Should().Be(image.Content.Length);
        image.Sha256.Should().HaveLength(64);
        // The original PNG bytes must not survive - only the re-encoded output is kept.
        image.Content.Should().NotEqual(png);
        SKBitmap.Decode(image.Content).Should().NotBeNull("the stored bytes must be a readable image");
    }

    [Fact]
    public async Task UploadAsync_WithImageLargerThanTheLimit_DownscalesPreservingAspectRatio()
    {
        var png = CreatePng(2400, 1200);
        ElectionImage? stored = null;
        _images.AddAsync(Arg.Do<ElectionImage>(argument => stored = argument));

        var result = await _service.UploadAsync(new MemoryStream(png), png.Length, UserId);

        result.Success.Should().BeTrue();
        result.Data!.Width.Should().Be(ValidationRules.ImageMaxDimension);
        result.Data.Height.Should().Be(ValidationRules.ImageMaxDimension / 2);
        stored!.Width.Should().Be(ValidationRules.ImageMaxDimension);
        stored.Height.Should().Be(ValidationRules.ImageMaxDimension / 2);
    }

    [Fact]
    public async Task UploadAsync_WithImageBelowTheLimit_KeepsOriginalDimensions()
    {
        var png = CreatePng(300, 180);
        ElectionImage? stored = null;
        _images.AddAsync(Arg.Do<ElectionImage>(argument => stored = argument));

        var result = await _service.UploadAsync(new MemoryStream(png), png.Length, UserId);

        result.Success.Should().BeTrue();
        stored!.Width.Should().Be(300);
        stored.Height.Should().Be(180);
    }

    [Fact]
    public async Task UploadAsync_WithHugeDeclaredDimensions_IsRejectedBeforeDecoding()
    {
        // A few hundred bytes claiming 40000x40000 - gigabytes of pixel buffer if it decoded.
        var bomb = CreatePngHeader(40_000, 40_000);
        ((long)40_000 * 40_000).Should().BeGreaterThan(ValidationRules.ImageMaxPixels);

        var result = await _service.UploadAsync(new MemoryStream(bomb), bomb.Length, UserId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.ImageTooLarge);
        await _images.DidNotReceive().AddAsync(Arg.Any<ElectionImage>());
    }

    [Fact]
    public async Task UploadAsync_WithBytesThatAreNotAnImage_FailsWithInvalidImage()
    {
        // Passes any extension or content-type check, but cannot be decoded.
        var notAnImage = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script/></svg>"u8.ToArray();

        var result = await _service.UploadAsync(new MemoryStream(notAnImage), notAnImage.Length, UserId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.InvalidImage);
        await _images.DidNotReceive().AddAsync(Arg.Any<ElectionImage>());
    }

    [Fact]
    public async Task UploadAsync_WithDeclaredLengthOverTheLimit_FailsWithoutReadingTheStream()
    {
        var png = CreatePng(10, 10);

        var result = await _service.UploadAsync(
            new MemoryStream(png), ValidationRules.ImageMaxUploadBytes + 1, UserId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.ImageTooLarge);
        await _images.DidNotReceive().AddAsync(Arg.Any<ElectionImage>());
    }

    [Fact]
    public async Task UploadAsync_WithEmptyUpload_FailsWithImageTooLarge()
    {
        var result = await _service.UploadAsync(new MemoryStream(), 0, UserId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.ImageTooLarge);
    }

    // ── Read authorization ───────────────────────────────────────────────────

    [Fact]
    public async Task GetMetadataForUserAsync_WithOwnDraft_ReturnsMetadata()
    {
        var id = Guid.NewGuid();
        _images.GetAccessInfoAsync(id).Returns(Access(UserId));

        var result = await _service.GetMetadataForUserAsync(id, UserId);

        result.Success.Should().BeTrue();
        result.Data!.ContentType.Should().Be("image/webp");
        result.Data.Sha256.Should().HaveLength(64);
    }

    [Fact]
    public async Task GetMetadataForUserAsync_WithAnotherUsersDraft_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _images.GetAccessInfoAsync(id).Returns(Access(OtherUserId));

        var result = await _service.GetMetadataForUserAsync(id, UserId);

        result.Success.Should().BeFalse();
        result.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task GetMetadataForUserAsync_WithMissingImage_ReturnsNotFound()
    {
        _images.GetAccessInfoAsync(Arg.Any<Guid>()).Returns((ImageAccessInfo?)null);

        var result = await _service.GetMetadataForUserAsync(Guid.NewGuid(), UserId);

        result.Success.Should().BeFalse();
        result.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task GetMetadataForUserAsync_WithAccessibleElection_ReturnsMetadata()
    {
        var id = Guid.NewGuid();
        _images.GetAccessInfoAsync(id).Returns(Access(OtherUserId, ElectionId));
        _elections.CanUserAccessAsync(ElectionId, UserId).Returns(true);

        var result = await _service.GetMetadataForUserAsync(id, UserId);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetMetadataForUserAsync_WithInaccessibleElection_ReturnsNotFound()
    {
        // A closed election the caller was never invited to.
        var id = Guid.NewGuid();
        _images.GetAccessInfoAsync(id).Returns(Access(OtherUserId, ElectionId));
        _elections.CanUserAccessAsync(ElectionId, UserId).Returns(false);

        var result = await _service.GetMetadataForUserAsync(id, UserId);

        result.Success.Should().BeFalse();
        result.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task GetMetadataForUserAsync_WithClaimedImage_IgnoresUploaderAndChecksTheElection()
    {
        // The uploader loses access too: the election's rules are the single source of truth.
        var id = Guid.NewGuid();
        _images.GetAccessInfoAsync(id).Returns(Access(UserId, ElectionId));
        _elections.CanUserAccessAsync(ElectionId, UserId).Returns(false);

        var result = await _service.GetMetadataForUserAsync(id, UserId);

        result.Success.Should().BeFalse();
        result.IsNotFound.Should().BeTrue();
    }

    [Fact]
    public async Task GetMetadataForUserAsync_WhenDenied_NeverReadsTheImageBytes()
    {
        // Guessing ids must stay cheap for the database: a refused request may not pull a blob.
        var id = Guid.NewGuid();
        _images.GetAccessInfoAsync(id).Returns(Access(OtherUserId));

        await _service.GetMetadataForUserAsync(id, UserId);

        await _images.DidNotReceive().GetContentAsync(Arg.Any<Guid>());
    }

    // ── Claim validation ─────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateClaimableAsync_WithNoImages_Succeeds()
    {
        var result = await _service.ValidateClaimableAsync(Array.Empty<Guid>(), UserId, null);

        result.Success.Should().BeTrue();
        await _images.DidNotReceive().GetClaimInfoAsync(Arg.Any<IReadOnlyCollection<Guid>>());
    }

    [Fact]
    public async Task ValidateClaimableAsync_WithUnknownId_Fails()
    {
        var id = Guid.NewGuid();
        _images.GetClaimInfoAsync(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new List<ImageClaimInfo>());

        var result = await _service.ValidateClaimableAsync(new[] { id }, UserId, null);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.InvalidImageReference);
    }

    [Fact]
    public async Task ValidateClaimableAsync_WithAnotherUsersImage_Fails()
    {
        var id = Guid.NewGuid();
        _images.GetClaimInfoAsync(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new List<ImageClaimInfo> { new(id, null, OtherUserId) });

        var result = await _service.ValidateClaimableAsync(new[] { id }, UserId, null);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.InvalidImageReference);
    }

    [Fact]
    public async Task ValidateClaimableAsync_WithImageAlreadyClaimedByAnotherElection_Fails()
    {
        var id = Guid.NewGuid();
        _images.GetClaimInfoAsync(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new List<ImageClaimInfo> { new(id, Guid.NewGuid(), UserId) });

        var result = await _service.ValidateClaimableAsync(new[] { id }, UserId, ElectionId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.InvalidImageReference);
    }

    [Fact]
    public async Task ValidateClaimableAsync_WithImageAlreadyOnTheEditedElection_Succeeds()
    {
        // Re-saving an election must not reject the pictures it already owns.
        var id = Guid.NewGuid();
        _images.GetClaimInfoAsync(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new List<ImageClaimInfo> { new(id, ElectionId, UserId) });

        var result = await _service.ValidateClaimableAsync(new[] { id }, UserId, ElectionId);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateClaimableAsync_WithUnclaimedDraftOwnedByTheCaller_Succeeds()
    {
        var id = Guid.NewGuid();
        _images.GetClaimInfoAsync(Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new List<ImageClaimInfo> { new(id, null, UserId) });

        var result = await _service.ValidateClaimableAsync(new[] { id }, UserId, null);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ClaimAsync_WithDuplicateIds_PassesEachIdOnce()
    {
        var id = Guid.NewGuid();
        _images.ClaimAsync(Arg.Any<IReadOnlyCollection<Guid>>(), ElectionId).Returns(1);

        var claimed = await _service.ClaimAsync(new[] { id, id }, ElectionId);

        claimed.Should().BeTrue();
        await _images.Received(1).ClaimAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(id)),
            ElectionId);
    }

    [Fact]
    public async Task ClaimAsync_WhenARowWasTakenByAnotherElection_ReturnsFalse()
    {
        // A short row count is how the repository reports a row another election grabbed.
        _images.ClaimAsync(Arg.Any<IReadOnlyCollection<Guid>>(), ElectionId).Returns(1);

        var claimed = await _service.ClaimAsync(new[] { Guid.NewGuid(), Guid.NewGuid() }, ElectionId);

        claimed.Should().BeFalse();
    }

    // ── Draft sweep ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteUnclaimedDraftsAsync_SweepsWithACutoffOneLifetimeInThePast()
    {
        // A sign flip here would delete every fresh draft on the next restart.
        DateTime? cutoff = null;
        _images.DeleteUnclaimedOlderThanAsync(Arg.Do<DateTime>(argument => cutoff = argument))
            .Returns(0);
        var before = DateTime.UtcNow;

        await _service.DeleteUnclaimedDraftsAsync();

        cutoff.Should().NotBeNull();
        cutoff!.Value.Should().BeCloseTo(
            before.AddHours(-ValidationRules.ImageDraftLifetimeHours), TimeSpan.FromSeconds(5));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ImageAccessInfo Access(Guid uploadedBy, Guid? electionId = null)
        => new(electionId, uploadedBy, "image/webp", new string('a', 64));

    private static byte[] CreatePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.CornflowerBlue);
            // Not a flat colour, so the encoder has something real to work with.
            using var paint = new SKPaint { Color = SKColors.Goldenrod };
            canvas.DrawCircle(width / 2f, height / 2f, Math.Min(width, height) / 3f, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// A PNG declaring <paramref name="width"/> x <paramref name="height"/> in its IHDR over a
    /// few bytes of pixel data. Hand-built: no encoder will produce that mismatch.
    /// </summary>
    private static byte[] CreatePngHeader(int width, int height)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;  // bit depth
        header[9] = 2;  // colour type: truecolour
        header[10] = header[11] = header[12] = 0;

        using var pixels = new MemoryStream();
        using (var deflate = new ZLibStream(pixels, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(new byte[16]);

        using var png = new MemoryStream();
        png.Write(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A });
        WriteChunk(png, "IHDR"u8, header);
        WriteChunk(png, "IDAT"u8, pixels.ToArray());
        WriteChunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream target, ReadOnlySpan<byte> tag, byte[] payload)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, payload.Length);
        target.Write(length);
        target.Write(tag);
        target.Write(payload);

        var crcInput = new byte[tag.Length + payload.Length];
        tag.CopyTo(crcInput);
        payload.CopyTo(crcInput, tag.Length);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(crcInput));
        target.Write(crc);
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return crc ^ 0xFFFFFFFFu;
    }
}
