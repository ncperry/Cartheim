using System.Linq;
using UnityEngine;

namespace Cartheim.Plugin
{
    // A piece shown on a cart, hovered and used as the piece itself: E at the workbench opens the workbench (its tabs
    // still lead to the cart's other stations), E at the bed claims it or sleeps in it, E at the chest opens it. Shift+E
    // anywhere on the cart is still the cart's: crafting with all its storage.
    internal class CartPieceHover : MonoBehaviour, Hoverable, Interactable
    {
        private CartBase _cart;
        private string _piece;
        private float _lastCoal;
        private const float CoalRepeat = 0.2f;

        public void Init(CartBase cart, string piece)
        {
            _cart = cart;
            _piece = piece;
        }

        private Bed Bed => GamePieces.Beds.Contains(_piece) ? _cart.Bed : null;

        private Container Container => _cart.Containers.Find(c => c && Utils.GetPrefabName(c.gameObject) == _piece);

        private CraftingStation Station => _cart.Station(_piece);

        private Beehive Hive => GamePieces.BeehiveSlots.Contains(_piece) ? _cart.BeehiveIn(_piece) : null;

        private Fireplace Fire => _cart.FireIn(_piece);

        public string GetHoverText()
        {
            if (!_cart || !Plugin.Enabled.Value)
                return "";
            var cartKey = "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] Cart";
            var bed = Bed;
            if (bed)
                return bed.GetHoverText() + Localization.instance.Localize(cartKey);
            var hive = Hive;
            if (hive)
                return hive.GetHoverText() + Localization.instance.Localize(cartKey);
            var fire = Fire;
            if (fire)
                return fire.GetHoverText() + Localization.instance.Localize(cartKey);
            if (_piece == Cartheim.Core.SteamEngine.Engine)
                return Localization.instance.Localize(
                    $"Steam engine{(_cart.EngineBurning ? "" : " (cold)")}\n( $piece_fire_fuel {Mathf.Ceil(_cart.Coal)}/{Cartheim.Core.SteamEngine.MaxCoal} )\n" +
                    $"[<color=yellow><b>$KEY_Use</b></color>] $piece_use {GamePieces.ItemName(Cartheim.Core.SteamEngine.Coal)}{cartKey}");
            var container = Container;
            if (container)
                return container.GetHoverText() + Localization.instance.Localize(cartKey);
            var station = Station;
            if (station)
                return Localization.instance.Localize($"{station.m_name}\n[<color=yellow><b>$KEY_Use</b></color>] $piece_use{cartKey}");
            return "";
        }

        public string GetHoverName() => GamePieces.DisplayName(_piece);

        public float GetHoverOffset() => 0f;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (!_cart || !Plugin.Enabled.Value || user != Player.m_localPlayer)
                return false;
            // Holding E keeps adding coal, as at a fire; nothing else repeats.
            if (_piece == Cartheim.Core.SteamEngine.Engine && !alt)
            {
                if (hold && Time.time - _lastCoal < CoalRepeat)
                    return false;
                _lastCoal = Time.time;
                return _cart.AddCoal(Player.m_localPlayer);
            }
            // Holding E keeps feeding a fire, as anywhere.
            var fire = Fire;
            if (fire && !alt)
                return fire.Interact(user, hold, false);
            if (hold)
                return false;
            if (alt)
                return _cart.GetComponent<Vagon>().Interact(user, false, true);
            var bed = Bed;
            if (bed)
                return bed.Interact(user, false, false);
            var hive = Hive;
            if (hive)
                return hive.Interact(user, false, false);
            var container = Container;
            if (container)
                return container.Interact(user, false, false);
            var station = Station;
            if (!station)
                return false;
            // With the cart's storage beside it, as for Shift+E.
            CartCrafting.Use(station);
            CartStorage.Open(_cart);
            return true;
        }

        // Resin from the hotbar goes into the sconce, as it would at any sconce.
        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            var fire = _cart && Plugin.Enabled.Value ? Fire : null;
            return fire && fire.UseItem(user, item);
        }
    }
}
