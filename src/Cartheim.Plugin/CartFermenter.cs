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
}
