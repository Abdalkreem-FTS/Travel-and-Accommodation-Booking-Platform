using HotelBooking.Domain.Common;

namespace HotelBooking.Domain.UnitTests.Common;

public sealed class EmailTests
{
    [Fact]
    public void Create_TrimsAndLowerCasesSoOneInboxIsOneAddress()
    {
        Email.Create("  Abdalkreem.Bzoor@Example.COM ").Value.Value.ShouldBe("abdalkreem.bzoor@example.com");
    }

    [Theory]
    [InlineData("a@b")]
    [InlineData("@example.com")]
    [InlineData("a@@example.com")]
    [InlineData("a b@example.com")]
    [InlineData("a@example.")]
    public void Create_ForAMalformedAddress_IsRefused(string value)
    {
        Email.Create(value).TopError.ShouldBe(EmailErrors.Invalid);
    }
}
