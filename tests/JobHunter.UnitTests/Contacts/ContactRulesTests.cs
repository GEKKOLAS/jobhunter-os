using JobHunter.Domain.Contacts;

namespace JobHunter.UnitTests.Contacts;

public class ContactRulesTests
{
    [Theory]
    [InlineData("https://linkedin.com/in/ada")]
    [InlineData("https://www.linkedin.com/in/ada")]
    [InlineData("https://co.linkedin.com/in/ada/")]
    [InlineData("HTTPS://WWW.LINKEDIN.COM/in/ada")]
    public void LinkedIn_addresses_pass(string url) => Assert.True(ContactRules.IsLinkedInUrl(url));

    // This value ends up in an href, so everything that merely resembles LinkedIn has to fail.
    [Theory]
    [InlineData("http://linkedin.com/in/ada")] // not https
    [InlineData("https://linkedin.com.evil.example/in/ada")]
    [InlineData("https://evil-linkedin.com/in/ada")]
    [InlineData("https://notlinkedin.com/in/ada")]
    [InlineData("https://evil.example/linkedin.com/in/ada")]
    [InlineData("https://linkedin.com@evil.example/in/ada")]
    [InlineData("https://ada:secret@linkedin.com/in/ada")] // credentials in the link
    [InlineData("https://linkedin.com:8443/in/ada")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,linkedin.com")]
    [InlineData("ftp://linkedin.com/in/ada")]
    [InlineData("linkedin.com/in/ada")] // not absolute: normalize first
    [InlineData("not a url")]
    public void Everything_else_fails(string url) => Assert.False(ContactRules.IsLinkedInUrl(url));

    [Theory]
    [InlineData("  https://linkedin.com/in/ada ", "https://linkedin.com/in/ada")]
    [InlineData("linkedin.com/in/ada", "https://linkedin.com/in/ada")]
    [InlineData("www.linkedin.com/in/ada", "https://www.linkedin.com/in/ada")]
    [InlineData("http://linkedin.com/in/ada", "http://linkedin.com/in/ada")] // a scheme it has stays, and then fails
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void A_pasted_link_is_tidied_only_as_far_as_adding_the_scheme(string? value, string? normalized) =>
        Assert.Equal(normalized, ContactRules.NormalizeLinkedInUrl(value));

    [Fact]
    public void A_link_pasted_without_a_scheme_passes_once_normalized()
    {
        Assert.True(ContactRules.IsLinkedInUrl(ContactRules.NormalizeLinkedInUrl("linkedin.com/in/ada")!));
        Assert.False(ContactRules.IsLinkedInUrl(ContactRules.NormalizeLinkedInUrl("evil.example/in/ada")!));
    }
}
