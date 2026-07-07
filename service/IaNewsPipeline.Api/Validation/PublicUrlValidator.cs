using System.Net;

namespace IaNewsPipeline.Api.Validation;

public static class PublicUrlValidator
{
    public static bool TryParse(string candidate, out Uri? sourceUrl)
    {
        sourceUrl = null;

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        if (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IPAddress.TryParse(uri.Host, out var address) && !IsPublicAddress(address))
        {
            return false;
        }

        sourceUrl = uri;
        return true;
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        return address.AddressFamily switch
        {
            System.Net.Sockets.AddressFamily.InterNetwork =>
                bytes[0] != 10 &&
                !(bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) &&
                !(bytes[0] == 192 && bytes[1] == 168) &&
                !(bytes[0] == 169 && bytes[1] == 254) &&
                bytes[0] != 127,
            System.Net.Sockets.AddressFamily.InterNetworkV6 =>
                !address.IsIPv6LinkLocal &&
                !address.IsIPv6Multicast &&
                !address.IsIPv6SiteLocal,
            _ => false,
        };
    }
}
