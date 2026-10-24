using UnityEngine;

namespace Cartheim.Plugin
{
    // The fire of a hearth built into a cart: a copy of a lit hearth's flames, light, smoke and warmth, sitting in the cart's
    // bed. It always burns. It keeps the hearth's burning and warmth areas (so the game sees a fire there), but not the part
    // that sets whoever touches it alight, nor the smoke balls that drift out of a real hearth.
    internal static class CartHearth
    {
        private const float Scale = 0.5f;
        private static readonly Vector3 Offset = new Vector3(0f, 0.45f, -0.3f);

        public static GameObject Light(Transform cart)
        {
            var fireplace = GamePieces.Prefab("hearth")?.GetComponent<Fireplace>();
            var lit = fireplace ? fireplace.m_enabledObjectHigh : null;
            if (!lit)
            {
                Debug.LogWarning("[Cartheim] The game's hearth has no lit fire to copy");
                return null;
            }
            var fire = Object.Instantiate(lit, cart, false);
            fire.name = "Cartheim hearth";
            fire.transform.localPosition = Offset;
            fire.transform.localScale = Vector3.one * Scale;
            foreach (var burn in fire.GetComponentsInChildren<Aoe>(true))
                Object.Destroy(burn);
            foreach (var smoke in fire.GetComponentsInChildren<SmokeSpawner>(true))
                Object.Destroy(smoke);
            fire.SetActive(true);
            Debug.Log("[Cartheim] Lit the hearth in a cart");
            return fire;
        }
    }
}
