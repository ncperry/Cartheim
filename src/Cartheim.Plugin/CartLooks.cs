using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using UnityEngine;

namespace Cartheim.Plugin
{
    // What a cart shows of what is built into it: copies of the pieces' own models. The bed lies lengthwise down the
    // middle; the workbench and forge hang off the sides, the chest and barrel off the back; a roof on four posts covers
    // it all. (The rug lies under the tent, shown while it's pitched; see Rug.) Only looks, made by every player from the
    // cart's upgrades, so nothing goes over the network; the bed, stations and containers can be hovered and used with E
    // (see CartPieceHover).
    internal class CartLooks
    {
        // How far the roof reaches past the cart's sides and ends, and how high its eaves are above the cart's floor.
        private const float RoofOverhang = 0.25f;
        private const float EavesHeight = 1.4f;
        // How far each half of the roof reaches past the ridge, so the two meet.
        private const float RidgeOverlap = 0.12f;
        private const string CrossPiece = "wood_wall_roof_top";
        private const string Cauldron = "piece_cauldron";
        private const string MeadKetill = "piece_MeadCauldron";
        // The fermenter shows as small as a growler.
        private const float GrowlerWidth = 0.36f;
        private const float GrowlerHeight = 0.56f;
        private const string FeyLights = "piece_FairylightGarland";
        private const string Sconce = "piece_walltorch";
        private const string BeamPiece = "wood_beam";
        // The rug lies just above the cart's floor.
        private const float RugLift = 0.02f;

        public GameObject Root { get; private set; }

        // Where each piece riding hidden in the cart should be, so using it (sleeping in the bed) happens on its model.
        public readonly Dictionary<string, Transform> Anchors = new Dictionary<string, Transform>();

        // The pieces to show. The bed, rug, chest and roof are whichever kind is built (the best roof).
        public static List<string> Shown(CartUpgrades upgrades)
        {
            var shown = new List<string>();
            foreach (var piece in GamePieces.Beds.Concat(new[] { GamePieces.WorkbenchPiece, "forge", Cauldron })
                         .Concat(GamePieces.Chests).Append("piece_chest_barrel").Append(MeadKetill).Append(GamePieces.Fermenter))
                if (upgrades.Has(piece))
                    shown.Add(piece);
            if (upgrades.Has(SteamEngine.Engine))
                shown.Add(SteamEngine.Engine);
            var roof = GamePieces.Roofs.LastOrDefault(upgrades.Has);
            // After the roof: the hives hang from its posts (shown by slot, one per hive built). (The rug lies under the
            // tent, shown with it.)
            if (roof != null)
                shown.Add(roof);
            if (upgrades.Has(Sconce))
                shown.Add(Sconce);
            if (upgrades.Has(FeyLights))
                shown.Add(FeyLights);
            shown.AddRange(GamePieces.BeehiveSlots.Take(upgrades.Count(CartUpgrades.Beehive)));
            return shown;
        }

