using BoatSpotFinder.Core.Helpers;

namespace BoatSpotFinder.Tests.Helpers;

public class TokenHasherTests
{
    [Fact]
    public void Hash_IsDeterministic_ForSameInput()
    {
        var first = TokenHasher.Hash("abc");
        var second = TokenHasher.Hash("abc");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Hash_DiffersForDifferentInputs()
    {
        var a = TokenHasher.Hash("abc");
        var b = TokenHasher.Hash("abd");

        Assert.NotEqual(a, b);
    }
}
