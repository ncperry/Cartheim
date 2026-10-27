using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using HarmonyLib;
using Splatform;
using UnityEngine;

namespace Cartheim.Plugin
{
    // A cart settled into a ruin (see RuinCamp): from the cart's tabs, within reach of a ruin the mod knows, a ghost of
    // everything shows over the ruin, and a click settles the cart into its place there, adding a wood door in the ruin's
    // doorway, a wood fence round the cart, and the cart's tent as a tarp over the ruin (if it has a tent; see CartBase).
    // All free, all real pieces, marked as the camp's: the hammer can't take them apart, they drop nothing when broken,
    // and they go when the cart returns to cart form. (A garden is set out from the cart's tabs, as anywhere.)
    [HarmonyPatch]
    internal static class CartRuin
    {
        private const string CampOfKey = "cartheim_camp_of";
        // Set out to look at, not to eat or take (food on a camp's table).
        private const string UntouchableKey = "cartheim_untouchable";
        // Set out to be taken (berries on the table): which of the camp's pieces it is (counted from 1); and, kept by the
        // building, those taken there, not set out again.
        private const string SetOutKey = "cartheim_set_out";
        private const string TakenKey = "cartheim_camp_taken";
        private const string DoorName = "$piece_wooddoor";
        private const string FenceName = "$piece_woodfence";

        private static readonly AccessTools.FieldRef<List<Location>> AllLocations =
            AccessTools.StaticFieldRefAccess<List<Location>>(AccessTools.Field(typeof(Location), "s_allLocations"));
        private static readonly AccessTools.FieldRef<ZDOMan, Dictionary<ZDOID, ZDO>> Objects =
            AccessTools.FieldRefAccess<ZDOMan, Dictionary<ZDOID, ZDO>>("m_objectsByID");

        // The nearest ruin the mod knows within reach of the cart.
        public static Location Near(CartBase cart) =>
            cart ? AllLocations().Where(l => l && RuinCamp.For(l.name) != null && RuinCamp.Near(Vector3.Distance(l.transform.position, cart.transform.position)))
                .OrderBy(l => Vector3.Distance(l.transform.position, cart.transform.position)).FirstOrDefault() : null;

        private static Vector3 World(Location ruin, float x, float z)
        {
            var p = ruin.transform.position + Quaternion.Euler(0f, ruin.transform.eulerAngles.y, 0f) * new Vector3(x, 0f, z);
            return new Vector3(p.x, CartGarden.Ground(p), p.z);
        }

        private static Quaternion Turn(Location ruin, float yaw) => Quaternion.Euler(0f, ruin.transform.eulerAngles.y + yaw, 0f);