        public CartLooks(CartBase cart, Vagon vagon, List<string> shown)
        {
            var bed = BedOf(vagon);
            Root = new GameObject("Cartheim looks");
            Root.transform.SetParent(vagon.transform, false);
            var floor = bed.min.y + RugLift;
            var w = bed.size.x;
            var l = bed.size.z;
            var cx = bed.center.x;
            var cz = bed.center.z;
            foreach (var piece in shown)
            {
                if (piece == Sconce)
                {
                    var sconce = Model(Sconce, Root.transform);
                    SconceOnBackPost(bed, sconce);
                    // Fed with resin like any sconce: hovered and used as the real one riding hidden with it.
                    if (sconce)
                        Hover(sconce, cart, Sconce, vagon.gameObject.layer);
                    continue;
                }
                if (piece == FeyLights)
                {
                    FeyLightsAcrossTheFront(bed);
                    continue;
                }
                if (GamePieces.BeehiveSlots.Contains(piece))
                {
                    Hive(piece, bed, floor, cart, vagon.gameObject.layer);
                    continue;
                }
                if (GamePieces.Roofs.Contains(piece))
                {
                    Roof(GamePieces.MadeOf(piece), bed);
                    continue;
                }
                var model = Model(piece == SteamEngine.Engine ? GamePieces.EngineLooksLike : piece, Root.transform);
                if (!model)
                    continue;
                if (GamePieces.Beds.Contains(piece))
                    Stand(model, new Vector3(cx, floor, cz), w, l, 1.2f);
                else if (piece == GamePieces.WorkbenchPiece)
                    Stand(model, new Vector3(bed.min.x - 0.15f, floor, cz), 0.7f, l * 0.8f, 0.9f, 180f);
                else if (piece == Cauldron)
                    // On the forge's side, just ahead of it towards the cart's handles, sticking out so the whole bowl shows.
                    Stand(model, new Vector3(bed.max.x + 0.3f, floor, bed.max.z - 0.1f), 0.45f, 0.45f, 0.6f);
                else if (piece == SteamEngine.Engine)
                    // Across from the cauldron, on the workbench's side, just ahead of it towards the cart's handles.
                    Stand(model, new Vector3(bed.min.x - 0.3f, floor, bed.max.z - 0.1f), 0.5f, 0.5f, 0.9f);
                else if (piece == MeadKetill)
                    // Behind the forge, mirroring the cauldron ahead of it: the brewing goes together.
                    Stand(model, new Vector3(bed.max.x + 0.3f, floor, bed.min.z + 0.1f), 0.45f, 0.45f, 0.6f);
                else if (piece == GamePieces.Fermenter)
                    // A growler standing on the chest, or where the chest would be. Its spigot faces the back of the cart.
                    Stand(model, _chestTop ?? new Vector3(cx - w / 4f, floor, bed.min.z - 0.1f), GrowlerWidth, GrowlerWidth, GrowlerHeight,
                        FermenterLook.SpigotBack(GamePieces.Prefab(piece)?.GetComponent<Fermenter>()));
                else if (piece == "forge")
                    Stand(model, new Vector3(bed.max.x + 0.15f, floor, cz), 0.7f, l * 0.7f, 0.9f);
                else if (GamePieces.Chests.Contains(piece))
                {
                    Stand(model, new Vector3(cx - w / 4f, floor, bed.min.z - 0.1f), w / 2f, 0.6f, 0.6f);
                    var top = BoundsOf(model.GetComponentsInChildren<MeshFilter>(), Root.transform);
                    if (top != null)
                        _chestTop = new Vector3(top.Value.center.x, top.Value.max.y, top.Value.center.z);
                }
                else
                    Stand(model, new Vector3(cx + w / 4f, floor, bed.min.z - 0.1f), w / 2.5f, 0.6f, 0.7f);
                Anchors[piece] = model.transform.GetChild(0);
                Hover(model, cart, piece, vagon.gameObject.layer);
            }
            Debug.Log($"[Cartheim] Cart bed spans {bed.min} to {bed.max}; showing {string.Join(", ", shown)}; raycasts hit triggers: {Physics.queriesHitTriggers}");
        }

        public void Destroy()
        {
            if (Root)
                Object.Destroy(Root);
            Anchors.Clear();
        }

        // The cart's bed, in the cart's own space: where the game stacks its load (crates and sacks) when it is full.
        internal static Bounds BedOf(Vagon vagon)
        {
            var meshes = vagon.m_loadVis.Where(l => l.m_gameobject).SelectMany(l => l.m_gameobject.GetComponentsInChildren<MeshFilter>(true));
            return BoundsOf(meshes, vagon.transform) ?? new Bounds(new Vector3(0f, 0.8f, 0f), new Vector3(1.4f, 0.7f, 1.9f));
        }

