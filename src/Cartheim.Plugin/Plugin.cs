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

        // How heavy the cart feels to pull, as the weight of what it would carry.
        public static ConfigEntry<float> FeltLoad;

        private readonly CartTabs _cartTabs = new CartTabs();
        private Harmony _harmony;
        private bool _loggedPieces;

        private void Awake()
        {
            Enabled = Config.Bind("Mods", "Cartheim", true,
                "Turn carts into mobile bases: build crafting stations, furniture and a chest into them. " +
                "Switched off, a destroyed cart still returns everything built into it.");
            FeltLoad = Config.Bind("Cart", "Pulls as if carrying", 600f,
                "The cart pulls as if its storage weighed this much, whatever it holds: 0 is an empty cart's light, " +
                "rattly pull; more is steadier and slower. Unlimited storage would otherwise make a full cart drag.");
            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(CartPatches));
            _harmony.PatchAll(typeof(CartCrafting));
            _harmony.PatchAll(typeof(RestPatches));
            _harmony.PatchAll(typeof(CartStorage));
            // Hot reloaded into a running world: the carts already there were woken before our patch existed.
            foreach (var vagon in FindObjectsByType<Vagon>(UnityEngine.FindObjectsSortMode.None))
                if (!vagon.GetComponent<CartBase>() && vagon.GetComponent<ZNetView>()?.GetZDO() != null)
                    vagon.gameObject.AddComponent<CartBase>();
            Logger.LogInfo("Cartheim loaded");
        }

        private void Update()
        {
            if (!_loggedPieces && ZNetScene.instance && ObjectDB.instance && Player.m_localPlayer)
            {
                _loggedPieces = true;
                GamePieces.LogStationPieces();
            }
            CartStorage.Update();
        }

        // After every Update, where other mods set up the crafting screen's buttons.
        private void LateUpdate()
        {
            _cartTabs.Update();
            CartCrafting.HideCraftAll();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            _cartTabs.Destroy();
            CartCrafting.Destroy();
            CartStorage.Close();
            foreach (var cart in CartBase.All.ToArray())
                Destroy(cart);
        }
    }
}
