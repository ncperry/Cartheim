using UnityEngine;

namespace Cartheim.Plugin
{
    // The wisp that floats around a cart with a wisp torch built in: the Wisplight's own ball, so it glows and clears the
    // Mistlands mist as the Wisplight does. Each player makes their own (mist is drawn per player), kept off the network.
    internal static class CartWisp
    {
        private const float Radius = 2f;
        private const float Height = 2.2f;
        private const float Speed = 0.6f;

        public static GameObject Make(Transform cart)
        {
            var demister = ObjectDB.instance?.GetItemPrefab("Demister")?.GetComponent<ItemDrop>();
            var effect = demister ? demister.m_itemData.m_shared.m_equipStatusEffect as SE_Demister : null;
            if (!effect || !effect.m_ballPrefab)
            {
                Debug.LogWarning("[Cartheim] No Wisplight ball to copy");
                return null;
            }
            GameObject wisp;
            ZNetView.m_forceDisableInit = true;
            try
            {
                wisp = Object.Instantiate(effect.m_ballPrefab, cart.position + Vector3.up * Height, Quaternion.identity);
            }
            finally
            {
                ZNetView.m_forceDisableInit = false;
            }
            foreach (var sync in wisp.GetComponentsInChildren<ZSyncTransform>(true))
                Object.Destroy(sync);
            foreach (var nview in wisp.GetComponentsInChildren<ZNetView>(true))
                Object.Destroy(nview);
            wisp.name = "Cartheim wisp";
            Debug.Log("[Cartheim] A wisp now floats around a cart");
            return wisp;
        }

        // Circles the cart a little above it, bobbing, the way the Wisplight hovers by its wearer.
        public static void Float(GameObject wisp, Transform cart)
        {
            var t = Time.time * Speed;
            var target = cart.position + new Vector3(Mathf.Cos(t) * Radius, Height + Mathf.Sin(t * 2.3f) * 0.3f, Mathf.Sin(t) * Radius);
            wisp.transform.position = Vector3.Lerp(wisp.transform.position, target, Time.deltaTime * 3f);
        }
    }
}
