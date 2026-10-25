using System.Linq;
using Cartheim.Core;
using Splatform;
using UnityEngine;

namespace Cartheim.Plugin
{
    // Pieces built into a cart that keep working as themselves (chests, beds, rugs, lights, the wisp torch) are real pieces
    // of that kind, hidden and carried along with the cart. Being real, the game itself keeps a chest's contents and its
    // "only its builder" rule, a bed's owner and spawn point, a rug's comfort and a lantern's light.
    internal static class CartRiders
    {
        public const string CartKey = "cartheim_cart";

        // The cart remembers each piece it carries under its own key.
        public static string KeyFor(string piece) => "cartheim_chest_" + piece;

        // Where the pieces ride: inside the cart's bed.
        private static readonly Vector3 Offset = Vector3.up * 0.5f;

        // Builds the piece for the local player, who becomes its creator (the only one who can open a personal chest).
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

        // Nothing of the piece shows or can be hit; it is reached only through the cart. Its light still shines and a wisp
        // torch still clears mist. Its WearNTear goes, so it never breaks for lack of support while it floats in the cart, and
        // a fire in it (sconce, jack-o-turnip) burns without fuel.
        public static void Hide(GameObject piece)
        {
            foreach (var renderer in piece.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            foreach (var collider in piece.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            var wear = piece.GetComponent<WearNTear>();
            if (wear)
                Object.Destroy(wear);
            foreach (var fire in piece.GetComponentsInChildren<Fireplace>(true))
                fire.m_infiniteFuel = true;
            // A beehive's bees still show, buzzing about the cart.
            var hive = piece.GetComponent<Beehive>();
            if (hive && hive.m_beeEffect)
                foreach (var renderer in hive.m_beeEffect.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = true;
            if (!piece.GetComponent<CartRider>())
                piece.AddComponent<CartRider>();
        }

        // Lights and mist-clearing follow the mod switch; the piece itself stays, so nothing is lost while it is off.
        public static void SetWorking(GameObject piece, bool on)
        {
            foreach (var light in piece.GetComponentsInChildren<Light>(true))
                light.enabled = on;
            foreach (var field in piece.GetComponentsInChildren<ParticleSystemForceField>(true))
                field.enabled = on;
        }

        // Every player moves the piece along with their own view of the cart, so the game's "too far" checks follow the cart;
        // its owner also moves it in the save data, so it loads wherever the cart is.
        public static void Follow(GameObject piece, Transform cart)
        {
            piece.transform.SetPositionAndRotation(cart.position + Offset, cart.rotation);
            var nview = piece.GetComponent<ZNetView>();
            if (nview && nview.IsValid() && nview.IsOwner())
                nview.GetZDO().SetPosition(piece.transform.position);
        }

        // How much honey a beehive holds, as the game keeps it.
        public static int HoneyIn(Beehive hive) => hive ? hive.GetComponent<ZNetView>()?.GetZDO()?.GetInt(ZDOVars.s_level) ?? 0 : 0;

        // Spills a container's contents and removes the piece without dropping its own materials (the cart's refund returns
        // those).
        public static void Break(GameObject piece, Vector3 at)
        {
            var nview = piece.GetComponent<ZNetView>();
            var container = piece.GetComponent<Container>();
            if (container)
            {
                var items = container.GetInventory().GetAllItems().ToList();
                Debug.Log($"[Cartheim] Cart's {piece.name} breaks; dropping {string.Join(", ", items.Select(i => $"{i.m_stack} {i.m_dropPrefab?.name}"))}");
                foreach (var item in items)
                    ItemDrop.DropItem(item, 0, at + Vector3.up * 0.5f + Random.insideUnitSphere * 0.5f, Quaternion.Euler(0f, Random.Range(0, 360), 0f));
                container.GetInventory().RemoveAll();
            }
            // Honey still in a beehive spills too.
            var hive = piece.GetComponent<Beehive>();
            var honey = HoneyIn(hive);
            if (honey > 0 && hive.m_honeyItem)
            {
                Debug.Log($"[Cartheim] Cart's {piece.name} breaks; dropping {honey} honey");
                GamePieces.Drop(new ItemCount(hive.m_honeyItem.gameObject.name, honey), at);
            }
            if (nview && nview.IsValid())
            {
                nview.ClaimOwnership();
                ZNetScene.instance.Destroy(piece);
            }
        }
    }

    // Marks a piece as one riding hidden in a cart.
    internal class CartRider : MonoBehaviour
    {
    }
}
