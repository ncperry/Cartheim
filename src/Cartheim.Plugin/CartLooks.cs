using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using UnityEngine;

namespace Cartheim.Plugin
{
    // What a cart shows of what is built into it: copies of the pieces' own models. The bed lies lengthwise down the
    // middle on the rug, which covers the cart's bed; the workbench and forge hang off the sides, the chest and barrel off
    // the back; a roof on four posts covers it all. Only looks, made by every player from the cart's upgrades, so nothing
    // goes over the network; the bed, stations and containers can be hovered and used with E (see CartPieceHover).
    internal class CartLooks
    {
        // How far the roof reaches past the cart's sides and ends, and how high its eaves are above the cart's floor.
        private const float RoofOverhang = 0.25f;
        private const float EavesHeight = 1.4f;
        // How far each half of the roof reaches past the ridge, so the two meet.
        private const float RidgeOverlap = 0.12f;
        private const string CrossPiece = "wood_wall_roof_top";
        // The rug lies just above the cart's floor.
        private const float RugLift = 0.02f;

        public GameObject Root { get; private set; }

        // Where each piece riding hidden in the cart should be, so using it (sleeping in the bed) happens on its model.
        public readonly Dictionary<string, Transform> Anchors = new Dictionary<string, Transform>();

        // The pieces to show. The bed, rug, chest and roof are whichever kind is built (the best roof).
        public static List<string> Shown(CartUpgrades upgrades)
        {
            var shown = new List<string>();
            foreach (var piece in GamePieces.Beds.Concat(new[] { GamePieces.WorkbenchPiece, "forge" }).Concat(GamePieces.Rugs)
                         .Concat(GamePieces.Chests).Append("piece_chest_barrel"))
                if (upgrades.Has(piece))
                    shown.Add(piece);
            var roof = GamePieces.Roofs.LastOrDefault(upgrades.Has);
            if (roof != null)
                shown.Add(roof);
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
                if (GamePieces.Roofs.Contains(piece))
                {
                    Roof(GamePieces.MadeOf(piece), bed);
                    continue;
                }
                var model = Model(piece, Root.transform);
                if (!model)
                    continue;
                if (GamePieces.Rugs.Contains(piece))
                {
                    LayOver(model, bed);
                    continue;
                }
                if (GamePieces.Beds.Contains(piece))
                    Stand(model, new Vector3(cx, floor, cz), w, l, 1.2f);
                else if (piece == GamePieces.WorkbenchPiece)
                    Stand(model, new Vector3(bed.min.x - 0.15f, floor, cz), 0.7f, l * 0.8f, 0.9f);
                else if (piece == "forge")
                    Stand(model, new Vector3(bed.max.x + 0.15f, floor, cz), 0.7f, l * 0.7f, 0.9f);
                else if (GamePieces.Chests.Contains(piece))
                    Stand(model, new Vector3(cx - w / 4f, floor, bed.min.z - 0.1f), w / 2f, 0.6f, 0.6f);
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
        private static Bounds BedOf(Vagon vagon)
        {
            var meshes = vagon.m_loadVis.Where(l => l.m_gameobject).SelectMany(l => l.m_gameobject.GetComponentsInChildren<MeshFilter>(true));
            return BoundsOf(meshes, vagon.transform) ?? new Bounds(new Vector3(0f, 0.8f, 0f), new Vector3(1.4f, 0.7f, 1.9f));
        }

        // Shrunk and maybe turned to fit its spot, standing on it.
        private static void Stand(GameObject model, Vector3 spot, float spotWidth, float spotLength, float maxHeight)
        {
            var holder = model.transform.GetChild(0);
            var size = BoundsOf(holder.GetComponentsInChildren<MeshFilter>(), model.transform);
            if (size == null)
                return;
            var b = size.Value;
            var fit = Fit.Into(spotWidth, spotLength, maxHeight, b.size.x, b.size.z, b.size.y);
            holder.localRotation = fit.Turned ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            holder.localPosition = -(holder.localRotation * new Vector3(b.center.x, b.min.y, b.center.z));
            model.transform.localPosition = spot;
            model.transform.localScale = Vector3.one * fit.Scale;
            Debug.Log($"[Cartheim] {model.name}: size {b.size}, scale {fit.Scale:0.00}{(fit.Turned ? ", turned" : "")}");
        }

        // Stretched over the whole bed, just above its floor.
        private static void LayOver(GameObject model, Bounds bed)
        {
            var holder = model.transform.GetChild(0);
            var size = BoundsOf(holder.GetComponentsInChildren<MeshFilter>(), model.transform);
            if (size == null)
                return;
            var b = size.Value;
            var turned = b.size.x > b.size.z != bed.size.x > bed.size.z;
            holder.localRotation = turned ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            holder.localPosition = -(holder.localRotation * new Vector3(b.center.x, b.min.y, b.center.z));
            var width = turned ? b.size.z : b.size.x;
            var length = turned ? b.size.x : b.size.z;
            model.transform.localPosition = new Vector3(bed.center.x, bed.min.y + RugLift / 2f, bed.center.z);
            model.transform.localScale = new Vector3(bed.size.x / width, 1f, bed.size.z / length);
        }

        // A gable roof of the roof piece, its two halves rising from eaves over the cart's sides to a ridge down its middle
        // (each reaching a little past it, so they meet), a wood roof cross filling each gable end, on a post at each corner.
        private void Roof(string piece, Bounds bed)
        {
            var eaves = bed.min.y + EavesHeight;
            var halfWidth = bed.size.x / 2f + RoofOverhang;
            var run = halfWidth + RidgeOverlap;
            var length = bed.size.z + 2f * RoofOverhang;
            var rises = RisingDirection(GamePieces.Prefab(piece));
            var rise = 0f;
            foreach (var side in new[] { -1f, 1f })
            {
                var model = Model(piece, Root.transform);
                if (!model)
                    return;
                var holder = model.transform.GetChild(0);
                // Turned so it rises from this side towards the middle.
                holder.localRotation = Quaternion.FromToRotation(rises, new Vector3(-side, 0f, 0f));
                var size = BoundsOf(holder.GetComponentsInChildren<MeshFilter>(), model.transform);
                if (size == null)
                    return;
                var b = size.Value;
                holder.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);
                var across = run / b.size.x;
                rise = across * b.size.y;
                model.transform.localScale = new Vector3(across, across, length / b.size.z);
                model.transform.localPosition = new Vector3(bed.center.x + side * (halfWidth - run / 2f), eaves, bed.center.z);
            }
            foreach (var end in new[] { bed.min.z, bed.max.z })
                GableCross(new Vector3(bed.center.x, eaves, end), 2f * halfWidth, rise);
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
            Debug.Log($"[Cartheim] Cart roof of {piece}, rising along {rises}");
        }