        // Shrunk and maybe turned to fit its spot, standing on it, turned a further yaw degrees.
        private static void Stand(GameObject model, Vector3 spot, float spotWidth, float spotLength, float maxHeight, float yaw = 0f)
        {
            var holder = model.transform.GetChild(0);
            var size = BoundsOf(holder.GetComponentsInChildren<MeshFilter>(), model.transform);
            if (size == null)
                return;
            var b = size.Value;
            var fit = Fit.Into(spotWidth, spotLength, maxHeight, b.size.x, b.size.z, b.size.y);
            // Turned to fit, and then (the workbench) to face out from the cart.
            holder.localRotation = Quaternion.Euler(0f, (fit.Turned ? 90f : 0f) + yaw, 0f);
            holder.localPosition = -(holder.localRotation * new Vector3(b.center.x, b.min.y, b.center.z));
            model.transform.localPosition = spot;
            model.transform.localScale = Vector3.one * fit.Scale;
            Debug.Log($"[Cartheim] {model.name}: size {b.size}, scale {fit.Scale:0.00}{(fit.Turned ? ", turned" : "")}");
        }

        // A rug laid flat in the middle of an area of the parent (the floor of a pitched tent), turned so its long side
        // runs along the area's, as big as fits without stretching it out of shape.
        public static GameObject Rug(Transform parent, string piece, Bounds area)
        {
            var model = Model(piece, parent);
            if (!model)
                return null;
            var holder = model.transform.GetChild(0);
            var size = BoundsOf(holder.GetComponentsInChildren<MeshFilter>(), model.transform);
            if (size == null)
                return model;
            var b = size.Value;
            var turned = b.size.x > b.size.z != area.size.x > area.size.z;
            holder.localRotation = turned ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            holder.localPosition = -(holder.localRotation * new Vector3(b.center.x, b.min.y, b.center.z));
            var width = turned ? b.size.z : b.size.x;
            var length = turned ? b.size.x : b.size.z;
            var scale = Mathf.Min(area.size.x / width, area.size.z / length);
            model.transform.localPosition = new Vector3(area.center.x, area.min.y + RugLift, area.center.z);
            model.transform.localScale = new Vector3(scale, 1f, scale);
            Debug.Log($"[Cartheim] {piece} laid under the tent: size {b.size}, scale {scale:0.00}");
            return model;
        }


        // The middle of the chest's lid, once the chest stands in the cart.
        private Vector3? _chestTop;

        // The top of the roof, down its middle, once a roof is built.
        private Vector3? _ridge;
        // How steeply the roof rises (up per across).
        private float _pitch;
        // How far each half of the roof reaches across from the ridge, and how thick its covering is above the ridge line.
        private float _roofHalfWidth;
        private float _roofThickness;

        // The wild beehive's look (as found hanging in trees), small.
        private const string WildHive = "Beehive";
        private const float HiveSize = 0.4f;
        // A hive's middle hangs this far behind the back posts' middle: past the post.
        private const float BehindPost = 0.3f;

        // A beehive hangs off the back of one of the roof's two back posts (the first hive the left, the second the right),
        // hung by its top where the post meets the roof's edge beam (which has climbed from the eave by then), sticking out
        // behind the cart; with no roof (so no posts), it sits on the floor there.
        private void Hive(string slot, Bounds bed, float floor, CartBase cart, int layer)
        {
            var model = Model(WildHive, Root.transform);
            if (!model)
            {
                Debug.LogWarning($"[Cartheim] No {WildHive} model to show the cart's hive with");
                return;
            }
            var holder = model.transform.GetChild(0);
            var size = BoundsOf(holder.GetComponentsInChildren<MeshFilter>(), model.transform);
            if (size == null)
                return;
            var b = size.Value;
            var fit = Fit.Into(HiveSize, HiveSize, HiveSize, b.size.x, b.size.z, b.size.y);
            var x = slot.Contains('#') ? bed.max.x : bed.min.x;
            var z = bed.min.z - BehindPost;
            if (_ridge.HasValue)
            {
                // Hung by its top.
                holder.localPosition = -new Vector3(b.center.x, b.max.y, b.center.z);
                model.transform.localPosition = new Vector3(x, bed.min.y + EavesHeight + RoofOverhang * _pitch, z);
            }
            else
            {
                holder.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);
                // Inside the back corner, clear of the chest and barrel hanging off the back.
                model.transform.localPosition = new Vector3(x, floor, bed.min.z + HiveSize);
            }
            model.transform.localScale = Vector3.one * fit.Scale;
            Anchors[slot] = holder;
            Hover(model, cart, slot, layer);
            Debug.Log($"[Cartheim] Cart hive {slot}: size {b.size}, scale {fit.Scale:0.00}, {(_ridge.HasValue ? "hanging off a back post" : "on the floor")}");
        }

