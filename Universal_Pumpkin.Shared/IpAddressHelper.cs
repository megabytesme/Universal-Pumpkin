using System.Linq;
using Windows.Networking;
using Windows.Networking.Connectivity;

namespace Universal_Pumpkin
{
    public static class IpAddressHelper
    {
        public static string GetLocalIpAddress()
        {
            try
            {
                var icp = NetworkInformation.GetInternetConnectionProfile();

                if (icp?.NetworkAdapter != null)
                {
                    var hostname = NetworkInformation.GetHostNames()
                        .FirstOrDefault(hn =>
                            hn.Type == HostNameType.Ipv4 &&
                            hn.IPInformation?.NetworkAdapter != null &&
                            hn.IPInformation.NetworkAdapter.NetworkAdapterId == icp.NetworkAdapter.NetworkAdapterId);

                    if (hostname != null)
                        return hostname.CanonicalName;
                }

                var anyIp = NetworkInformation.GetHostNames()
                    .FirstOrDefault(hn =>
                        hn.Type == HostNameType.Ipv4 &&
                        hn.IPInformation != null &&
                        hn.CanonicalName != "127.0.0.1");

                return anyIp?.CanonicalName ?? "127.0.0.1";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}