using PermaLocke.App.Services;

namespace PermaLocke.App.Tests;

public class DiscordLoginTests
{
    /// <summary>SHA-256 in base64url without padding, checked against openssl: a wrong challenge makes the server refuse every sign-in.</summary>
    [Fact]
    public void ChallengeIsBase64UrlSha256() =>
        Assert.Equal("42dRMHAfBEdHOb4v3kPo0uoA5NnHV3hWbAQ-4cF2o34",
            DiscordLogin.Challenge("dBjftJeZ4CVP-mJ92K9sM9wOVyZGyyVwKHUmXSiS6xg"));
}
