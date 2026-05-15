using System.Security.Cryptography;
using System.Text;

namespace BoatSpotFinder.Core.Helpers;

public static class TokenHasher
{
    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