        // Everything the camp adds, where it goes: the door, the fence, the campfire and its cooking station, and the sconces
        // by the door, each piece with how it's turned.
        private static IEnumerable<(CartGarden.Kind kind, Vector3 at, Quaternion facing, System.Action<GameObject> fill)> Pieces(Location ruin, RuinCamp camp)
        {
            // A recorded camp: just what was recorded, each where it stood round the building (its height too) and turned as
            // it was; the food not to be eaten marked so.
            if (camp.Recorded != null)
            {
                var turn = Turn(ruin, 0f);
                var bases = GroundBases(ruin, camp);
                var taken = Taken(ruin);
                for (var index = 0; index < camp.Recorded.Count; index++)
                {
                    var recorded = camp.Recorded[index];
                    // Set out to be taken (berries), and taken here before: not set out again.
                    if (taken.Contains(index))
                        continue;
                    var kind = CartGarden.Measure(GamePieces.Prefab(recorded.Prefab));
                    if (!kind.Prefab)
                        continue;
                    kind.Lift = 0f;
                    var untouchable = camp.Untouchable.Contains(recorded.Prefab);
                    var takeable = !untouchable && kind.Prefab.GetComponent<ItemDrop>();
                    var number = index;
                    var at = ruin.transform.position + turn * new Vector3(recorded.X, 0f, recorded.Z);
                    at.y = bases[recorded] + recorded.Y;
                    yield return (kind, at, Turn(ruin, recorded.Yaw),
                        untouchable ? (System.Action<GameObject>)(built => built.GetComponent<ZNetView>()?.GetZDO()?.Set(UntouchableKey, true))
                        : takeable ? (System.Action<GameObject>)(built => built.GetComponent<ZNetView>()?.GetZDO()?.Set(SetOutKey, number + 1))
                        : (System.Action<GameObject>)null);
                }
                yield break;
            }
            var door = CartGarden.HammerPiece(DoorName);
            var fence = CartGarden.HammerPiece(FenceName);
            if (door.Prefab)
            {
                var facing = Turn(ruin, camp.DoorYaw + door.Turn);
                var at = World(ruin, camp.DoorX, camp.DoorZ);
                // Its middle in the doorway (a door's pivot is at its hinge).
                yield return (door, at - facing * door.Middle, facing, null);
            }
            if (fence.Prefab)
                foreach (var run in camp.Fence)
                    foreach (var (x, z, yaw) in FenceRun.Pieces(run.from, run.to, fence.Length))
                        yield return (fence, World(ruin, x, z), Turn(ruin, yaw + fence.Turn), null);
            // The campfire in its corner, the cooking station over it.
            foreach (var name in new[] { Campfire, CookingStation })
            {
                var kind = CartGarden.Measure(GamePieces.Prefab(name));
                if (kind.Prefab)
                    yield return (kind, World(ruin, camp.FireX, camp.FireZ), Turn(ruin, 0f), null);
            }
            foreach (var sconce in Sconces(ruin, camp))
                yield return sconce;
            foreach (var furnishing in Furnishings(ruin, camp))
                yield return furnishing;
        }

        // The ruin's own lands' things on the game's own stands, and growing there: trophies on the walls, the shield's stand
        // above the bed (its shield hung while there's a shield about; see HangShield), bronze armour on its stand, and
        // thistles up through the floor.
        private static IEnumerable<(CartGarden.Kind kind, Vector3 at, Quaternion facing, System.Action<GameObject> fill)> Furnishings(Location ruin, RuinCamp camp)
        {
            var stand = CartGarden.Measure(GamePieces.Prefab(WallStand));
            if (stand.Prefab)
            {
                stand.Lift = 0f;
                // A stand's board looks out along its forward: turned to look into the room, its back off the wall, its
                // middle at the height given.
                var back = new Vector3(stand.Box.center.x, stand.Box.center.y, stand.Box.min.z);
                (CartGarden.Kind, Vector3, Quaternion, System.Action<GameObject>) On(float x, float z, float height, float faceX, float faceZ, System.Action<GameObject> fill)
                {
                    var facing = Turn(ruin, Mathf.Atan2(faceX, faceZ) * Mathf.Rad2Deg);
                    var at = World(ruin, x, z) + Vector3.up * height + facing * Vector3.forward * RuinCamp.WallOff - facing * back;
                    return (stand, at, facing, fill);
                }
                foreach (var h in camp.Hangings)
                    yield return On(h.X, h.Z, h.Height, h.FaceX, h.FaceZ, built => Show(built.GetComponent<ZNetView>()?.GetZDO(), h.Item));
                yield return On(camp.ShieldX, camp.ShieldZ, camp.ShieldHeight, 1f, 0f, built => built.GetComponent<ZNetView>()?.GetZDO()?.Set(ShieldStandKey, true));
            }
            var armour = CartGarden.Measure(GamePieces.Prefab(ArmourStand));
            if (armour.Prefab)
                yield return (armour, World(ruin, camp.ArmourX, camp.ArmourZ), Turn(ruin, camp.ArmourYaw), Dress);
            var thistle = CartGarden.Measure(GamePieces.Prefab(RuinCamp.Thistle));
            if (thistle.Prefab)
                for (var i = 0; i < camp.Thistles.Count; i++)
                    yield return (thistle, World(ruin, camp.Thistles[i].x, camp.Thistles[i].z), Turn(ruin, i * 137f), null);
        }

