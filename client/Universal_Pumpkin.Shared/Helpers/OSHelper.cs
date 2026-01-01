using Windows.Foundation.Metadata;
using Windows.Storage;

namespace Universal_Pumpkin.Services
{
    public static class OSHelper
    {
        public static bool IsWindows11 =>
            ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 13);

        public static bool IsWindows10_1709OrGreater =>
            ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5);
    }
}