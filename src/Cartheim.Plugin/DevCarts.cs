using System.Collections;
using System.Linq;
using Cartheim.Core;
using UnityEngine;

namespace Cartheim.Plugin
{
    // A cart built out for a biome, put down in front of the player in the dev world: everything the cart can be built with
    // by the end of that biome (see BiomeCart), built in as the Cart tab builds it, at no cost.
    internal static class DevCarts
    {
        private const string CartPrefab = "Cart";

        public static void Spawn(Biome biome)
        {
            var player = Player.m_localPlayer;
            var prefab = GamePieces.Prefab(CartPrefab);
            if (!player || !prefab)
                return;
            InventoryGui.instance?.Hide();
            var at = player.transform.position + player.transform.forward * 5f;
            at.y = ZoneSystem.instance ? ZoneSystem.instance.GetGroundHeight(at) + 0.3f : at.y;
            // Side on to the player, its handle to their left.
            var built = Object.Instantiate(prefab, at, Quaternion.Euler(0f, player.transform.eulerAngles.y + 90f, 0f));
            var cart = built.GetComponent<CartBase>();
            if (cart)
                cart.StartCoroutine(BuildOut(cart, biome, player));
        }

        // A frame later, once the cart has woken: each piece, best first, as often as the cart takes it (two beehives),
        // over and over until nothing more goes in (a roof over the one before it, an extension once its station's in).
        private static IEnumerator BuildOut(CartBase cart, Biome biome, Player player)
        {
            yield return null;
            var candidates = GamePieces.Sections().SelectMany(s => s.pieces).Distinct()
                .Select(p => (piece: p, materials: UpgradeCost.For(p, GamePieces.Recipe(p)).Select(c => c.Prefab).ToArray()))
                .ToList();
            var later = candidates.Where(c => BiomeCart.TierOf(c.materials) == Biome.Later)
                .Select(c => $"{c.piece} ({string.Join(", ", c.materials.Where(m => BiomeCart.TierOf(m) == Biome.Later))})");
            Debug.Log($"[Cartheim] Dev cart for the {biome}: left out as later (or of unknown materials): {string.Join("; ", later)}");
            var order = BiomeCart.For(biome, candidates).ToList();
            var added = true;
            while (added && cart)
            {
                added = false;
                foreach (var piece in order)
                {
                    var extends = GamePieces.Extends(piece);
                    if (!cart.Upgrades.CanAdd(piece, extends))
                        continue;
                    cart.Add(piece, extends);
                    if (piece == Portal.Piece)
                        CartPortal.Name(cart, player);
                    if (GamePieces.Riders.Contains(piece))
                        cart.BuildRider(piece);
                    added = true;
                }
            }
            if (cart)
                Debug.Log($"[Cartheim] Dev cart for the {biome} built with: {cart.Upgrades.Save().Replace("\n", ", ")}");
            player?.Message(MessageHud.MessageType.Center, $"A {Named(biome)} cart, built out");
        }

        public static string Named(Biome biome) =>
            biome == Biome.BlackForest ? "Black Forest" : biome.ToString();
    }
}
