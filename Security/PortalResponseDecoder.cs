using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;

namespace SankalpAPI.Security;

/// <summary>Normalizes the external portal's encrypted data into JSON before business processing.</summary>
public static class PortalResponseDecoder
{
    public static string Decode(string responseBody, string key)
    {
        var token = JToken.Parse(responseBody);
        if (token is not JObject envelope) return responseBody;
        var data = envelope["data"];
        if (data?.Type == JTokenType.String && !string.IsNullOrWhiteSpace(data.Value<string>()))
        {
            var original = data.Value<string>()!;
            string json = original.TrimStart().StartsWith("{") || original.TrimStart().StartsWith("[")
                ? original : Aes256CbcEncrypterApp.Decrypt(original, key);
            var decoded = JToken.Parse(json);
            if (decoded is not JObject && decoded is not JArray)
                throw new CryptographicException("Portal response data must decode to an object or array.");
            envelope["data"] = decoded;
        }
        return envelope.ToString(Formatting.None);
    }
}