        private const string WallStand = "itemstand";
        private const string ArmourStand = "ArmorStand";
        private const string ShieldStandKey = "cartheim_shield_stand";

        // An item stand showing an item (none, for null), as the game keeps what's on it.
        private static void Show(ZDO stand, string item)
        {
            if (stand == null)
                return;
            stand.Set(ZDOVars.s_item, string.IsNullOrEmpty(item) ? 0 : item.GetStableHashCode());
            stand.Set(ZDOVars.s_variant, 0);
            stand.Set(ZDOVars.s_quality, 1);
        }

        // The armour stand in the bronze armour, each piece in the slot that takes its kind, as the game keeps them.
        private static void Dress(GameObject built)
        {
            var stand = built.GetComponent<ArmorStand>();
            var zdo = built.GetComponent<ZNetView>()?.GetZDO();
            if (!stand || zdo == null)
                return;
            for (var i = 0; i < stand.m_slots.Count; i++)
            {
                var wear = RuinCamp.Armour.FirstOrDefault(a => GamePieces.Item(a) is ItemDrop drop && stand.m_slots[i].m_supportedTypes.Contains(drop.m_itemData.m_shared.m_itemType));
                if (wear != null)
                    zdo.Set((i + "_item").GetStableHashCode(), wear.GetStableHashCode());
            }
            Debug.Log($"[Cartheim] Armour stand dressed in bronze ({stand.m_slots.Count} slots), facing {built.transform.forward}");
        }

        // The shield's stand above the bed of a cart's camp, found once, then kept: its shield hung while there's one about.
        private static readonly Dictionary<long, ZDO> ShieldStands = new Dictionary<long, ZDO>();

        public static void HangShield(CartBase cart, bool shield)
        {
            var uid = cart.Uid();
            if (uid == 0 || ZDOMan.instance == null)
                return;
            if (!ShieldStands.TryGetValue(uid, out var stand) || stand == null || !stand.IsValid())
            {
                stand = Objects(ZDOMan.instance).Values.FirstOrDefault(z => z.GetLong(CampOfKey, 0L) == uid && z.GetBool(ShieldStandKey));
                if (stand == null)
                    return;
                ShieldStands[uid] = stand;
            }
            var want = shield ? RuinCamp.Shield.GetStableHashCode() : 0;
            if (stand.GetInt(ZDOVars.s_item) != want && stand.IsOwner())
                Show(stand, shield ? RuinCamp.Shield : null);
        }

        // Where a cart's rug lies (in a ruin's room, or in its pitched tent: its middle, how it's turned, half its size), no grass
        // grows up through it. Kept by what it's in.
        private static readonly Dictionary<object, (Vector3 at, Quaternion turn, float half)> Rugs = new Dictionary<object, (Vector3, Quaternion, float)>();

        public static void Grassless(object owner, (Vector3 at, Quaternion turn, float half)? rug)
        {
            Rugs.TryGetValue(owner, out var was);
            if (rug is (Vector3, Quaternion, float) lies)
                Rugs[owner] = lies;
            else if (!Rugs.Remove(owner))
                return;
            var where = rug ?? was;
            if (ClutterSystem.instance)
                ClutterSystem.instance.ResetGrass(where.at, where.half * 1.5f);
        }

        [HarmonyPatch(typeof(ClutterSystem), nameof(ClutterSystem.GetGroundInfo)), HarmonyPostfix]
        private static void NoGrassUnderRugs(Vector3 p, ref bool __result)
        {
            if (!__result || Rugs.Count == 0)
                return;
            foreach (var rug in Rugs.Values)
            {
                var local = Quaternion.Inverse(rug.turn) * (p - rug.at);
                if (Mathf.Abs(local.x) <= rug.half && Mathf.Abs(local.z) <= rug.half)
                {
                    __result = false;
                    return;
                }
            }
        }

