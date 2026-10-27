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


        // Where the pieces ride: inside the cart's bed.
        private static readonly Vector3 Offset = Vector3.up * 0.5f;

        // Marked with the cart's own id and the slot it rides in, which last across world loads (the game's object ids
        // don't); and, as before, with the cart's object id, which tells other mods it rides in a cart.
        public const string UidKey = "cartheim_cart_uid";
        public const string SlotKey = "cartheim_slot";

        // Builds the piece for the local player, who becomes its creator (the only one who can open a personal chest).
        public static ZDOID Build(string piece, Transform cart, ZDOID cartId, long cartUid, string slot)
        {
            var prefab = GamePieces.Prefab(piece);
            var go = Object.Instantiate(prefab, cart.position + Offset, cart.rotation);
            var nview = go.GetComponent<ZNetView>();
            go.GetComponent<Piece>()?.SetCreator(Player.m_localPlayer.GetPlayerID(), PlatformManager.DistributionPlatform.LocalUser.PlatformUserID);
            nview.GetZDO().Set(CartKey, cartId);
            nview.GetZDO().Set(UidKey, cartUid);
            nview.GetZDO().Set(SlotKey, slot);
            Hide(go);
            Debug.Log($"[Cartheim] Built {piece} {nview.GetZDO().m_uid} into cart {cartUid} as {slot}");
            return nview.GetZDO().m_uid;
        }

        // Fires that burn their fuel as anywhere else (the sconce, fed resin through its look on the cart); the others burn
        // without.
        private static readonly System.Collections.Generic.HashSet<string> NeedsFuel = new System.Collections.Generic.HashSet<string> { "piece_walltorch" };

        // Nothing of the piece shows or can be hit; it is reached only through the cart. Its light still shines and a wisp
        // torch still clears mist. Its WearNTear goes, so it never breaks for lack of support while it floats in the cart, and
        // a fire in it (a jack-o-turnip) burns without fuel, but for the sconce, which takes resin as anywhere.

        public static void Hide(GameObject piece)
        {
            // An item stand rides where it shows what's mounted on it, the stand itself unseen; its colliders only catch the
            // player's look, so it can be hovered and used, never bumping the cart.
            var showsItself = ShowsItself(piece);
            foreach (var renderer in piece.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;
            foreach (var collider in piece.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = showsItself;
                if (!showsItself)
                    continue;
                if (collider is MeshCollider mesh)
                    mesh.convex = true;
                collider.isTrigger = true;
            }
            var wear = piece.GetComponent<WearNTear>();
            if (wear)
                Object.Destroy(wear);
            foreach (var fire in piece.GetComponentsInChildren<Fireplace>(true))
            {
                fire.m_infiniteFuel = !NeedsFuel.Contains(Utils.GetPrefabName(piece));
                // Riding under the cart's roof and its tent, smoke trapped there: still lit (while it has fuel).
                fire.m_disableCoverCheck = true;
            }
            // A beehive's bees still show, buzzing about the cart.
            var hive = piece.GetComponent<Beehive>();
            // A light's flame (a sconce's) still burns, on the piece's model on the cart.
            if (piece.GetComponentInChildren<Light>(true))
                foreach (var flame in piece.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    flame.enabled = true;
            if (hive && hive.m_beeEffect)
                foreach (var renderer in hive.m_beeEffect.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = true;
            if (!piece.GetComponent<CartRider>())
                piece.AddComponent<CartRider>();
        }

        // A bed standing in the room of a ruin the cart has settled into: shown and solid as itself; hidden again after.
        public static void ShowInRoom(GameObject piece, bool shown)
        {
            foreach (var renderer in piece.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = shown;
            foreach (var collider in piece.GetComponentsInChildren<Collider>(true))
                collider.enabled = shown;
        }

        public static bool ShowsItself(GameObject piece) => Utils.GetPrefabName(piece) == CartUpgrades.ItemStand;

        private static readonly HarmonyLib.AccessTools.FieldRef<ItemStand, GameObject> Mounted =
            HarmonyLib.AccessTools.FieldRefAccess<ItemStand, GameObject>("m_visualItem");

        // Only what's mounted on the stand shows, and only while it has its spot on the cart.
        private static void ShowOnly(GameObject piece, bool shown)
        {
            var stand = piece.GetComponent<ItemStand>();
            var mounted = stand ? Mounted(stand) : null;
            foreach (var renderer in piece.GetComponentsInChildren<Renderer>())
            {
                var show = shown && mounted && renderer.transform.IsChildOf(mounted.transform);
                if (renderer.enabled != show)
                    renderer.enabled = show;
            }
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
        // Where it rides: on its model when the cart shows one (so a bed is slept in where it shows), else in the cart's bed.
        public static void Follow(GameObject piece, Transform cart, Transform model = null)
        {
            // An item stand shows only on its spot (not while the cart shows no looks).
            if (ShowsItself(piece))
                ShowOnly(piece, model);
            if (model)
            {
                piece.transform.SetPositionAndRotation(model.position, model.rotation);
                // A light (a sconce) is shrunk with its model, so its flame stays where the model's bracket is; an item stand to
                // its spot's size.
                if (piece.GetComponentInChildren<Light>(true) || ShowsItself(piece))
                    piece.transform.localScale = model.lossyScale;
            }
            else
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
