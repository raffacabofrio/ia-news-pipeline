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
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();

        return address.AddressFamily switch
        {
            System.Net.Sockets.AddressFamily.InterNetwork => IsPublicIpv4(bytes),
            System.Net.Sockets.AddressFamily.InterNetworkV6 =>
                (bytes[0] & 0xFE) != 0xFC &&
                !address.IsIPv6LinkLocal &&
                !address.IsIPv6Multicast &&
                !address.IsIPv6SiteLocal,
            _ => false,
        };
    }

    private static bool IsPublicIpv4(byte[] bytes)
    {
        return bytes[0] switch
        {
            0 => false,
            10 => false,
            100 when bytes[1] >= 64 && bytes[1] <= 127 => false,
            127 => false,
            169 when bytes[1] == 254 => false,
            172 when bytes[1] >= 16 && bytes[1] <= 31 => false,
            192 when bytes[1] == 0 && bytes[2] == 0 => false,
            192 when bytes[1] == 0 && bytes[2] == 2 => false,
            192 when bytes[1] == 168 => false,
            198 when bytes[1] is 18 or 19 => false,
            198 when bytes[1] == 51 && bytes[2] == 100 => false,
            203 when bytes[1] == 0 && bytes[2] == 113 => false,
            >= 224 => false,
            _ => true,
        };
    }
}
