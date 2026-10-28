using HarmonyLib;
using UnityEngine;

namespace Cartheim.Plugin
{
    // A cart given rather than built: the Spawn cart button (for starting a test from scratch) and the dev world's built-out
    // carts. Destroyed, it gives back none of the cart's own wood and nails, as nobody paid them; what was built into it
    // still drops (see CartBase.DropRefund).
    [HarmonyPatch]
    internal static class CartSpawn
    {
        private const string CartPrefab = "Cart";
        private const string SpawnedKey = "cartheim_spawned";

        public static void Spawn()
        {
            if (Place(Player.m_localPlayer))
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "A cart");
        }

        // Put down in front of the player, side on, its handle to their left.
        public static CartBase Place(Player player)
        {
            var prefab = GamePieces.Prefab(CartPrefab);
            if (!player || !prefab)
                return null;
            InventoryGui.instance?.Hide();
            var at = player.transform.position + player.transform.forward * 5f;
            at.y = ZoneSystem.instance ? ZoneSystem.instance.GetGroundHeight(at) + 0.3f : at.y;
            var built = Object.Instantiate(prefab, at, Quaternion.Euler(0f, player.transform.eulerAngles.y + 90f, 0f));
            built.GetComponent<ZNetView>()?.GetZDO()?.Set(SpawnedKey, true);
            Debug.Log($"[Cartheim] Cart spawned for {player.GetPlayerName()} at {at}");
            return built.GetComponent<CartBase>();
        }

        // Kept even with the mod switched off, so switching it off can't turn a given cart into wood and nails.
        [HarmonyPatch(typeof(Piece), nameof(Piece.DropResources)), HarmonyPrefix]
        private static bool NotIfSpawned(Piece __instance)
        {
            var zdo = __instance.GetComponent<Vagon>() ? __instance.GetComponent<ZNetView>()?.GetZDO() : null;
            return zdo == null || !zdo.GetBool(SpawnedKey);
        }
    }
}
