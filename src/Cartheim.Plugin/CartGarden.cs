using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using Splatform;
using UnityEngine;

namespace Cartheim.Plugin
{
    // The Wayside Garden (see Garden): set out from the cart's tabs by anyone carrying a cultivator, placed as the raft is
    // (see CartRaft): a square of cultivated ground inside the game's own stone fences, with its wooden fence gate, all
    // real pieces built by the player. And Freyr's Blessing: a blessed cart makes every crop near it grow faster.
    internal static class CartGarden
    {
        // The game's pieces, by their names in the game's text.
        private const string FenceName = "$piece_stonefence";
        private const string GateName = "$piece_woodfencegate";
        private const string CultivatorItem = "Cultivator";
        // How far from the cart a garden may be set out.
        public const float Reach = 20f;

        private static GameObject _fence, _gate, _cultivate;
        private static float _fenceLength, _fenceTurn, _gateWidth, _gateTurn, _cultivateSpacing;

        public static bool Carries(Player player) =>
            player && player.GetInventory().GetAllItems().Any(i => i.m_dropPrefab && i.m_dropPrefab.name == CultivatorItem);

        // The fence, gate and cultivating prefabs, and how long the fence and gate are along their own length.
        private static bool Ready()
        {
            if (_fence && _gate && _cultivate)
                return true;
            var hammer = ObjectDB.instance?.GetItemPrefab("Hammer")?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces;
            var cultivator = ObjectDB.instance?.GetItemPrefab(CultivatorItem)?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_buildPieces;
            if (!hammer || !cultivator)
                return false;
            _fence = hammer.m_pieces.FirstOrDefault(p => p && p.GetComponent<Piece>()?.m_name == FenceName);
            _gate = hammer.m_pieces.FirstOrDefault(p => p && p.GetComponent<Piece>()?.m_name == GateName);
            _cultivate = cultivator.m_pieces.FirstOrDefault(p => p && p.GetComponent<TerrainOp>() && p.GetComponent<TerrainOp>().m_settings.m_paintCleared &&
                                                                p.GetComponent<TerrainOp>().m_settings.m_paintType == TerrainModifier.PaintType.Cultivate);
            if (!_fence || !_gate || !_cultivate)
            {
                Debug.LogWarning($"[Cartheim] Garden pieces not found: fence {(bool)_fence}, gate {(bool)_gate}, cultivate {(bool)_cultivate}");
                return false;
            }
            (_fenceLength, _fenceTurn) = Length(_fence);
            (_gateWidth, _gateTurn) = Length(_gate);
            var op = _cultivate.GetComponent<TerrainOp>().m_settings;
            _cultivateSpacing = Mathf.Max(0.5f, op.m_paintRadius * 1.2f);
            Debug.Log($"[Cartheim] Garden: fence {_fence.name} {_fenceLength:0.00} m long (turned {_fenceTurn}), gate {_gate.name} {_gateWidth:0.00} m (turned {_gateTurn}), " +
                      $"cultivating with {_cultivate.name}, radius {op.m_paintRadius:0.00} (level {op.m_level}, smooth {op.m_smooth}), every {_cultivateSpacing:0.00} m; " +
                      $"has a ZNetView: {(bool)_cultivate.GetComponent<ZNetView>()}");
            return true;
        }

