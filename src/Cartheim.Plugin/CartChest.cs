using System.Linq;
using Splatform;
using UnityEngine;

namespace Cartheim.Plugin
{
    // The chest built into a cart is a real chest of that kind, hidden and carried along with the cart. Being real, the game
    // itself keeps its size, its contents, one player at a time, and a personal chest's "only its builder" rule.
    internal static class CartChest
    {
        public const string ChestKey = "cartheim_chest";
        public const string CartKey = "cartheim_cart";

        // Where the chest rides: inside the cart's bed.
        private static readonly Vector3 Offset = Vector3.up * 0.5f;

        // Builds the chest for the local player, who becomes its creator (the only one who can open a personal chest).
        public static ZDOID Build(string piece, Transform cart, ZDOID cartId)
        {
            var prefab = GamePieces.Prefab(piece);
            var go = Object.Instantiate(prefab, cart.position + Offset, cart.rotation);
            var nview = go.GetComponent<ZNetView>();
            go.GetComponent<Piece>()?.SetCreator(Player.m_localPlayer.GetPlayerID(), PlatformManager.DistributionPlatform.LocalUser.PlatformUserID);
            nview.GetZDO().Set(CartKey, cartId);
            Hide(go);
            Debug.Log($"[Cartheim] Built {piece} {nview.GetZDO().m_uid} into cart {cartId}");
            return nview.GetZDO().m_uid;
        }

        // Nothing of the chest shows or can be hit; it is reached only through the cart. Its WearNTear goes too, so the chest
        // never breaks for lack of support while it floats in the cart.
        public static void Hide(GameObject chest)
        {
            foreach (var renderer in chest.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            foreach (var collider in chest.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            var wear = chest.GetComponent<WearNTear>();
            if (wear)
                Object.Destroy(wear);
        }

        // Every player moves the chest along with their own view of the cart, so the game's "too far from the chest" check
        // follows the cart; its owner also moves it in the save data, so it loads wherever the cart is.
        public static void Follow(Container chest, Transform cart)
        {
            chest.transform.SetPositionAndRotation(cart.position + Offset, cart.rotation);
            var nview = chest.GetComponent<ZNetView>();
            if (nview && nview.IsValid() && nview.IsOwner())
                nview.GetZDO().SetPosition(chest.transform.position);
        }

        // Spills the chest's contents and removes it without dropping its own materials (the cart's refund returns those).
        public static void Break(Container chest, Vector3 at)
        {
            var nview = chest.GetComponent<ZNetView>();
            var items = chest.GetInventory().GetAllItems().ToList();
            Debug.Log($"[Cartheim] Cart chest {nview?.GetZDO()?.m_uid} breaks; dropping {string.Join(", ", items.Select(i => $"{i.m_stack} {i.m_dropPrefab?.name}"))}");
            foreach (var item in items)
                ItemDrop.DropItem(item, 0, at + Vector3.up * 0.5f + Random.insideUnitSphere * 0.5f, Quaternion.Euler(0f, Random.Range(0, 360), 0f));
            chest.GetInventory().RemoveAll();
            if (nview && nview.IsValid())
            {
                nview.ClaimOwnership();
                ZNetScene.instance.Destroy(chest.gameObject);
            }
        }
    }
}
