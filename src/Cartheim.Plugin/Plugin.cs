using BepInEx;
using BepInEx.Configuration;
using Cartheim.Core;
using HarmonyLib;
using Jotunn.Utils;

namespace Cartheim.Plugin
{
    [BepInPlugin(Guid, "Cartheim", Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // Everyone in a game (server included) must run the same build, or Jotunn refuses the join and says why.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Patch)]
    public class Plugin : BaseUnityPlugin
    {
        // nate.* puts the [Mods] switch in ValheimInventory's Mods tab.
        public const string Guid = "nate.cartheim";

        // Set by release.sh.
        public const string Version = "0.1.1";

        // Read whenever cart behavior would act, so switching it off takes effect at once.
        public static ConfigEntry<bool> Enabled;

        // How heavy the cart feels to pull, as the weight of what it would carry.
        public static ConfigEntry<float> FeltLoad;

        // How uneven the ground may be where the tent is pitched.
        public static ConfigEntry<float> TentMaxRise;

        // What becomes of a cart afloat when its player dies at sea.
        public static ConfigEntry<SeaDeath> AtSeaDeath;

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
            TentMaxRise = Config.Bind("Cart", "Tent ground rise", 0.6f,
                "How much higher (in metres) the ground may be at one place than another where the tent would stand; " +
                "on rougher ground it can't be pitched.");
            AtSeaDeath = Config.Bind("Difficulty", "Cart after dying at sea", SeaDeath.ComesExceptHardcore,
                "Dying at sea respawns you on land. A cart afloat near where you died can come ashore beside you, its raft " +
                "packed away, rather than staying out on the water: ComesExceptHardcore (not on a world with the Hardcore " +
                "preset or death penalty), AlwaysComes, or StaysAtSea.");
            TarpTuning.Bind(Config);
            DevButtons.Bind(Config);
            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(CartPatches));
            _harmony.PatchAll(typeof(CartCrafting));
            _harmony.PatchAll(typeof(RestPatches));
            _harmony.PatchAll(typeof(CartStorage));
            _harmony.PatchAll(typeof(CartMaterials));
            _harmony.PatchAll(typeof(CartMaterials.CountTheCart));
            _harmony.PatchAll(typeof(CartCrates));
            _harmony.PatchAll(typeof(CartFermenter));
            _harmony.PatchAll(typeof(CartRaft));
            _harmony.PatchAll(typeof(CartPortal));
            _harmony.PatchAll(typeof(CartRescue));
            _harmony.PatchAll(typeof(CartRuin));
            _harmony.PatchAll(typeof(CampRules));
            _harmony.PatchAll(typeof(DevButtons));
            _harmony.PatchAll(typeof(DevForest));
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
            CartRaft.Update();
            CartPortal.Update();
            CartRescue.Update();
            CartGarden.Update();
            CartRuinSurvey.Update();
            DevBuildings.Update();
            CampRules.Update();
            DevTemplates.Update();
            DevButtons.Update();
            TarpTuning.Update();
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
            CartStorage.DestroyTabs();
            CartRaft.Cancel(null);
            CartPortal.Close();
            CartRescue.Unregister();
            CartPortal.Unregister();
            CartGarden.Close();
            DevBuildings.Close();
            TarpTuning.Close();
            TentAssets.Release();
            foreach (var cart in CartBase.All.ToArray())
                Destroy(cart);
        }
    }
}