        // Fey lights strung across the front of the cart (its handle end): hung from the eaves between the two front posts
        // with a roof, along the top of the front rail without. Their light comes from the garland, so it shines there.
        private void FeyLightsAcrossTheFront(Bounds bed)
        {
            var model = Model(FeyLights, Root.transform);
            if (!model)
                return;
            var holder = model.transform.GetChild(0);
            var size = BoundsOf(holder.GetComponentsInChildren<MeshFilter>(), model.transform);
            if (size == null)
                return;
            var b = size.Value;
            var fit = Fit.Into(bed.size.x, 0.3f, 0.6f, b.size.x, b.size.z, b.size.y);
            holder.localRotation = fit.Turned ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            // Hung by its top.
            holder.localPosition = -(holder.localRotation * new Vector3(b.center.x, b.max.y, b.center.z));
            var top = _ridge.HasValue ? bed.min.y + EavesHeight : bed.max.y;
            model.transform.localPosition = new Vector3(bed.center.x, top, bed.max.z);
            model.transform.localScale = Vector3.one * fit.Scale;
            Anchors[FeyLights] = holder;
            Debug.Log($"[Cartheim] Fey lights across the front: size {b.size}, scale {fit.Scale:0.00}{(fit.Turned ? ", turned" : "")}");
        }

        // The sconce on the outer face of the back post on the workbench's side, near its top, pointing away from the cart.
        // Its flame (on the hidden sconce riding with this model) burns there.
        private void SconceOnBackPost(Bounds bed, GameObject model)
        {
            if (!model)
                return;
            var holder = model.transform.GetChild(0);
            var size = BoundsOf(holder.GetComponentsInChildren<MeshFilter>(), model.transform);
            if (size == null)
                return;
            var b = size.Value;
            // Small; the real sconce riding hidden with this model is shrunk to match, so its flame stays in the bracket.
            var scale = Mathf.Min(1f, SconceSize / Mathf.Max(b.size.x, b.size.y, b.size.z));
            // The sconce points along its own x, its bracket at the low end: turned so it points straight out from the
            // cart's side, its bracket against the post.
            holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
            holder.localPosition = -(holder.localRotation * new Vector3(b.min.x, b.center.y, b.center.z));
            var top = _ridge.HasValue ? bed.min.y + EavesHeight : bed.max.y + SconceBelowEaves;
            model.transform.localPosition = new Vector3(bed.min.x - PostHalfWidth, top - SconceBelowEaves, bed.min.z);
            model.transform.localScale = Vector3.one * scale;
            Anchors[Sconce] = holder;
            Debug.Log($"[Cartheim] Sconce on the back post: size {b.size}, scale {scale:0.00}");
        }

