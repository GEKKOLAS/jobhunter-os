using JobHunter.Infrastructure.Identity;

namespace JobHunter.UnitTests.Identity;

public class PasswordHasherTests
{
    private readonly Argon2PasswordHasher _hasher = new();

    [Fact]
    public void A_hash_verifies_its_own_password_only()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.StartsWith("$argon2id$v=19$m=65536,t=3,p=2$", hash);
        Assert.True(_hasher.Verify("correct horse battery staple", hash));
        Assert.False(_hasher.Verify("correct horse battery stapler", hash));
    }

    [Fact]
    public void The_same_password_gets_a_different_salt_each_time()
    {
        Assert.NotEqual(_hasher.Hash("same password here"), _hasher.Hash("same password here"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("$argon2i$v=19$m=65536,t=3,p=2$AAAA$AAAA")]
    [InlineData("$argon2id$v=19$m=65536,t=3$AAAA$AAAA")]
    [InlineData("$argon2id$v=19$m=65536,t=3,p=2$***$AAAA")]
    // A tampered hash asking for 4 GB of memory is refused instead of computed.
    [InlineData("$argon2id$v=19$m=4194304,t=3,p=2$AAAAAAAAAAAAAAAAAAAAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Malformed_hashes_never_verify_and_need_a_rehash(string encoded)
    {
        Assert.False(_hasher.Verify("anything at all", encoded));
        Assert.True(_hasher.NeedsRehash(encoded));
    }

    [Fact]
    public void Hashes_with_older_parameters_need_a_rehash()
    {
        var current = _hasher.Hash("correct horse battery staple");
        var older = current.Replace("m=65536,t=3,p=2", "m=19456,t=2,p=1");

        Assert.False(_hasher.NeedsRehash(current));
        Assert.True(_hasher.NeedsRehash(older));
    }
}
