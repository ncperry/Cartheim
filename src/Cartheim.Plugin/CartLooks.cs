using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using UnityEngine;

namespace Cartheim.Plugin
{
    // What a cart shows of what is built into it: small copies of the pieces' own models standing in its bed (a workbench
    // and forge at the front, the chest and barrel at the back) on the rug, which covers the bed. Only looks: no colliders
    // or game logic, made by every player from the cart's upgrades, so nothing goes over the network.
    internal static class CartLooks
    {
        // How tall a piece may stand in the cart.
        private const float MaxHeight = 0.9f;
        // The rug lies just above the bed's floor.
        private const float RugLift = 0.02f;

        // The pieces to show, by their spot. The rug and chest are whichever kind is built.
        public static List<string> Shown(CartUpgrades upgrades)
        {
            var shown = new List<string>();
            foreach (var piece in new[] { GamePieces.WorkbenchPiece, "forge" }.Concat(GamePieces.Rugs).Concat(GamePieces.Chests).Append("piece_chest_barrel"))
                if (upgrades.Has(piece))
                    shown.Add(piece);
            return shown;
        }

        public static GameObject Make(Vagon vagon, List<string> shown)
        {
            var bed = BedOf(vagon);
            var looks = new GameObject("Cartheim looks");
            looks.transform.SetParent(vagon.transform, false);
            var halfWidth = bed.size.x / 2f;
            var halfLength = bed.size.z / 2f;
            foreach (var piece in shown)
            {
                var model = Model(piece, looks.transform);
                if (!model)
                    continue;
                if (GamePieces.Rugs.Contains(piece))
                    LayOver(model, bed);
                else
                {
                    // The bed in quarters: workbench front left, forge front right, chest back left, barrel back right.
                    var front = piece == GamePieces.WorkbenchPiece || piece == "forge";
                    var left = piece == GamePieces.WorkbenchPiece || GamePieces.Chests.Contains(piece);
                    var spot = new Vector3(bed.center.x + (left ? -1 : 1) * halfWidth / 2f, bed.min.y + RugLift, bed.center.z + (front ? 1 : -1) * halfLength / 2f);
                    Stand(model, spot, halfWidth, halfLength);
                }
            }
            Debug.Log($"[Cartheim] Cart bed spans {bed.min} to {bed.max}; showing {string.Join(", ", shown)}");
            return looks;
        }

        // The cart's bed, in the cart's own space: where the game stacks its load (crates and sacks) when it is full.
        private static Bounds BedOf(Vagon vagon)
        {
            var meshes = vagon.m_loadVis.Where(l => l.m_gameobject).SelectMany(l => l.m_gameobject.GetComponentsInChildren<MeshFilter>(true));
            return BoundsOf(meshes, vagon.transform) ?? new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 0.2f, 1.6f));
        }

        // Shrunk and maybe turned to fit its spot, standing on it.
        private static void Stand(GameObject model, Vector3 spot, float spotWidth, float spotLength)
        {
            var holder = model.transform.GetChild(0);
            var size = BoundsOf(holder.GetComponentsInChildren<MeshFilter>(), model.transform);
            if (size == null)
                return;
            var b = size.Value;
            var fit = Fit.Into(spotWidth, spotLength, MaxHeight, b.size.x, b.size.z, b.size.y);
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
            Debug.Log($"[Cartheim] {model.name}: size {b.size}, stretched over the bed{(turned ? ", turned" : "")}");
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