        // A piece's length, by its meshes, and the turn that lays it along x.
        private static (float length, float turn) Length(GameObject prefab)
        {
            var bounds = new Bounds();
            var first = true;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh)
                    continue;
                var local = filter.sharedMesh.bounds;
                foreach (var corner in Corners(local))
                {
                    var p = prefab.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (first)
                        bounds = new Bounds(p, Vector3.zero);
                    else
                        bounds.Encapsulate(p);
                    first = false;
                }
            }
            return bounds.size.x >= bounds.size.z ? (bounds.size.x, 0f) : (bounds.size.z, 90f);
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (var i = 0; i < 8; i++)
                yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
        }

        private static List<Garden.Part> Layout() => Garden.Layout(_fenceLength, _gateWidth);

        public static List<ItemCount> Cost() =>
            Garden.Cost(GamePieces.Recipe(_fence.name), Layout().Count(p => !p.Gate), GamePieces.Recipe(_gate.name));

        // A ghost of the whole garden: a stripped copy of each fence and the gate, kept on the ground by Pose.
        public static GameObject Ghost()
        {
            if (!Ready())
                return null;
            var root = new GameObject("Cartheim placing the garden");
            foreach (var part in Layout())
            {
                var piece = CartRaft.Ghost(part.Gate ? _gate.name : _fence.name);
                if (!piece)
                    continue;
                piece.transform.SetParent(root.transform, false);
                piece.transform.localPosition = new Vector3(part.X, 0f, part.Z);
                piece.transform.localRotation = Quaternion.Euler(0f, part.Yaw + (part.Gate ? _gateTurn : _fenceTurn), 0f);
            }
            return root;
        }

        // Each piece of the ghost on the ground under it.
        public static void Pose(GameObject ghost, Vector3 at, Quaternion facing)
        {
            ghost.transform.SetPositionAndRotation(at, facing);
            foreach (Transform piece in ghost.transform)
            {
                var p = piece.position;
                piece.position = new Vector3(p.x, Ground(p), p.z);
            }
        }

        private static string ItemName(string prefab) =>
            GamePieces.Item(prefab) is ItemDrop item ? Localization.instance.Localize(item.m_itemData.m_shared.m_name) : prefab;

        private static float Ground(Vector3 p) => ZoneSystem.instance ? ZoneSystem.instance.GetGroundHeight(p) : p.y;

        // Why the garden can't go there, if it can't: rough ground, water, too far from the cart, or too dear.
        public static string Problem(Vector3 at, Quaternion facing, CartBase cart, Player player)
        {
            var half = Garden.Side / 2f + 0.5f;
            var heights = new List<float>();
            for (var x = -1; x <= 1; x++)
                for (var z = -1; z <= 1; z++)
                    heights.Add(Ground(at + facing * new Vector3(x * half, 0f, z * half)));
            var water = ZoneSystem.instance ? ZoneSystem.instance.m_waterLevel : 30f;
            if (!Raft.Ashore(heights.Min(), water))
                return "The garden needs dry ground";
            if (!Tent.FlatEnough(heights.ToArray(), Plugin.TentMaxRise.Value))
                return "The ground is too uneven for a garden";
            if (Vector3.Distance(at, cart.transform.position) > Reach)
                return "Too far from the cart";
            if (!player.NoCostCheat())
            {
                var cost = Cost();
                var carried = GamePieces.Carried(player, cost);
                var missing = cost.Where(c => !carried.TryGetValue(c.Prefab, out var have) || have < c.Count).ToList();
                if (missing.Count > 0)
                    return "A garden takes " + string.Join(", ", cost.Select(c => $"{c.Count} {ItemName(c.Prefab)}"));
            }
            return null;
        }

        // Paid for, the ground inside cultivated, and the fence and gate built, as the player's own pieces.
        public static void Place(Vector3 at, Quaternion facing, Player player)
        {
            if (!Ready() || !player)
                return;
            var cost = Cost();
            if (!player.NoCostCheat())
                GamePieces.Take(player, cost);
            foreach (var (x, z) in Garden.CultivatePoints(_cultivateSpacing))
            {
                var p = at + facing * new Vector3(x, 0f, z);
                Cultivate(new Vector3(p.x, Ground(p), p.z));
            }
            foreach (var part in Layout())
            {
                var prefab = part.Gate ? _gate : _fence;
                var p = at + facing * new Vector3(part.X, 0f, part.Z);
                var rotation = facing * Quaternion.Euler(0f, part.Yaw + (part.Gate ? _gateTurn : _fenceTurn), 0f);
                var built = Object.Instantiate(prefab, new Vector3(p.x, Ground(p), p.z), rotation);
                built.GetComponent<Piece>()?.SetCreator(player.GetPlayerID(), PlatformManager.DistributionPlatform.LocalUser.PlatformUserID);
            }
            _fence.GetComponent<Piece>()?.m_placeEffect.Create(at, facing);
            Debug.Log($"[Cartheim] Wayside Garden set out at {at}, for {string.Join(", ", cost)}");
            player.Message(MessageHud.MessageType.Center, "Wayside Garden set out");
        }

        // The cultivator's own work at one spot, without its sound (a whole garden at once would be a racket).
        private static void Cultivate(Vector3 at)
        {
            var wasActive = _cultivate.activeSelf;
            _cultivate.SetActive(false);
            GameObject op;
            ZNetView.m_forceDisableInit = true;
            try
            {
                op = Object.Instantiate(_cultivate, at, Quaternion.identity);
            }
            finally
            {
                ZNetView.m_forceDisableInit = false;
                _cultivate.SetActive(wasActive);
            }
            op.GetComponent<TerrainOp>().m_onPlacedEffect = new EffectList();
            op.SetActive(true);
        }

        // Freyr's Blessing, every second from a blessed cart: every crop near it this game looks after is moved on by half
        // the time since the cart last blessed it. Only crops it blessed the time before (so not across a gap while the
        // cart was away, but across a night slept through); and when two blessed carts are near, only one moves it on.
        private const string BlessedAtKey = "cartheim_freyr_blessed";

        public static long Bless(CartBase cart, long lastBlessed)
        {
            if (!ZNet.instance)
                return lastBlessed;
            var now = ZNet.instance.GetTime().Ticks;
            var blessed = 0;
            foreach (var plant in SlowUpdate.GetAllInstaces().OfType<Plant>())
            {
                var view = plant ? plant.GetComponent<ZNetView>() : null;
                if (!view || !view.IsValid() || !view.IsOwner() ||
                    !FreyrsBlessing.Near(Vector3.Distance(plant.transform.position, cart.transform.position)))
                    continue;
                var zdo = view.GetZDO();
                var last = zdo.GetLong(BlessedAtKey, 0L);
                zdo.Set(BlessedAtKey, now);
                if (lastBlessed == 0 || last != lastBlessed || now <= last)
                    continue;
                var planted = zdo.GetLong(ZDOVars.s_plantTime, 0L);
                if (planted == 0)
                    continue;
                zdo.Set(ZDOVars.s_plantTime, planted - (long)FreyrsBlessing.Bonus(now - last));
                blessed++;
            }
            return now;
        }
    }
}
