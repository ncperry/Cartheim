using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace Cartheim.Plugin
{
    [BepInPlugin(Guid, "Cartheim", "0.1.0")]
    [BepInDependency(Jotunn.Main.ModGuid)]
    public class Plugin : BaseUnityPlugin
    {
        // nate.* puts the [Mods] switch in ValheimInventory's Mods tab.
        public const string Guid = "nate.cartheim";

        // Read whenever cart behavior would act, so switching it off takes effect at once.
        public static ConfigEntry<bool> Enabled;

        private readonly CartPanel _panel = new CartPanel();
        private Harmony _harmony;

        private void Awake()
        {
            Enabled = Config.Bind("Mods", "Cartheim", true,
                "Turn carts into mobile bases: build crafting stations, furniture and a chest into them. " +
                "Switched off, a destroyed cart still returns everything built into it.");
            CartPatches.OpenPanel = cart => _panel.Open(cart);
            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(CartPatches));
            _harmony.PatchAll(typeof(MenuBlockPatch));
            // Hot reloaded into a running world: the carts already there were woken before our patch existed.
            foreach (var vagon in FindObjectsByType<Vagon>(UnityEngine.FindObjectsSortMode.None))
                if (!vagon.GetComponent<CartBase>() && vagon.GetComponent<ZNetView>()?.GetZDO() != null)
                    vagon.gameObject.AddComponent<CartBase>();
            Logger.LogInfo("Cartheim loaded");
        }

        private void Update()
        {
            // Stays true for the frame in which Esc closes the panel, so that same press doesn't open the game menu.
            MenuBlockPatch.PanelOpen = _panel.IsOpen;
            _panel.Update();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            _panel.Destroy();
            foreach (var cart in CartBase.All.ToArray())
                Destroy(cart);
        }
    }
}
