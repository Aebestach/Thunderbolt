using System;

namespace Thunderbolt
{
    /// <summary>
    /// Prefers the optional EVE provider when present; otherwise atmospheric ASL-density storms.
    /// </summary>
    public static class StormSampler
    {
        public delegate bool TrySampleDelegate(Vessel vessel, out StormSample sample);

        private static Func<bool> eveAvailable;
        private static TrySampleDelegate eveTrySample;

        public static bool HasEveProvider => eveAvailable != null && eveAvailable();

        public static string ActiveModeLabel => HasEveProvider ? "EVE clouds" : "atmospheric";

        public static void RegisterEveProvider(Func<bool> available, TrySampleDelegate trySample)
        {
            eveAvailable = available;
            eveTrySample = trySample;
            if (available != null && available())
            {
                ThunderboltSettings.Log("EVE storm provider registered.");
            }
        }

        public static bool TrySampleStormAboveVessel(Vessel vessel, out StormSample sample)
        {
            if (HasEveProvider && eveTrySample != null)
            {
                return eveTrySample(vessel, out sample);
            }

            return AtmosphericStormBridge.TrySampleStormAboveVessel(vessel, out sample);
        }
    }
}
