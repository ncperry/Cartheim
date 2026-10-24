using UnityEngine;

namespace Cartheim.Plugin
{
    // A player-base area around a cart, like a workbench's: enemies don't spawn inside it.
    internal static class CartBaseArea
    {
        public const float Radius = 20f;

        public static GameObject Make(Transform cart)
        {
            var area = new GameObject("Cartheim base area");
            area.layer = LayerMask.NameToLayer("character_trigger");
            area.transform.SetParent(cart, false);
            var sphere = area.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = Radius;
            // EffectArea reads its collider as it wakes, so the collider goes on first.
            area.AddComponent<EffectArea>().m_type = EffectArea.Type.PlayerBase;
            return area;
        }
    }
}
