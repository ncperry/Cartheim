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

        public void Init(CartBase cart, string piece)
        {
            _cart = cart;
            _piece = piece;
        }

        private Bed Bed => GamePieces.Beds.Contains(_piece) ? _cart.Bed : null;

        private Container Container => _cart.Containers.Find(c => c && Utils.GetPrefabName(c.gameObject) == _piece);

        private CraftingStation Station => _cart.Station(_piece);

        public string GetHoverText()
        {
            if (!_cart || !Plugin.Enabled.Value)
                return "";
            var cartKey = "\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] Cart";
            var bed = Bed;
            if (bed)
                return bed.GetHoverText() + Localization.instance.Localize(cartKey);
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
            if (hold || !_cart || !Plugin.Enabled.Value || user != Player.m_localPlayer)
                return false;
            if (alt)
                return _cart.GetComponent<Vagon>().Interact(user, false, true);
            var bed = Bed;
            if (bed)
                return bed.Interact(user, false, false);
            var container = Container;
            if (container)
                return container.Interact(user, false, false);
            var station = Station;
            if (!station)
                return false;
            CartCrafting.Use(station);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
    }
}
