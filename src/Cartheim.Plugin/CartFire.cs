using UnityEngine;

namespace Cartheim.Plugin
{
    // Whether a fire burns close enough to a cart to count as the cart's own: for the cauldron now, and for sleeping and rest later.
    internal static class CartFire
    {
        public const float Range = 5f;

        // A lit fire has a burning area around it; any of it within range of the cart counts.
        public static bool Near(Vector3 cart) => EffectArea.IsPointInsideArea(cart, EffectArea.Type.Burning, Range) != null;
    }
}
