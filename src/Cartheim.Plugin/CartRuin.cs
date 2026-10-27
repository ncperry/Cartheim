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
        private static readonly System.Reflection.MethodInfo Unhitch = AccessTools.Method(typeof(Vagon), "Detach");
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
                var placement = Placement(ruin, camp);
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
                    // Left out where nothing near is clear of this building's things.
                    if (!placement.TryGetValue(recorded, out var at))
                        continue;
                    if (recorded.Wears.Count > 0)
                    {
                        var wears = recorded.Wears;
                        yield return (kind, at, Turn(ruin, recorded.Yaw), built => Wear(built, wears));
                        continue;
                    }
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

        // A moment after the garden ground's painted: whether it shows where it was recorded (or, mirrored or turned
        // round, somewhere else), for the log.
        private static System.Collections.IEnumerator GardenCheck(Location ruin, RuinCamp camp)
        {
            yield return new WaitForSeconds(1.5f);
            if (!ruin)
                yield break;
            var turn = Turn(ruin, 0f);
            int Count(float sx, float sz) => camp.Cultivated.Count(c =>
            {
                var at = ruin.transform.position + turn * new Vector3(sx * c.x, 0f, sz * c.z);
                var ground = Heightmap.FindHeightmap(at);
                return ground && ground.IsCultivated(at);
            });
            var first = ruin.transform.position + turn * new Vector3(camp.Cultivated[0].x, 0f, camp.Cultivated[0].z);
            Debug.Log($"[Cartheim] The {camp.Name}'s garden ground shows at {Count(1f, 1f)} of {camp.Cultivated.Count} recorded spots " +
                      $"(mirrored across x {Count(-1f, 1f)}, across z {Count(1f, -1f)}, turned round {Count(-1f, -1f)}); the building at " +
                      $"{ruin.transform.position} turned {ruin.transform.eulerAngles.y:0.0}, the first spot at {first}");
        }

        // A recorded armour stand in what it wore, slot by slot (a trophy on its head, say), as the game keeps it.
        private static void Wear(GameObject built, List<(int slot, string item)> wears)
        {
            var zdo = built.GetComponent<ZNetView>()?.GetZDO();
            if (zdo == null)
                return;
            foreach (var (slot, item) in wears)
                zdo.Set((slot + "_item").GetStableHashCode(), item.GetStableHashCode());
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

        // A bed of ours set where one of the building's own stands: centred on it, its length the way the other's runs.
        internal static (Vector3 at, Quaternion facing) BedInPlaceOf(Vector3 ruinAt, Quaternion turn, Cartheim.Core.RecordedPiece spot, string ours)
        {
            var theirs = CartGarden.Measure(GamePieces.Prefab(spot.Prefab));
            var mine = CartGarden.Measure(GamePieces.Prefab(ours));
            var theirTurn = turn * Quaternion.Euler(0f, spot.Yaw, 0f);
            var middle = ruinAt + turn * new Vector3(spot.X, spot.Y, spot.Z) + theirTurn * new Vector3(theirs.Box.center.x, 0f, theirs.Box.center.z);
            var facing = theirTurn * Quaternion.Euler(0f, theirs.Turn - mine.Turn, 0f);
            return (middle - facing * new Vector3(mine.Box.center.x, 0f, mine.Box.center.z), facing);
        }

        // Which of the building's beds the cart's bed takes the place of (the first a bed fits at, clear of the building's
        // furniture and props: a chest beside it, say), and which is the spare (the other); none for the cart's, if a bed fits
        // at neither (it stays in the cart).
        internal static (Cartheim.Core.RecordedPiece cart, Cartheim.Core.RecordedPiece spare) Beds(Vector3 ruinAt, Quaternion turn, RuinCamp camp, string ours)
        {
            var spots = new[] { camp.BedSpot, camp.SpareBedSpot }.Where(s => s != null).ToList();
            var cart = spots.FirstOrDefault(s => Fits(ruinAt, turn, s, ours));
            return (cart, spots.FirstOrDefault(s => s != cart));
        }

        // Whether a bed of ours, in place of one of the building's, would stand clear of the building's furniture and props
        // (its walls and sloping roof it may touch). A spare bed a camp made there, unclaimed, isn't in the way: it makes way.
        internal static bool Fits(Vector3 ruinAt, Quaternion turn, Cartheim.Core.RecordedPiece spot, string ours)
        {
            var (at, facing) = BedInPlaceOf(ruinAt, turn, spot, ours);
            var inTheWay = Blockers(at, facing, CartGarden.Measure(GamePieces.Prefab(ours)).Box, spot.Prefab, structure: false, alsoIgnore: MakesWay);
            if (inTheWay.Count > 0)
                Debug.Log($"[Cartheim] A bed in place of the {spot.Prefab} at {spot.X:0.00},{spot.Z:0.00} would meet: {string.Join(", ", inTheWay)}");
            return inTheWay.Count == 0;
        }

        // A spare bed a camp made (given, or no player's), that no one's claimed.
        private static bool MakesWay(ZNetView view) =>
            view.GetComponent<Bed>() && view.GetZDO()?.GetLong(ZDOVars.s_owner, 0L) == 0L &&
            (IsGiven(view) || view.GetComponent<Piece>() is Piece piece && piece.GetCreator() == 0L);

        // Unclaimed spare beds where the cart's bed is to stand, cleared away for it.
        internal static void MakeWayForBed(Vector3 at, Quaternion facing, string ours)
        {
            var box = CartGarden.Measure(GamePieces.Prefab(ours)).Box;
            foreach (var view in Physics.OverlapBox(at + facing * box.center, box.extents, facing, ~0, QueryTriggerInteraction.Ignore)
                         .Select(c => c.GetComponentInParent<ZNetView>()).Where(v => v && v.IsValid() && !v.GetComponent<CartRider>() && MakesWay(v)).Distinct().ToList())
            {
                view.ClaimOwnership();
                ZNetScene.instance.Destroy(view.gameObject);
                Debug.Log("[Cartheim] A spare bed made way for the cart's");
            }
        }

        // The building's spare bed made a real one, for good: given to it (it stays when the camp's packed up, and gives nothing
        // back if taken down), anyone's to claim. Not again where it's been made one already, nor where it wouldn't fit.
        private const string SpareBed = "bed";

        private static void MakeSpareBed(Location ruin, RuinCamp camp)
        {
            var turn = Turn(ruin, 0f);
            var spare = Beds(ruin.transform.position, turn, camp, GamePieces.Beds.FirstOrDefault() ?? SpareBed).spare;
            if (spare == null)
                return;
            var spot = ruin.transform.position + turn * new Vector3(spare.X, spare.Y, spare.Z);
            var near = Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None)
                .Where(v => v && v.IsValid() && Vector3.Distance(v.transform.position, spot) < 1.5f).ToList();
            if (near.Any(v => v.GetComponent<Bed>() && !IsCamp(v) && !v.GetComponent<CartRider>()))
                return;
            if (!Fits(ruin.transform.position, turn, spare, SpareBed))
            {
                Debug.Log($"[Cartheim] The {camp.Name}'s spare {spare.Prefab} left as it is: a bed wouldn't fit there");
                return;
            }
            foreach (var old in near.Where(v => Utils.GetPrefabName(v.gameObject) == spare.Prefab && Vector3.Distance(v.transform.position, spot) < 0.6f))
            {
                old.ClaimOwnership();
                ZNetScene.instance.Destroy(old.gameObject);
            }
            var (at, facing) = BedInPlaceOf(ruin.transform.position, turn, spare, SpareBed);
            Give(SpareBed, at, facing);
            Debug.Log($"[Cartheim] The {camp.Name}'s spare {spare.Prefab} made a real bed, for good");
        }

        // A recorded camp's building made whole overhead and at the door, for good: every roof piece and door its template
        // has that isn't there now, given to it (it stays when the camp's packed up, and gives nothing back if taken down).
        private static readonly string[] Overhead = { "roof", "door" };

        private static void MendRoofAndDoor(Location ruin, RuinCamp camp)
        {
            var location = ZoneSystem.instance.m_locations.FirstOrDefault(l => l != null && l.m_prefab.IsValid && l.m_prefab.Name == camp.Name);
            if (location == null)
                return;
            location.m_prefab.Load();
            var template = location.m_prefab.Asset.transform;
            var turn = ruin.transform.rotation;
            var here = Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None)
                .Where(v => v && v.IsValid() && Vector3.Distance(v.transform.position, ruin.transform.position) <= camp.Area + 5f).ToList();
            var given = new List<string>();
            foreach (var part in template.GetComponentsInChildren<ZNetView>(true))
            {
                var name = Utils.GetPrefabName(part.gameObject);
                if (!Overhead.Any(o => name.IndexOf(o, System.StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;
                var at = ruin.transform.position + turn * template.InverseTransformPoint(part.transform.position);
                if (here.Any(v => Utils.GetPrefabName(v.gameObject) == name && Vector3.Distance(v.transform.position, at) < 0.3f))
                    continue;
                if (Give(name, at, turn * (Quaternion.Inverse(template.rotation) * part.transform.rotation)))
                    given.Add(name);
            }
            Debug.Log($"[Cartheim] The {camp.Name} made whole overhead and at the door: {(given.Count == 0 ? "nothing missing" : string.Join(", ", given))}");
        }
        // Where each recorded piece goes at this building, made to fit however this one turned out (its seed can give it other
        // furniture, other huts): a piece standing on the ground goes where it was recorded unless something of the
        // building's is in the way there, then to the nearest clear spot within reach (turned as recorded), else it's left
        // out; one resting on another (food on the table, a pole on a pole) goes with the one it rests on. The camp's own
        // structure (a shelter joined to the building) goes where it was recorded, as it's meant to meet the building. Each
        // stands on the highest ground under it (none of it buried on a slope); what rests on it, as high above that.
        private const float OnTheGround = 0.5f;
        private const float RestingOnWithin = 2f;
        private const float NudgeStep = 0.25f;
        private const float NudgeReach = 1.5f;

        private static Dictionary<Cartheim.Core.RecordedPiece, Vector3> Placement(Location ruin, RuinCamp camp)
        {
            var turn = Turn(ruin, 0f);
            var placed = new Dictionary<Cartheim.Core.RecordedPiece, Vector3>();
            var moved = new Dictionary<Cartheim.Core.RecordedPiece, (Vector3 by, float ground)>();
            var notes = new List<string>();
            var standing = camp.Recorded.Where(p => p.Y <= OnTheGround).ToList();
            foreach (var piece in standing)
            {
                var kind = CartGarden.Measure(GamePieces.Prefab(piece.Prefab));
                if (!kind.Prefab)
                    continue;
                var recordedAt = ruin.transform.position + turn * new Vector3(piece.X, 0f, piece.Z);
                var facing = Turn(ruin, piece.Yaw);
                if (Structural(piece.Prefab))
                {
                    var ground = Highest(recordedAt, facing, kind.Box);
                    moved[piece] = (Vector3.zero, ground);
                    placed[piece] = new Vector3(recordedAt.x, ground + piece.Y, recordedAt.z);
                    continue;
                }
                var nudges = Nudges();
                foreach (var by in nudges)
                {
                    var at = recordedAt + by;
                    var ground = Highest(at, facing, kind.Box);
                    var blockers = Blockers(at + Vector3.up * (ground + piece.Y - at.y), facing, kind.Box, piece.Prefab);
                    if (blockers.Count > 0 && by != nudges.Last())
                        continue;
                    if (blockers.Count > 0)
                    {
                        notes.Add($"{piece.Prefab} left out ({string.Join(", ", blockers)} in the way)");
                        break;
                    }
                    moved[piece] = (by, ground);
                    placed[piece] = new Vector3(at.x, ground + piece.Y, at.z);
                    if (by != Vector3.zero)
                        notes.Add($"{piece.Prefab} moved {by.magnitude:0.00} m");
                    break;
                }
            }
            foreach (var piece in camp.Recorded.Where(p => p.Y > OnTheGround))
            {
                var at = ruin.transform.position + turn * new Vector3(piece.X, 0f, piece.Z);
                var under = standing.OrderBy(s => (s.X - piece.X) * (s.X - piece.X) + (s.Z - piece.Z) * (s.Z - piece.Z)).FirstOrDefault();
                if (under != null && Mathf.Sqrt((under.X - piece.X) * (under.X - piece.X) + (under.Z - piece.Z) * (under.Z - piece.Z)) <= RestingOnWithin)
                {
                    // With what it rests on: left out with it, moved with it.
                    if (!moved.TryGetValue(under, out var with))
                        continue;
                    placed[piece] = new Vector3(at.x + with.by.x, with.ground + piece.Y, at.z + with.by.z);
                }
                else
                    placed[piece] = new Vector3(at.x, CartGarden.Ground(at) + piece.Y, at.z);
            }
            if (notes.Count > 0)
                Debug.Log($"[Cartheim] The {camp.Name} camp, fitted to this building: {string.Join("; ", notes)}");
            return placed;
        }

        // Where to try a piece: where it was recorded, then round it, nearest first, out to the reach.
        private static Vector3[] Nudges()
        {
            var tries = new List<Vector3> { Vector3.zero };
            for (var ring = NudgeStep; ring <= NudgeReach + 0.001f; ring += NudgeStep)
                for (var i = 0; i < 8; i++)
                {
                    var angle = i * Mathf.PI / 4f;
                    tries.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ring);
                }
            return tries.ToArray();
        }

        // The highest ground anywhere under a piece's footprint: the land, or a floor of the building's over it (looked for
        // from just above the building's own floor, so not its roof). At, the building's height.
        private const float FloorsWithin = 1.5f;

        private static float Highest(Vector3 at, Quaternion facing, Bounds box)
        {
            var corners = new[] { new Vector3(box.min.x, 0f, box.min.z), new Vector3(box.min.x, 0f, box.max.z), new Vector3(box.max.x, 0f, box.min.z), new Vector3(box.max.x, 0f, box.max.z), Vector3.zero };
            var solid = LayerMask.GetMask("terrain", "piece", "static_solid");
            return corners.Select(c =>
            {
                var spot = at + facing * c;
                var land = CartGarden.Ground(spot);
                return Physics.Raycast(new Vector3(spot.x, at.y + FloorsWithin, spot.z), Vector3.down, out var hit, 2f * FloorsWithin, solid, QueryTriggerInteraction.Ignore)
                       && !hit.collider.GetComponentInParent<Character>() ? Mathf.Max(land, hit.point.y) : land;
            }).Max();
        }

        // A building's structure (walls, roofs, floors, beams, of wood or stone), as against its furniture and props.
        private static readonly string[] StructureNames = { "wood_", "woodwall", "woodiron", "darkwood_", "stone_", "piece_grausten", "blackmarble" };

        private static bool Structural(string prefab) => StructureNames.Any(s => prefab.StartsWith(s, System.StringComparison.OrdinalIgnoreCase));

        // What of the building's would be in a piece's way there: its furniture and props anywhere the piece would be, its
        // structure only well inside it (a piece may stand against a wall, under a slope). Not the camp's own pieces, the
        // cart and what rides in it, creatures, things lying about, floors and rugs.
        private static List<string> Blockers(Vector3 at, Quaternion facing, Bounds box, string ignoring = null, bool structure = true,
            System.Func<ZNetView, bool> alsoIgnore = null)
        {
            var terrain = LayerMask.NameToLayer("terrain");
            var middle = at + facing * box.center + Vector3.up * 0.05f;
            var found = new List<string>();
            foreach (var (shrink, structural) in new[] { (0.75f, false), (0.4f, true) })
            {
                if (structural && !structure)
                    continue;
                foreach (var collider in Physics.OverlapBox(middle, Vector3.Scale(box.extents, new Vector3(shrink, shrink, shrink)), facing, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (collider.gameObject.layer == terrain || collider.GetComponentInParent<Character>() || collider.GetComponentInParent<Vagon>()
                        || collider.GetComponentInParent<CartRider>() || collider.GetComponentInParent<ItemDrop>() || collider.GetComponentInParent<Pickable>())
                        continue;
                    var view = collider.GetComponentInParent<ZNetView>();
                    if (!view || IsCamp(view) || alsoIgnore?.Invoke(view) == true)
                        continue;
                    var name = Utils.GetPrefabName(view.gameObject);
                    if (name == ignoring || name.StartsWith("rug_") || name.StartsWith("wood_floor") || Structural(name) != structural)
                        continue;
                    found.Add(name);
                }
            }
            return found.Distinct().ToList();
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
            // Hitched: unhitched first.
            var vagon = cart.GetComponent<Vagon>();
            if (vagon && vagon.IsAttached() && Unhitch != null)
                Unhitch.Invoke(vagon, null);
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
            if (camp.Recorded != null)
            {
                MendRoofAndDoor(ruin, camp);
                var cultivated = CartGarden.Cultivate(camp.Cultivated.Select(c => ruin.transform.position + Turn(ruin, 0f) * new Vector3(c.x, 0f, c.z)));
                if (camp.Cultivated.Count > 0)
                    Debug.Log($"[Cartheim] The {camp.Name}'s garden ground: cultivated at {cultivated} of {camp.Cultivated.Count} spots (the rest already were)");
                if (camp.Cultivated.Count > 0)
                    player.StartCoroutine(GardenCheck(ruin, camp));
            }
            MakeSpareBed(ruin, camp);
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
        // (A bed no player built is one a camp made of a building's spare, given before they were marked so.)
        private static bool NothingBack(Piece __instance) =>
            !IsCamp(__instance) && !IsGiven(__instance) && !(__instance.GetComponent<Bed>() && __instance.GetCreator() == 0L);

        // Given to a building by a camp, to stay (its rebuilt roof and door, a spare bed): nothing back when taken down.
        private const string GivenKey = "cartheim_given";

        private static bool IsGiven(Component piece)
        {
            var zdo = piece ? piece.GetComponentInParent<ZNetView>()?.GetZDO() : null;
            return zdo != null && zdo.GetBool(GivenKey);
        }

        private static GameObject Give(string prefab, Vector3 at, Quaternion facing)
        {
            var made = GamePieces.Prefab(prefab);
            if (!made)
                return null;
            var given = Object.Instantiate(made, at, facing);
            given.GetComponent<ZNetView>()?.GetZDO()?.Set(GivenKey, true);
            return given;
        }

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
            {
                fire.m_infiniteFuel = true;
                // Burning whatever's over it (a camp's fire under its shelter's roof).
                fire.m_disableCoverCheck = true;
            }
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