        private const float SconceSize = 0.5f;
        private const float SconceBelowEaves = 0.65f;
        // Half a roof post's thickness, so a sconce sits on its face.
        private const float PostHalfWidth = 0.08f;
        // A gable roof of the roof piece, its two halves rising from eaves over the cart's sides until their top edges meet
        // in a ridge down its middle, a wood roof cross on the ridge at each end, on a post at each corner. Placed by the
        // pieces' snap points, as the game fits them together on a house, not by their meshes (thatch hangs past its edges).
        private void Roof(string piece, Bounds bed)
        {
            var eaves = bed.min.y + EavesHeight;
            var halfWidth = bed.size.x / 2f + RoofOverhang;
            var slope = Slope.Of(GamePieces.Prefab(piece));
            // One scale across and up, so the roof keeps its pitch (and the cross matches it); its own along the ridge.
            var scale = halfWidth / slope.Run;
            var alongRidge = (bed.size.z + RoofOverhang) / slope.Width;
            var ridge = new Vector3(bed.center.x, eaves + scale * slope.Rise, bed.center.z);
            _ridge = ridge;
            _pitch = slope.Rise / slope.Run;
            _roofHalfWidth = halfWidth;
            // The top of the roof's thatch (or shingles) above the line its pieces fit together on.
            var piecePrefab = GamePieces.Prefab(piece);
            var meshes = piecePrefab ? BoundsOf(piecePrefab.GetComponentsInChildren<MeshFilter>(true), piecePrefab.transform) : null;
            _roofThickness = meshes.HasValue ? Mathf.Max(0f, meshes.Value.max.y - slope.Top.y) * scale : 0f;
            foreach (var side in new[] { -1f, 1f })
            {
                var model = Model(piece, Root.transform);
                if (!model)
                    return;
                var holder = model.transform.GetChild(0);
                // Turned so it rises from this side towards the middle, its top edge on the ridge.
                holder.localRotation = Quaternion.FromToRotation(slope.Rising, new Vector3(-side, 0f, 0f));
                holder.localPosition = -(holder.localRotation * slope.Top);
                model.transform.localScale = new Vector3(scale, scale, alongRidge);
                model.transform.localPosition = ridge;
            }
            foreach (var end in new[] { -1f, 1f })
            {
                var onRidge = ridge + new Vector3(0f, 0f, end * alongRidge * slope.Width / 2f);
                GableCross(onRidge, scale);
                // A beam down each edge of this end, eave to ridge, on the same line as the cross's logs.
                foreach (var side in new[] { -1f, 1f })
                    Beam(onRidge + new Vector3(side * halfWidth, eaves - ridge.y, 0f), onRidge, scale);
            }
            foreach (var x in new[] { bed.min.x, bed.max.x })
                foreach (var z in new[] { bed.min.z, bed.max.z })
                {
                    var post = Model("wood_pole2", Root.transform);
                    if (!post)
                        return;
                    var holder = post.transform.GetChild(0);
                    var size = BoundsOf(holder.GetComponentsInChildren<MeshFilter>(), post.transform);
                    if (size == null)
                        return;
                    var b = size.Value;
                    holder.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);
                    post.transform.localScale = new Vector3(0.6f, (eaves - bed.min.y) / b.size.y, 0.6f);
                    post.transform.localPosition = new Vector3(x, bed.min.y, z);
                }
            Debug.Log($"[Cartheim] Cart roof of {piece}: run {slope.Run}, rise {slope.Rise}, width {slope.Width}, rising along {slope.Rising}; scale {scale:0.00}");
            LogShape(piece);
            LogShape(CrossPiece);
            LogShape(BeamPiece);
        }

        // The wood roof cross, its logs running on up the two slopes and crossing on the ridge, at the roof's scale.
        private void GableCross(Vector3 onRidge, float scale)
        {
            var cross = Model(CrossPiece, Root.transform);
            if (!cross)
                return;
            var holder = cross.transform.GetChild(0);
            // In its own space it stands across x, and its logs cross at its middle snap point.
            var points = SnapPoints(GamePieces.Prefab(CrossPiece));
            holder.localPosition = -(points.Count == 0 ? new Vector3(0f, 0.5f, 0f) : points.OrderBy(p => Mathf.Abs(p.x)).First());
            // Its logs are modelled thicker than a wood beam; across its face it shrinks until they match the beams (the same
            // shrink both ways keeps their angle, so they stay in line with the beams), and keeps the roof's scale in depth.
            var face = scale * LogToBeam(points);
            cross.transform.localScale = new Vector3(face, face, scale);
            cross.transform.localPosition = onRidge;
        }

        // How much thinner a wood beam is than one of the cross's logs, across: the beam's height, against how far the
        // cross's meshes reach past its top ends, square to the logs' slope, on both sides.
        private static float LogToBeam(List<Vector3> crossPoints)
        {
            var crossPrefab = GamePieces.Prefab(CrossPiece);
            var beamPrefab = GamePieces.Prefab(BeamPiece);
            var cross = crossPrefab ? BoundsOf(crossPrefab.GetComponentsInChildren<MeshFilter>(true), crossPrefab.transform) : null;
            var beam = beamPrefab ? BoundsOf(beamPrefab.GetComponentsInChildren<MeshFilter>(true), beamPrefab.transform) : null;
            if (cross == null || beam == null || crossPoints.Count < 3)
                return 1f;
            var top = crossPoints.Max(p => p.y);
            var crossing = crossPoints.OrderBy(p => Mathf.Abs(p.x)).First();
            var end = crossPoints.First(p => Mathf.Abs(p.y - top) < 0.01f);
            var pitch = Mathf.Atan2(top - crossing.y, Mathf.Abs(end.x - crossing.x));
            var log = 2f * (cross.Value.max.y - top) * Mathf.Cos(pitch);
            var ratio = log > 0f ? beam.Value.size.y / log : 1f;
            Debug.Log($"[Cartheim] Roof cross logs {log:0.00} thick, beams {beam.Value.size.y:0.00}: cross shrunk to {ratio:0.00}");
            return Mathf.Clamp(ratio, 0.3f, 1f);
        }

        // A wood beam from one point to another, as thick as the roof's scale makes it.
        private void Beam(Vector3 from, Vector3 to, float scale)
        {
            var beam = Model(BeamPiece, Root.transform);
            if (!beam)
                return;
            var holder = beam.transform.GetChild(0);
            // Its length runs between its two farthest snap points (its ends).
            var points = SnapPoints(GamePieces.Prefab(BeamPiece));
            var ends = points.SelectMany(a => points.Select(b => (a, b))).OrderByDescending(p => Vector3.Distance(p.a, p.b)).FirstOrDefault();
            if (points.Count < 2)
                ends = (new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f));
            var along = ends.b - ends.a;
            var axis = along.normalized;
            var lengthwise = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
            var stretch = Vector3.one * scale + lengthwise * (Vector3.Distance(from, to) / along.magnitude - scale);
            holder.localScale = stretch;
            holder.localPosition = -Vector3.Scale(stretch, (ends.a + ends.b) / 2f);
            beam.transform.localRotation = Quaternion.FromToRotation(axis, (to - from).normalized);
            beam.transform.localPosition = (from + to) / 2f;
        }

        // Where a piece's meshes and snap points sit around its origin, for fitting the roof cross to the roof.
        private static void LogShape(string piece)
        {
            var prefab = GamePieces.Prefab(piece);
            if (!prefab)
                return;
            var b = BoundsOf(prefab.GetComponentsInChildren<MeshFilter>(true), prefab.transform);
            var snaps = prefab.GetComponentsInChildren<Transform>(true).Where(t => t.CompareTag("snappoint"))
                .Select(t => prefab.transform.InverseTransformPoint(t.position).ToString("0.00"));
            Debug.Log($"[Cartheim] Shape of {piece}: meshes {b?.min:0.00} to {b?.max:0.00}; snap points {string.Join(" ", snaps)}");
        }

        private static List<Vector3> SnapPoints(GameObject prefab) =>
            prefab ? prefab.GetComponentsInChildren<Transform>(true).Where(t => t.CompareTag("snappoint"))
                .Select(t => prefab.transform.InverseTransformPoint(t.position)).ToList() : new List<Vector3>();

        // A roof piece's slope, read from its snap points, which sit at the corners of its sloping face: which way it
        // rises, how far across and up, how wide it is along the ridge, and the middle of its top edge.
        private readonly struct Slope
        {
            public Vector3 Rising { get; }
            public float Run { get; }
            public float Rise { get; }
            public float Width { get; }
            public Vector3 Top { get; }

            private Slope(Vector3 rising, float run, float rise, float width, Vector3 top)
            {
                Rising = rising;
                Run = run;
                Rise = rise;
                Width = width;
                Top = top;
            }

            public static Slope Of(GameObject prefab)
            {
                var points = SnapPoints(prefab);
                if (points.Count < 4)
                    return new Slope(Vector3.back, 2f, 1f, 2f, new Vector3(0f, 1f, -1f));
                var low = points.Min(p => p.y);
                var high = points.Max(p => p.y);
                var bottom = points.Where(p => Mathf.Abs(p.y - low) < 0.01f).ToList();
                var top = points.Where(p => Mathf.Abs(p.y - high) < 0.01f).ToList();
                var bottomMiddle = bottom.Aggregate(Vector3.zero, (a, p) => a + p) / bottom.Count;
                var topMiddle = top.Aggregate(Vector3.zero, (a, p) => a + p) / top.Count;
                var across = topMiddle - bottomMiddle;
                across.y = 0f;
                var width = top.Count > 1 ? Vector3.Distance(top[0], top[1]) : 2f;
                return new Slope(across.normalized, across.magnitude, high - low, width, topMiddle);
            }
        }

        // A box around the model that the player's look can land on, to show the piece's hover text and use it with E. A
        // trigger, so it never bumps into anything as the cart moves.
        private static void Hover(GameObject model, CartBase cart, string piece, int layer)
        {
            var holder = model.transform.GetChild(0);
            var size = BoundsOf(holder.GetComponentsInChildren<MeshFilter>(), model.transform);
            if (size == null)
                return;
            model.layer = layer;
            var box = model.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = size.Value.center;
            box.size = size.Value.size;
            model.AddComponent<CartPieceHover>().Init(cart, piece);
        }

        // A copy of what the piece shows (its most detailed meshes), and nothing else of it.
        private static GameObject Model(string piece, Transform parent)
        {
            var prefab = GamePieces.Prefab(piece);
            if (!prefab)
                return null;
            var lesserDetail = new HashSet<Renderer>(prefab.GetComponentsInChildren<LODGroup>(true)
                .SelectMany(g => g.GetLODs().Skip(1)).SelectMany(l => l.renderers).Where(r => r));
            var model = new GameObject("Cartheim look " + piece);
            model.transform.SetParent(parent, false);
            var holder = new GameObject("model").transform;
            holder.SetParent(model.transform, false);
            // A fermenter's lid, bubbles and full look show by what's in it (see FermenterLook), so they're copied apart.
            var fermenter = piece == GamePieces.Fermenter ? prefab.GetComponent<Fermenter>() : null;
            var states = fermenter ? new[] { fermenter.m_fermentingObject, fermenter.m_readyObject, fermenter.m_topObject } : new GameObject[0];
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mesh = renderer.GetComponent<MeshFilter>();
                if (!renderer.enabled || !mesh || !mesh.sharedMesh || lesserDetail.Contains(renderer) || !Shows(renderer.transform, prefab.transform) ||
                    (OnlyParts.TryGetValue(piece, out var only) && !renderer.name.StartsWith(only)) ||
                    states.Any(s => s && renderer.transform.IsChildOf(s.transform)))
                    continue;
                var part = new GameObject(renderer.name, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(holder, false);
                var place = prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                part.transform.localPosition = place.GetColumn(3);
                part.transform.localRotation = place.rotation;
                part.transform.localScale = place.lossyScale;
                part.GetComponent<MeshFilter>().sharedMesh = LegShare.TryGetValue(piece, out var legs) ? WithoutLegs(mesh.sharedMesh, place, legs) : mesh.sharedMesh;
                part.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            if (fermenter)
                holder.gameObject.AddComponent<FermenterLook>().Copy(fermenter, holder);
            return model;
        }

        // The only part of a piece shown on a cart, by name: the cauldron's bowl, without its tripod and chains.
        private static readonly Dictionary<string, string> OnlyParts = new Dictionary<string, string> { [Cauldron] = "cauldron" };

        // How much of a piece's height is its legs, cut off when it's shown on a cart: it rests on the cart instead.
        private static readonly Dictionary<string, float> LegShare = new Dictionary<string, float>
        {
            [GamePieces.WorkbenchPiece] = 0.4f,
            ["forge"] = 0.5f,
            [MeadKetill] = 0.35f,
        };

        // Each mesh with its legs cut off, made once.
        private static readonly Dictionary<Mesh, Mesh> Legless = new Dictionary<Mesh, Mesh>();

        // The mesh without the triangles reaching into the bottom share of its height (as the piece stands): a leg runs
        // from the floor up to the top in long faces, so only what lies wholly above the cut is kept.
        private static Mesh WithoutLegs(Mesh mesh, Matrix4x4 place, float share)
        {
            if (!mesh.isReadable)
                return mesh;
            if (Legless.TryGetValue(mesh, out var made) && made)
                return made;
            var heights = mesh.vertices.Select(v => place.MultiplyPoint3x4(v).y).ToArray();
            var lowest = heights.Min();
            var cut = lowest + share * (heights.Max() - lowest);
            var legless = Object.Instantiate(mesh);
            legless.name = mesh.name + " without legs";
            for (var sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var triangles = mesh.GetTriangles(sub);
                var kept = new List<int>();
                for (var i = 0; i + 2 < triangles.Length; i += 3)
                    if (Mathf.Min(heights[triangles[i]], heights[triangles[i + 1]], heights[triangles[i + 2]]) >= cut)
                        kept.AddRange(new[] { triangles[i], triangles[i + 1], triangles[i + 2] });
                legless.SetTriangles(kept, sub);
            }
            // Bounds from the vertices still drawn: the legs' vertices stay in the mesh, unused.
            var vertices = mesh.vertices;
            var used = Enumerable.Range(0, mesh.subMeshCount).SelectMany(legless.GetTriangles).Distinct().Select(i => vertices[i]).ToList();
            if (used.Count > 0)
            {
                var bounds = new Bounds(used[0], Vector3.zero);
                foreach (var v in used)
                    bounds.Encapsulate(v);
                legless.bounds = bounds;
            }
            Legless[mesh] = legless;
            return legless;
        }

        // Whether the part shows on a piece as built: it and every parent up to the piece switched on.
        private static bool Shows(Transform part, Transform piece)
        {
            for (var t = part; t && t != piece; t = t.parent)
                if (!t.gameObject.activeSelf)
                    return false;
            return true;
        }

        // The box around the meshes, in the space of `relativeTo`.
        private static Bounds? BoundsOf(IEnumerable<MeshFilter> meshes, Transform relativeTo)
        {
            Bounds? all = null;
            foreach (var mesh in meshes)
            {
                if (!mesh.sharedMesh)
                    continue;
                var toSpace = relativeTo.worldToLocalMatrix * mesh.transform.localToWorldMatrix;
                var m = mesh.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = toSpace.MultiplyPoint3x4(m.center + Vector3.Scale(m.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (all == null)
                        all = new Bounds(corner, Vector3.zero);
                    else
                    {
                        var b = all.Value;
                        b.Encapsulate(corner);
                        all = b;
                    }
                }
            }
            return all;
        }
    }
}
