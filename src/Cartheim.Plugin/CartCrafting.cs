using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cartheim.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Cartheim.Plugin
{
    // The Cart tab of the crafting screen: the cart's upgrades as recipes at the cart's own station, so the game lists them
    // as at any crafting table (what can be built first, each with its cost) and Fetch materials fetches for them. Craft
    // builds the upgrade into the cart instead of making an item.
    internal static class CartCrafting
    {
        // The Cart tab's station, as CartStation.Piece.
        public const string UpgradeStationPiece = "cartheim_cart";

        private static readonly MethodInfo UpdateCraftingPanel =
            AccessTools.Method(typeof(InventoryGui), "UpdateCraftingPanel") ?? throw new System.MissingMethodException("InventoryGui", "UpdateCraftingPanel");
        private static readonly FieldInfo SelectedRecipeField =
            AccessTools.Field(typeof(InventoryGui), "m_selectedRecipe") ?? throw new System.MissingFieldException("InventoryGui", "m_selectedRecipe");
        private static readonly PropertyInfo RecipeProperty =
            SelectedRecipeField.FieldType.GetProperty("Recipe") ?? throw new System.MissingMemberException("RecipeDataPair", "Recipe");

        private static readonly MethodInfo CloneShared = AccessTools.Method(typeof(object), "MemberwiseClone");

        // Each upgrade's recipe, made once, and the other way round.
        private static readonly Dictionary<string, Recipe> Recipes = new Dictionary<string, Recipe>();
        private static readonly Dictionary<Recipe, string> Pieces = new Dictionary<Recipe, string>();
        // Holds the stand-in items the recipes "make" (name, icon, description), switched off so they never wake.
        private static GameObject _items;

        public static CartBase CartOf(CraftingStation station)
        {
            var marker = station ? station.GetComponent<CartStation>() : null;
            return marker ? marker.Cart : null;
        }

        public static bool IsUpgradeStation(CraftingStation station) =>
            station && station.GetComponent<CartStation>()?.Piece == UpgradeStationPiece;

        // Shows a cart station's crafting screen, as using a crafting table does.
        public static void Use(CraftingStation station)
        {
            var player = Player.m_localPlayer;
            if (!station || !player)
                return;
            player.SetCraftingStation(station);
            if (InventoryGui.IsVisible())
                Refresh();
            else
                InventoryGui.instance.Show(null, 3);
        }

        public static void Refresh() => UpdateCraftingPanel.Invoke(InventoryGui.instance, new object[] { false });

        public static void Destroy()
        {
            foreach (var recipe in Recipes.Values)
                Object.Destroy(recipe);
            Recipes.Clear();
            Pieces.Clear();
            if (_items)
                Object.Destroy(_items);
            _items = null;
        }

        // At the Cart tab, the recipes are the upgrades the cart can take now that the player has learnt; built ones and
        // ones still locked (a forge cooler before the forge, a second chest) aren't listed, as the game hides recipes
        // a station can't make.
        [HarmonyPatch(typeof(Player), nameof(Player.GetAvailableRecipes)), HarmonyPostfix]
        private static void OfferUpgrades(Player __instance, ref List<Recipe> available)
        {
            var station = __instance.GetCurrentCraftingStation();
            var cart = IsUpgradeStation(station) ? CartOf(station) : null;
            if (!cart)
                return;
            available.Clear();
            var section = 0;
            foreach (var (_, pieces) in GamePieces.Sections())
            {
                foreach (var piece in pieces)
                    if (cart.Upgrades.CanAdd(piece, GamePieces.Extends(piece)) && GamePieces.KnownBy(__instance, piece))
                    {
                        var recipe = RecipeFor(piece);
                        if (!recipe)
                            continue;
                        recipe.m_craftingStation = station;
                        // Among what can be built (and then among what can't), stations first, then fire, roof, and so on.
                        recipe.m_listSortWeight = section;
                        available.Add(recipe);
                    }
                section++;
            }
        }

        // Craft at the Cart tab builds the selected upgrade into the cart, at once.
        [HarmonyPatch(typeof(InventoryGui), "OnCraftPressed"), HarmonyPrefix]
        private static bool BuildUpgrade(InventoryGui __instance)
        {
            var recipe = (Recipe)RecipeProperty.GetValue(SelectedRecipeField.GetValue(__instance));
            if (!recipe || !Pieces.TryGetValue(recipe, out var piece))
                return true;
            var player = Player.m_localPlayer;
            var cart = CartOf(player ? player.GetCurrentCraftingStation() : null);
            if (cart)
                Build(cart, player, piece);
            Refresh();
            return false;
        }

        // The Cart tab's title shows the cart's health.
        [HarmonyPatch(typeof(InventoryGui), "UpdateRecipe"), HarmonyPostfix]
        private static void ShowCartHealth(InventoryGui __instance, Player player)
        {
            var station = player ? player.GetCurrentCraftingStation() : null;
            var cart = IsUpgradeStation(station) ? CartOf(station) : null;
            if (cart)
                __instance.m_craftingStationName.text = $"Cart  <size=70%>{cart.Health:0} / {cart.MaxHealth:0} health</size>";
        }

        private static void Build(CartBase cart, Player player, string piece)
        {
            var extends = GamePieces.Extends(piece);
            var cost = UpgradeCost.For(GamePieces.Recipe(piece));
            var state = cart.Upgrades.StateOf(piece, extends, cost, GamePieces.Carried(player, cost));
            if (state != UpgradeState.Ready && !(state == UpgradeState.CannotAfford && player.NoCostCheat()))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_missingrequirement");
                return;
            }
            if (!player.NoCostCheat())
                GamePieces.Take(player, cost);
            cart.Add(piece, extends);
            if (GamePieces.Riders.Contains(piece))
                cart.BuildRider(piece);
            var station = cart.UpgradeStation;
            if (station)
                station.m_craftItemEffects.Create(cart.transform.position, Quaternion.identity);
            player.Message(MessageHud.MessageType.Center, $"Built {GamePieces.DisplayName(piece)} into the cart");
        }

        private static Recipe RecipeFor(string piece)
        {
            if (Recipes.TryGetValue(piece, out var made) && made)
                return made;
            var recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = "Cartheim " + piece;
            recipe.m_item = StandIn(piece);
            recipe.m_amount = 1;
            recipe.m_minStationLevel = 1;
            recipe.m_resources = UpgradeCost.For(GamePieces.Recipe(piece))
                .Select(c => new Piece.Requirement { m_resItem = GamePieces.Item(c.Prefab), m_amount = c.Count, m_recover = true })
                .Where(r => r.m_resItem)
                .ToArray();
            Recipes[piece] = recipe;
            Pieces[recipe] = piece;
            return recipe;
        }

        // What the recipe shows: the piece's name, build-menu icon and description, and what it does for the cart.
        private static ItemDrop StandIn(string piece)
        {
            if (!_items)
            {
                _items = new GameObject("Cartheim upgrades");
                _items.SetActive(false);
                Object.DontDestroyOnLoad(_items);
            }
            var go = new GameObject("Cartheim " + piece);
            go.transform.SetParent(_items.transform, false);
            var item = go.AddComponent<ItemDrop>();
            var icon = GamePieces.Icon(piece);
            // A copy of an ordinary item's data, so every field the tooltip reads (attacks, damages...) is filled in.
            var shared = (ItemDrop.ItemData.SharedData)CloneShared.Invoke(GamePieces.Item("Wood").m_itemData.m_shared, null);
            shared.m_name = GamePieces.DisplayName(piece);
            shared.m_description = Localization.instance.Localize(GamePieces.Description(piece)) + EffectOf(piece);
            shared.m_icons = icon ? new[] { icon } : new Sprite[0];
            shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
            shared.m_maxStackSize = 1;
            shared.m_maxQuality = 1;
            shared.m_weight = 0f;
            shared.m_value = 0;
            shared.m_teleportable = true;
            shared.m_dlc = "";
            item.m_itemData.m_shared = shared;
            item.m_itemData.m_dropPrefab = go;
            return item;
        }

        private static string EffectOf(string piece) =>
            piece == GamePieces.WorkbenchPiece ? $"\n\n<color=orange>+{CartUpgrades.HealthPerWorkbenchLevel:0} cart health per workbench level. Needed before any other upgrade.</color>" :
            piece == "forge" ? $"\n\n<color=orange>+{CartUpgrades.ArmorPerForgeLevel:0} cart armor per forge level.</color>" :
            GamePieces.Roofs.Contains(piece) ? "\n\n<color=orange>Keeps the rain off within 5 m, and +1 comfort near the cart.</color>" :
            piece == "hearth" ? "\n\n<color=orange>A fire in the cart: lights the cauldron and lets you rest.</color>" :
            piece == SteamEngine.Engine ? $"\n\n<color=orange>Load it with up to {SteamEngine.MaxCoal} coal; it burns one as long as a hearth burns a log. While it burns it warms, lights the cauldron and lets you rest like a hearth, and takes half the cart's pull off you.</color>" :
            piece == SteamEngine.Boiler || piece == SteamEngine.Pistons ? "\n\n<color=orange>The steam engine takes a quarter more of the cart's pull off you. With both, pulling the cart doesn't slow you at all.</color>" :
            piece == CartUpgrades.Beehive ? "\n\n<color=orange>Up to two per cart. Its bees are always happy.</color>" :
            piece == GamePieces.Fermenter ? "\n\n<color=orange>Ferments under the cart's roof, wherever the cart goes.</color>" :
            "";

        // Valheim Inventory's craft counter (how many Craft makes) doesn't apply to upgrades: each is built once. Runs after
        // every Update, which is where that mod shows its counter.
        public static void HideCraftAll()
        {
            var gui = InventoryGui.instance;
            var player = Player.m_localPlayer;
            if (!gui || !player || !InventoryGui.IsVisible() || !IsUpgradeStation(player.GetCurrentCraftingStation()) || !gui.m_craftButton)
                return;
            foreach (Transform sibling in gui.m_craftButton.transform.parent)
                if (sibling.name == CraftCounter)
                    sibling.gameObject.SetActive(false);
        }

        private const string CraftCounter = "Craft count";
    }
}
