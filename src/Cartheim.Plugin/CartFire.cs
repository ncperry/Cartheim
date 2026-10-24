using UnityEngine;

namespace Cartheim.Plugin
{
    // What's close enough to a cart to count as the cart's own: a fire (for the cauldron, sleeping and rest) and, the other
    // way round, which carts shelter a point under their roof.
    internal static class CartFire
    {
        public const float Range = 5f;

        // A hearth built into a cart near the point, or any lit fire (its burning area) within range of it.
        public static bool Near(Vector3 point) =>
            NearCart(point, cart => cart.HasLitHearth) ||
            EffectArea.IsPointInsideArea(point, EffectArea.Type.Burning, Range) != null;

        // Within range of a cart with a roof: kept out of the rain.
        public static bool UnderCartRoof(Vector3 point) => NearCart(point, cart => cart.HasRoof);

        private static bool NearCart(Vector3 point, System.Func<CartBase, bool> has)
        {
            foreach (var cart in CartBase.All)
                if (cart && has(cart) && Vector3.Distance(cart.transform.position, point) <= Range)
                    return true;
            return false;
        }
    }
}
