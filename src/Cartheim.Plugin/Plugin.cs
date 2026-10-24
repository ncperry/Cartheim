using BepInEx;
using BepInEx.Configuration;

namespace Cartheim.Plugin
{
    [BepInPlugin(Guid, "Cartheim", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        // nate.* puts the [Mods] switch in ValheimInventory's Mods tab.
        public const string Guid = "nate.cartheim";

        // Read whenever cart behavior would act, so switching it off takes effect at once.
        public static ConfigEntry<bool> Enabled;

        private void Awake()
        {
            Enabled = Config.Bind("Mods", "Cartheim", true,
                "Turn carts into mobile bases: build crafting stations, furniture and a chest into them. " +
                "Switched off, a destroyed cart still returns everything built into it.");
            Logger.LogInfo("Cartheim loaded");
        }
    }
}
