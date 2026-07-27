using Electronic_Election_Management_System.Services;
using FluentAssertions;

namespace Electronic_Election_Management_System.Tests.Services;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ThenVerify_WithCorrectPassword_ReturnsTrue()
    {
        const string password = "A strong password 123!";

        var hash = PasswordHasher.Hash(password);

        hash.Should().NotBe(password);
        PasswordHasher.Verify(password, hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WithIncorrectPassword_ReturnsFalse()
    {
        var hash = PasswordHasher.Hash("correct password");

        PasswordHasher.Verify("incorrect password", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_CalledTwice_UsesDifferentRandomSalts()
    {
        const string password = "same password";

        var first = PasswordHasher.Hash(password);
        var second = PasswordHasher.Hash(password);

        first.Should().NotBe(second);
        PasswordHasher.Verify(password, first).Should().BeTrue();
        PasswordHasher.Verify(password, second).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("missing.sections")]
    [InlineData("not-a-valid-hash")]
    public void Verify_WithIncorrectStoredFormat_ReturnsFalse(string storedHash)
    {
        PasswordHasher.Verify("password", storedHash).Should().BeFalse();
    }
}