        // What's on a camp's stands stays there, and its thistles grow on: nothing taken off or put on, nothing picked, and
        // nothing to say so when looked at.
        [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.Interact)), HarmonyPrefix]
        private static bool NothingOffStands(ItemStand __instance, ref bool __result) => Untouched(__instance, ref __result);

        [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.UseItem)), HarmonyPrefix]
        private static bool NothingOntoStands(ItemStand __instance, ref bool __result) => Untouched(__instance, ref __result);

        // The armour stand's slots and its pose are switches on it.
        [HarmonyPatch(typeof(Switch), nameof(Switch.Interact)), HarmonyPrefix]
        private static bool ArmourStaysOn(Switch __instance, ref bool __result) => Untouched(__instance, ref __result);

        [HarmonyPatch(typeof(Switch), nameof(Switch.UseItem)), HarmonyPrefix]
        private static bool NoArmourPutOn(Switch __instance, ref bool __result) => Untouched(__instance, ref __result);

        [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact)), HarmonyPrefix]
        private static bool NotPicked(Pickable __instance, ref bool __result) => Untouched(__instance, ref __result);

        [HarmonyPatch(typeof(Pickable), nameof(Pickable.UseItem)), HarmonyPrefix]
        private static bool NothingUsedOnThistles(Pickable __instance, ref bool __result) => Untouched(__instance, ref __result);

        [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.GetHoverText)), HarmonyPrefix]
        private static bool StandSaysNothing(ItemStand __instance, ref string __result) => Unsaid(__instance, ref __result);

        [HarmonyPatch(typeof(ItemStand), nameof(ItemStand.GetHoverName)), HarmonyPrefix]
        private static bool StandUnnamed(ItemStand __instance, ref string __result) => Unsaid(__instance, ref __result);

        [HarmonyPatch(typeof(Switch), nameof(Switch.GetHoverText)), HarmonyPrefix]
        private static bool ArmourSaysNothing(Switch __instance, ref string __result) => Unsaid(__instance, ref __result);

        [HarmonyPatch(typeof(Switch), nameof(Switch.GetHoverName)), HarmonyPrefix]
        private static bool ArmourUnnamed(Switch __instance, ref string __result) => Unsaid(__instance, ref __result);

        [HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText)), HarmonyPrefix]
        private static bool ThistleSaysNothing(Pickable __instance, ref string __result) => Unsaid(__instance, ref __result);

        [HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverName)), HarmonyPrefix]
        private static bool ThistleUnnamed(Pickable __instance, ref string __result) => Unsaid(__instance, ref __result);

        private static bool Untouched(Component thing, ref bool result)
        {
            if (!IsCamp(thing))
                return true;
            result = false;
            return false;
        }

        private static bool Unsaid(Component thing, ref string result)
        {
            if (!IsCamp(thing))
                return true;
            result = "";
            return false;
        }


        // The building's own record (it lasts as long as the building does).
        private static ZDO BuildingRecord(Component building) =>
            building ? building.GetComponentInParent<LocationProxy>()?.GetComponent<ZNetView>()?.GetZDO() : null;

        private static HashSet<int> Taken(Location ruin) =>
            new HashSet<int>((BuildingRecord(ruin)?.GetString(TakenKey, "") ?? "").Split(',')
                .Select(s => int.TryParse(s, out var n) ? n : -1).Where(n => n >= 0));

        // Something set out at a camp, taken: the building remembers it, so it's not set out there again.
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Pickup)), HarmonyPrefix]
        private static void Taking(GameObject go, out (int piece, Vector3 at) __state)
        {
            var number = go ? go.GetComponent<ZNetView>()?.GetZDO()?.GetInt(SetOutKey) ?? 0 : 0;
            __state = (number - 1, go ? go.transform.position : Vector3.zero);
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Pickup)), HarmonyPostfix]
        private static void Taken(bool __result, (int piece, Vector3 at) __state)
        {
            if (!__result || __state.piece < 0)
                return;
            var building = Location.GetLocation(__state.at, checkDungeons: false);
            var record = BuildingRecord(building);
            if (record == null)
                return;
            var taken = Taken(building);
            taken.Add(__state.piece);
            record.Set(TakenKey, string.Join(",", taken.OrderBy(n => n)));
            Debug.Log($"[Cartheim] Camp piece {__state.piece} taken at {building.name}: not set out there again");
        }
        // What each recorded piece's height is measured from, here: for one standing on the ground (recorded at about the
        // building's ground), the highest ground anywhere under it, so none of it's buried on a slope; for one resting on
        // another (food on the table, a pole on a pole), the same as the ground piece it's over, so it stays on it.
        private const float OnTheGround = 0.5f;
        private const float RestingOnWithin = 2f;

        private static Dictionary<Cartheim.Core.RecordedPiece, float> GroundBases(Location ruin, RuinCamp camp)
        {
            var turn = Turn(ruin, 0f);
            var bases = new Dictionary<Cartheim.Core.RecordedPiece, float>();
            var standing = camp.Recorded.Where(p => p.Y <= OnTheGround).ToList();
            foreach (var piece in standing)
            {
                var middle = ruin.transform.position + turn * new Vector3(piece.X, 0f, piece.Z);
                var facing = Turn(ruin, piece.Yaw);
                var box = CartGarden.Measure(GamePieces.Prefab(piece.Prefab)).Box;
                var corners = new[] { new Vector3(box.min.x, 0f, box.min.z), new Vector3(box.min.x, 0f, box.max.z), new Vector3(box.max.x, 0f, box.min.z), new Vector3(box.max.x, 0f, box.max.z), Vector3.zero };
                bases[piece] = corners.Select(c => CartGarden.Ground(middle + facing * c)).Max();
            }
            foreach (var piece in camp.Recorded.Where(p => p.Y > OnTheGround))
            {
                var under = standing.OrderBy(s => (s.X - piece.X) * (s.X - piece.X) + (s.Z - piece.Z) * (s.Z - piece.Z)).FirstOrDefault();
                bases[piece] = under != null && Mathf.Sqrt((under.X - piece.X) * (under.X - piece.X) + (under.Z - piece.Z) * (under.Z - piece.Z)) <= RestingOnWithin
                    ? bases[under]
                    : CartGarden.Ground(ruin.transform.position + turn * new Vector3(piece.X, 0f, piece.Z));
            }
            return bases;
        }

        // A sconce on the outside of the front wall either side of the door. It points along its own x, its bracket at the
        // low end: turned to point straight out of the wall, its bracket against the wall's face at the height given.
        private static IEnumerable<(CartGarden.Kind kind, Vector3 at, Quaternion facing, System.Action<GameObject> fill)> Sconces(Location ruin, RuinCamp camp)
        {
            var sconce = CartGarden.Measure(GamePieces.Prefab(Sconce));
            if (!sconce.Prefab)
                yield break;
            var facing = Turn(ruin, 90f);
            var bracket = new Vector3(sconce.Box.min.x, sconce.Box.center.y, sconce.Box.center.z);
            sconce.Lift = 0f;
            foreach (var x in camp.Sconces)
                yield return (sconce, World(ruin, x, camp.SconceZ) + Vector3.up * camp.SconceHeight - facing * bracket, facing, null);
        }

        private const string Sconce = "piece_walltorch";
        private const string Campfire = "fire_pit";
        private const string CookingStation = "piece_cookingstation";

        // The ghost of it all, standing over the ruin: the cart in its place, the door, the fence and the tarp.
        public static GameObject Ghost(CartBase cart, Location ruin)
        {
            var camp = RuinCamp.For(ruin.name);
            if (camp == null || !CartGarden.Ready())
                return null;
            var root = new GameObject("Cartheim settling into a ruin");
            var cartGhost = CartRaft.Ghost("Cart");
            if (cartGhost)
            {
                var renderers = cartGhost.GetComponentsInChildren<Renderer>();
                var lift = renderers.Length > 0 ? cartGhost.transform.position.y - renderers.Min(r => r.bounds.min.y) : 0f;
                cartGhost.transform.SetPositionAndRotation(World(ruin, camp.CartX, camp.CartZ) + Vector3.up * lift, Turn(ruin, camp.CartYaw));
                cartGhost.transform.SetParent(root.transform, true);
            }
            foreach (var (kind, at, facing, _) in Pieces(ruin, camp))
            {
                var piece = CartRaft.Ghost(kind.Prefab.name);
                if (!piece)
                    continue;
                piece.transform.SetPositionAndRotation(at + Vector3.up * kind.Lift, facing);
                piece.transform.SetParent(root.transform, true);
            }
            if (cart.HasTent && camp.HasTarp)
            {
                var tarp = TentAssets.Tarp(ruin.transform.position, Quaternion.Euler(0f, ruin.transform.eulerAngles.y, 0f), TarpTuning.BoxFor(camp), ghost: true);
                if (tarp)
                    tarp.transform.SetParent(root.transform, true);
            }
            // The cart's rug laid in the room and its bed on it, if it has them.
            var rug = camp.HasRoom ? GamePieces.Rugs.FirstOrDefault(cart.Upgrades.Has) : null;
            if (rug != null)
            {
                var floor = new GameObject("rug");
                floor.transform.SetPositionAndRotation(World(ruin, camp.RugX, camp.RugZ), Turn(ruin, 0f));
                floor.transform.SetParent(root.transform, true);
                CartLooks.Rug(floor.transform, rug, new Bounds(Vector3.zero, new Vector3(camp.RugSize, 0f, camp.RugSize)));
            }
            var bed = camp.HasRoom ? GamePieces.Beds.FirstOrDefault(cart.Upgrades.Has) : null;
            var bedGhost = bed != null ? CartRaft.Ghost(bed) : null;
            if (bedGhost)
            {
                bedGhost.transform.SetPositionAndRotation(World(ruin, camp.BedX, camp.BedZ),
                    Turn(ruin, 90f - CartGarden.Measure(GamePieces.Prefab(bed)).Turn));
                bedGhost.transform.SetParent(root.transform, true);
            }
            Ghostly(root);
            return root;
        }

        // Things drawn see-through for a while (a tarp while a garden is placed under it, a camp while its cart is returned
        // to cart form), then all as they were again.
        private static readonly List<GameObject> SeenThrough = new List<GameObject>();

        public static void ShowThrough(IEnumerable<GameObject> things)
        {
            foreach (var thing in things.Where(t => t))
            {
                SeeThrough(thing, true);
                SeenThrough.Add(thing);
            }
        }

        public static void ShowAsTheyAre()
        {
            foreach (var thing in SeenThrough)
                SeeThrough(thing, false);
            SeenThrough.Clear();
        }

        // What a cart's camp has added that's loaded here: its tarp, its room's rug and bed, and its pieces.
        public static IEnumerable<GameObject> CampOf(CartBase cart)
        {
            // The cart itself too, standing where it was settled, as it's about to be put down elsewhere.
            yield return cart.gameObject;
            if (cart.Tarp)
                yield return cart.Tarp;
            foreach (var thing in cart.InRoom)
                yield return thing;
            var uid = cart.Uid();
            if (uid == 0 || ZDOMan.instance == null || !ZNetScene.instance)
                yield break;
            foreach (var zdo in Objects(ZDOMan.instance).Values.Where(z => z.GetLong(CampOfKey, 0L) == uid).ToList())
                if (ZNetScene.instance.FindInstance(zdo) is ZNetView view && view)
                    yield return view.gameObject;
        }

        private static void SeeThrough(GameObject thing, bool seeThrough)
        {
            if (!thing)
                return;
            var kept = thing.GetComponent<GhostMaterials>();
            if (seeThrough && !kept)
            {
                var originals = Solid(thing).ToDictionary(r => r, r => (r.sharedMaterials, r.shadowCastingMode));
                Ghostly(thing);
                thing.GetComponent<GhostMaterials>().Originals = originals;
            }
            else if (!seeThrough && kept)
            {
                foreach (var pair in kept.Originals.Where(p => p.Key))
                {
                    pair.Key.sharedMaterials = pair.Value.materials;
                    pair.Key.shadowCastingMode = pair.Value.shadows;
                }
                Object.Destroy(kept);
            }
        }

        // What makes a thing's shape (not its flames, smoke or sparks, which stay as they are).
        private static IEnumerable<Renderer> Solid(GameObject thing) =>
            thing.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer);

        // Pale and see-through until it's settled: every material swapped for a transparent one (keeping its texture where
        // it can), so the ghost can't be taken for the real thing.
        private static readonly Color GhostTint = new Color(0.75f, 0.9f, 1f, 0.35f);
        private static Shader _ghostShader;

        internal static void Ghostly(GameObject root)
        {
            if (!_ghostShader)
            {
                _ghostShader = new[] { "Legacy Shaders/Transparent/Diffuse", "Sprites/Default", "UI/Default" }.Select(Shader.Find).FirstOrDefault(s => s);
                Debug.Log($"[Cartheim] Ruin ghost drawn with {(_ghostShader ? _ghostShader.name : "no transparent shader found")}");
            }
            if (!_ghostShader)
                return;
            var made = new Dictionary<Material, Material>();
            foreach (var renderer in Solid(root))
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (!original)
                        continue;
                    if (!made.TryGetValue(original, out var ghost))
                    {
                        ghost = new Material(_ghostShader) { color = GhostTint, renderQueue = 3000 };
                        if (original.HasProperty("_MainTex") && original.mainTexture)
                            ghost.mainTexture = original.mainTexture;
                        made[original] = ghost;
                    }
                    materials[i] = ghost;
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            root.AddComponent<GhostMaterials>().Made = made.Values.ToList();
        }

        // The ghost's own materials, let go with it.
        private class GhostMaterials : MonoBehaviour
        {
            public List<Material> Made;
            public Dictionary<Renderer, (Material[] materials, UnityEngine.Rendering.ShadowCastingMode shadows)> Originals;

            private void OnDestroy()
            {
                foreach (var material in Made.Where(m => m))
                    Destroy(material);
            }
        }

        // The cart whose camp pieces are being built.
        private static long _campOf;

        public static void Settle(CartBase cart, Location ruin, Player player)
        {
            var camp = RuinCamp.For(ruin ? ruin.name : null);
            if (camp == null || !cart || !player)
                return;
            cart.Settle(camp, ruin.transform.position, ruin.transform.eulerAngles.y);
            _campOf = cart.Uid();
            try
            {
                foreach (var (kind, at, facing, fill) in Pieces(ruin, camp))
                {
                    var built = Object.Instantiate(kind.Prefab, at + Vector3.up * kind.Lift, facing);
                    built.GetComponent<Piece>()?.SetCreator(player.GetPlayerID(), PlatformManager.DistributionPlatform.LocalUser.PlatformUserID);
                    Mark(built);
                    fill?.Invoke(built);
                }
            }
            finally
            {
                _campOf = 0;
            }
            // Everything built there made good, for good: cosier.
            var mended = Object.FindObjectsByType<WearNTear>(FindObjectsSortMode.None)
                .Count(w => w && new Vector2(w.transform.position.x - ruin.transform.position.x, w.transform.position.z - ruin.transform.position.z).magnitude <= camp.Area + 2f && w.Repair());
            Debug.Log($"[Cartheim] Settling into the {camp.Name}: {mended} pieces repaired");
            player.Message(MessageHud.MessageType.Center, "The cart is settled into the ruin");
        }

        // A piece added by a camp: marked as the cart's, and kept as a camp's piece.
        private static void Mark(GameObject built)
        {
            var view = built.GetComponent<ZNetView>();
            if (view && view.IsValid() && _campOf != 0)
                view.GetZDO().Set(CampOfKey, _campOf);
            Keep(built);
        }

        // Food set out on a camp's table, to look at only.
        internal static bool IsUntouchable(Component thing)
        {
            var zdo = thing ? thing.GetComponentInParent<ZNetView>()?.GetZDO() : null;
            return zdo != null && zdo.GetBool(UntouchableKey);
        }

        internal static bool IsCamp(Component piece)
        {
            var zdo = piece ? piece.GetComponentInParent<ZNetView>()?.GetZDO() : null;
            return zdo != null && zdo.GetLong(CampOfKey, 0L) != 0;
        }

        // Why the camp can't be taken down yet, if it can't: something on its cooking station (loaded or not), which would go
        // with it.
        public static string Cooking(CartBase cart)
        {
            var uid = cart.Uid();
            if (uid == 0 || ZDOMan.instance == null)
                return null;
            var station = GamePieces.Prefab(CookingStation);
            var slots = station && station.GetComponent<global::CookingStation>() is global::CookingStation cooks ? cooks.m_slots.Length : 0;
            var hash = CookingStation.GetStableHashCode();
            foreach (var zdo in Objects(ZDOMan.instance).Values.Where(z => z.GetLong(CampOfKey, 0L) == uid && z.GetPrefab() == hash))
            {
                var cooking = Enumerable.Range(0, slots).Count(i => zdo.GetString("slot" + i) != "");
                if (!RuinCamp.CanTearDown(cooking, zdo.GetFloat(ZDOVars.s_fuel)))
                    return "Take what's on the cooking station off first";
            }
            return null;
        }

        // Everything a cart's camp added, gone (loaded or not), with nothing dropped.
        public static void TearDown(long cartUid)
        {
            if (cartUid == 0 || ZDOMan.instance == null)
                return;
            var me = ZDOMan.GetSessionID();
            var gone = 0;
            foreach (var zdo in Objects(ZDOMan.instance).Values.Where(z => z.GetLong(CampOfKey, 0L) == cartUid).ToList())
            {
                zdo.SetOwner(me);
                var instance = ZNetScene.instance ? ZNetScene.instance.FindInstance(zdo) : null;
                if (instance)
                    ZNetScene.instance.Destroy(instance.gameObject);
                else
                    ZDOMan.instance.DestroyZDO(zdo);
                gone++;
            }
            Debug.Log($"[Cartheim] Cart {cartUid}'s camp taken down: {gone} pieces");
        }

        // A camp's pieces can't be taken apart with the hammer, and give nothing back when broken.
        [HarmonyPatch(typeof(Piece), nameof(Piece.CanBeRemoved)), HarmonyPostfix]
        private static void NotByHammer(Piece __instance, ref bool __result)
        {
            if (__result && IsCamp(__instance))
            {
                __instance.m_canBeRemoved = false;
                __result = false;
            }
        }

        [HarmonyPatch(typeof(Piece), nameof(Piece.DropResources)), HarmonyPrefix]
        private static bool NothingBack(Piece __instance) => !IsCamp(__instance);

        // Loaded again, a camp's piece is kept so still.
        [HarmonyPatch(typeof(Piece), "Awake"), HarmonyPostfix]
        private static void StillNotByHammer(Piece __instance)
        {
            if (IsCamp(__instance))
                Keep(__instance.gameObject);
        }

        // A camp's piece can't be taken apart with the hammer, and its campfire burns without wood.
        private static void Keep(GameObject piece)
        {
            var p = piece.GetComponent<Piece>();
            if (p)
                p.m_canBeRemoved = false;
            foreach (var fire in piece.GetComponentsInChildren<Fireplace>(true))
                fire.m_infiniteFuel = true;
            // Things set out (food on the table): staying where they're put, not swept up by passing.
            foreach (var drop in piece.GetComponentsInChildren<ItemDrop>(true))
            {
                drop.m_autoPickup = false;
                if (drop.GetComponent<Rigidbody>() is Rigidbody body)
                    body.isKinematic = true;
            }
        }

        // Loaded again, a camp's thing set out is kept so still.
        [HarmonyPatch(typeof(ItemDrop), "Awake"), HarmonyPostfix]
        private static void StillSetOut(ItemDrop __instance)
        {
            if (IsCamp(__instance))
                Keep(__instance.gameObject);
        }
    }
}