        // The wood roof cross, stood across the cart at one end, filling the gable under the roof.
        private void GableCross(Vector3 bottomMiddle, float width, float height)
        {
            var cross = Model(CrossPiece, Root.transform);
            if (!cross)
                return;
            var holder = cross.transform.GetChild(0);
            var size = BoundsOf(holder.GetComponentsInChildren<MeshFilter>(), cross.transform);
            if (size == null)
                return;
            // It is a wall: thin one way. Turned so its face looks along the cart.
            if (size.Value.size.x < size.Value.size.z)
            {
                holder.localRotation = Quaternion.Euler(0f, 90f, 0f);
                size = BoundsOf(holder.GetComponentsInChildren<MeshFilter>(), cross.transform);
            }
            var b = size.Value;
            holder.localPosition = -new Vector3(b.center.x, b.min.y, b.center.z);
            var across = width / b.size.x;
            cross.transform.localScale = new Vector3(across, height / b.size.y, across);
            cross.transform.localPosition = bottomMiddle;
        }

        // Which way the roof piece slopes up, in its own space: read from its snap points, which sit at its corners.
        private static Vector3 RisingDirection(GameObject prefab)
        {
            var points = prefab ? prefab.GetComponentsInChildren<Transform>(true).Where(t => t.CompareTag("snappoint"))
                .Select(t => prefab.transform.InverseTransformPoint(t.position)).ToList() : new List<Vector3>();
            if (points.Count < 2)
                return Vector3.forward;
            var mean = points.Aggregate(Vector3.zero, (a, p) => a + p) / points.Count;
            float alongX = 0f, alongZ = 0f;
            foreach (var p in points)
            {
                alongX += (p.x - mean.x) * (p.y - mean.y);
                alongZ += (p.z - mean.z) * (p.y - mean.y);
            }
            return Mathf.Abs(alongX) > Mathf.Abs(alongZ) ? new Vector3(Mathf.Sign(alongX), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(alongZ));
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
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mesh = renderer.GetComponent<MeshFilter>();
                if (!renderer.enabled || !mesh || !mesh.sharedMesh || lesserDetail.Contains(renderer) || !Shows(renderer.transform, prefab.transform))
                    continue;
                var part = new GameObject(renderer.name, typeof(MeshFilter), typeof(MeshRenderer));
                part.transform.SetParent(holder, false);
                var place = prefab.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                part.transform.localPosition = place.GetColumn(3);
                part.transform.localRotation = place.rotation;
                part.transform.localScale = place.lossyScale;
                part.GetComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                part.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            return model;
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
