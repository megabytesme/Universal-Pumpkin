using Windows.Foundation.Metadata;

namespace Universal_Pumpkin.Services
{
    public static class OSHelper
    {
        public static bool IsWindows11 => ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 13);
    }
}