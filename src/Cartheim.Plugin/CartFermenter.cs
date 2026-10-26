using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // A fermenter riding in a cart is a full-size one, hidden where its growler-sized look stands. The effects it makes as
    // mead base goes in and as it's tapped are shrunk to the growler, instead of foaming over the whole cart.
    internal static class CartFermenter
    {
        private static float _shrink = 1f;

        [HarmonyPatch(typeof(Fermenter), "RPC_AddItem"), HarmonyPrefix]
        private static void ShrinkAdding(Fermenter __instance) => _shrink = ShrinkOf(__instance);

        [HarmonyPatch(typeof(Fermenter), "RPC_Tap"), HarmonyPrefix]
        private static void ShrinkTapping(Fermenter __instance) => _shrink = ShrinkOf(__instance);

        [HarmonyPatch(typeof(Fermenter), "DelayedTap"), HarmonyPrefix]
        private static void ShrinkPouring(Fermenter __instance) => _shrink = ShrinkOf(__instance);

        [HarmonyPatch(typeof(Fermenter), "RPC_AddItem"), HarmonyPostfix]
        private static void DoneAdding() => _shrink = 1f;

        [HarmonyPatch(typeof(Fermenter), "RPC_Tap"), HarmonyPostfix]
        private static void DoneTapping() => _shrink = 1f;

        [HarmonyPatch(typeof(Fermenter), "DelayedTap"), HarmonyPostfix]
        private static void DonePouring() => _shrink = 1f;

        private static float ShrinkOf(Fermenter fermenter)
        {
            var cart = CartBase.All.FirstOrDefault(c => c && c.Fermenter == fermenter);
            return cart ? cart.FermenterShrink : 1f;
        }

        // Particles follow the shrunk effect only when scaled with it, not by their own scale alone.
        [HarmonyPatch(typeof(EffectList), nameof(EffectList.Create)), HarmonyPostfix]
        private static void Shrink(GameObject[] __result)
        {
            if (Mathf.Approximately(_shrink, 1f) || __result == null)
                return;
            foreach (var effect in __result.Where(e => e))
            {
                effect.transform.localScale *= _shrink;
                foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = particles.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
            }
            Debug.Log($"[Cartheim] The cart's fermenter made {string.Join(", ", __result.Where(e => e).Select(e => e.name))}, shrunk to {_shrink:0.00}");
        }
    }

    // On a fermenter's look on the cart: copies of its lid, bubbles and full look, each shown as the real (hidden) one is,
    // so it shows from outside what's in it as any fermenter does.
    internal class FermenterLook : MonoBehaviour
    {
        private GameObject _fermenting;
        private GameObject _ready;
        private GameObject _top;

        public void Copy(Fermenter fermenter, Transform holder)
        {
            _fermenting = CopyOf(fermenter.m_fermentingObject, fermenter.transform, holder);
            _ready = CopyOf(fermenter.m_readyObject, fermenter.transform, holder);
            _top = CopyOf(fermenter.m_topObject, fermenter.transform, holder);
        }

        // Placed in the look as in the fermenter; hidden until matched; nothing to bump into; its particles shrunk with it.
        private static GameObject CopyOf(GameObject part, Transform fermenter, Transform holder)
        {
            if (!part)
                return null;
            var copy = Instantiate(part, holder);
            copy.name = part.name;
            var place = fermenter.worldToLocalMatrix * part.transform.localToWorldMatrix;
            copy.transform.localPosition = place.GetColumn(3);
            copy.transform.localRotation = place.rotation;
            copy.transform.localScale = place.lossyScale;
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
                Destroy(collider);
            foreach (var particles in copy.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
            copy.SetActive(false);
            return copy;
        }

        public void Match(Fermenter fermenter)
        {
            Show(_fermenting, fermenter.m_fermentingObject);
            Show(_ready, fermenter.m_readyObject);
            Show(_top, fermenter.m_topObject);
        }

        private static void Show(GameObject copy, GameObject real)
        {
            if (copy && real && copy.activeSelf != real.activeSelf)
                copy.SetActive(real.activeSelf);
        }

        // How far to turn the fermenter (degrees about the vertical) so its spigot, where tapped mead comes out, faces the
        // back of the cart (-z).
        public static float SpigotBack(Fermenter fermenter)
        {
            if (!fermenter || !fermenter.m_outputPoint)
                return 0f;
            var spigot = fermenter.transform.InverseTransformPoint(fermenter.m_outputPoint.position);
            return 180f - Mathf.Atan2(spigot.x, spigot.z) * Mathf.Rad2Deg;
        }
    }
}
