using UnityEngine;

namespace Cartheim.Plugin
{
    // What's close enough to a cart to count as the cart's own: a fire (for the cauldron, sleeping and rest) and, the other
    // way round, which carts shelter a point under their pitched tent.
    internal static class CartFire
    {
        public const float Range = 5f;

        // A hearth built into a cart near the point, or any lit fire (its burning area) within range of it.
        public static bool Near(Vector3 point) =>
            NearCart(point, cart => cart.HasLitHearth) ||
            EffectArea.IsPointInsideArea(point, EffectArea.Type.Burning, Range) != null;

        // Under a cart's pitched tent: kept out of the rain.
        public static bool UnderCartTent(Vector3 point)
        {
            foreach (var cart in CartBase.All)
                if (cart && cart.Shelters(point))
                    return true;
            return false;
        }

        // Beside a cart that has a fire near it (its own hearth, or a fire within range): a place to rest, roof or no roof.
        public static bool RestingByCart(Vector3 point) => NearCart(point, cart => Plugin.Enabled.Value && Near(cart.transform.position));

        // The nearest cart within range of the point, if any.
        public static CartBase CartNear(Vector3 point)
        {
            CartBase nearest = null;
            foreach (var cart in CartBase.All)
                if (cart && Vector3.Distance(cart.transform.position, point) <= Range &&
                    (!nearest || Vector3.Distance(cart.transform.position, point) < Vector3.Distance(nearest.transform.position, point)))
                    nearest = cart;
            return nearest;
        }

        private static bool NearCart(Vector3 point, System.Func<CartBase, bool> has)
        {
            foreach (var cart in CartBase.All)
                if (cart && has(cart) && Vector3.Distance(cart.transform.position, point) <= Range)
                    return true;
            return false;
        }
    }
}
