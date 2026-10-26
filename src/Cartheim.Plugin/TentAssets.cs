using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using UnityEngine;

namespace Cartheim.Plugin
{
    // The cloth tent from Haldor's camp, pitched against the side of a cart: a copy of its mesh, materials and collider (so
    // it stands solid, and the game's own cover checks see a roof). The camp is a location, which the game loads only when
    // it places one; its prefab is loaded here once and held while the game runs, since the tent's mesh belongs to it.
    internal static class TentAssets
    {
        private const string Camp = "Vendor_BlackForest";
        private const string TentPart = "TraderTent";

        // Haldor's canopy ends (on its mesh's low-x side) in an edge with two boards hanging from it, this far apart along
        // its z, either side of z = 0 (as matched in game to the roof posts); its tall pole stands out at the other side. Pitched by a cart, that edge runs along
        // the cart's left side, the boards at the cart's corners, and the pole stands out away from the cart.
        private static readonly Vector3 Edge = new Vector3(0.48f, 0f, 0f);
        private const float Boards = 3.4f;

        // Squeezed along the cart so its boards meet the cart's corners; this much of Haldor's size out from the cart, and
        // up (so the edge comes level with the cart's eaves).
        private const float Across = 0.6f;
        private const float Up = 0.8f;
        // The tent's rug: this big each way, lying this far out from the cart's side, level with the cart's middle.
        private const float RugSize = 2.2f;
        private const float RugFromCart = 0.15f;
        // Ground is sampled this many times along each side of where the tent would stand.
        private const int Samples = 4;

        private static ZoneSystem.ZoneLocation _camp;
        private static MeshFilter _tent;

        // The tent's mesh, from the camp's prefab, loading the camp the first time.
        private static MeshFilter Tent()
        {
            if (_tent)
                return _tent;
            if (!ZoneSystem.instance)
                return null;
            _camp = ZoneSystem.instance.m_locations.FirstOrDefault(l => l.m_prefabName == Camp);
            if (_camp == null)
            {
                Debug.LogWarning($"[Cartheim] No {Camp} location to take the tent from");
                return null;
            }
            _camp.m_prefab.Load();
            var camp = _camp.m_prefab.Asset;
            _tent = camp ? camp.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == TentPart)?.GetComponentInChildren<MeshFilter>(true) : null;
            if (!_tent)
                Debug.LogWarning($"[Cartheim] No {TentPart} in {Camp}");
            return _tent;
        }

        // Lets go of the camp (when the mod unloads).
        public static void Release()
        {
            if (_camp != null && _tent)
                _camp.m_prefab.Release();
            _camp = null;
            _tent = null;
        }

        // The tent's mesh turned half round, so its edge faces the cart (to the root's right) and the rest stands out to the
        // cart's left; the edge's middle at the origin.
        private static readonly Quaternion MeshTurn = Quaternion.Euler(0f, 180f, 0f);

        // How much to squeeze the tent along the cart, so its boards are as far apart as the cart is long.
        private static Vector3 Squeeze(Bounds bed) => new Vector3(Across, Up, bed.size.z / Boards);

        // The space the pitched tent covers, in the space of its root (at the middle of the cart's left side, facing along
        // the cart).
        private static Bounds Footprint(Mesh mesh, Bounds bed)
        {
            var squeeze = Squeeze(bed);
            var turn = MeshTurn;
            var b = mesh.bounds;
            var corners = new List<Vector3>();
            foreach (var x in new[] { b.min.x, b.max.x })
            foreach (var y in new[] { b.min.y, b.max.y })
            foreach (var z in new[] { b.min.z, b.max.z })
                corners.Add(Vector3.Scale(turn * (new Vector3(x, y, z) - Edge), squeeze));
            var footprint = new Bounds(corners[0], Vector3.zero);
            foreach (var corner in corners)
                footprint.Encapsulate(corner);
            return footprint;
        }

        // Where the tent would stand by the cart: its root below the middle of the cart's left side, facing along the cart,
        // and the ground heights under the tent.
        public static bool Spot(Transform cart, Bounds bed, out Vector3 position, out Quaternion rotation, out float[] heights)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            heights = new float[0];
            var tent = Tent();
            if (!tent || !ZoneSystem.instance)
                return false;
            var forward = Vector3.ProjectOnPlane(cart.forward, Vector3.up).normalized;
            rotation = Quaternion.LookRotation(forward, Vector3.up);
            var post = cart.TransformPoint(new Vector3(bed.min.x, bed.min.y, bed.center.z));
            var footprint = Footprint(tent.sharedMesh, bed);
            var samples = new List<float>();
            for (var i = 0; i < Samples; i++)
            for (var j = 0; j < Samples; j++)
            {
                var local = new Vector3(Mathf.Lerp(footprint.min.x, footprint.max.x, i / (Samples - 1f)), 0f,
                    Mathf.Lerp(footprint.min.z, footprint.max.z, j / (Samples - 1f)));
                samples.Add(ZoneSystem.instance.GetGroundHeight(post + rotation * local));
            }
            heights = samples.ToArray();
            position = new Vector3(post.x, Cartheim.Core.Tent.GroundLevel(heights), post.z);
            return true;
        }

        // The tent standing at a spot (from Spot), with the cart's rug (if it has one) on its floor.
        public static GameObject Pitch(Vector3 position, Quaternion rotation, Bounds bed, string rug)
        {
            var tent = Tent();
            if (!tent)
                return null;
            var root = new GameObject("Cartheim tent");
            root.transform.SetPositionAndRotation(position, rotation);
            // Squeezed in the cart's directions, then the mesh turned within that.
            var squeeze = new GameObject("squeeze").transform;
            squeeze.SetParent(root.transform, false);
            squeeze.localScale = Squeeze(bed);
            var model = new GameObject("model", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            model.layer = tent.gameObject.layer;
            model.transform.SetParent(squeeze, false);
            model.transform.localRotation = MeshTurn;
            model.transform.localPosition = -(MeshTurn * Edge);
            model.GetComponent<MeshFilter>().sharedMesh = tent.sharedMesh;
            model.GetComponent<MeshRenderer>().sharedMaterials = tent.GetComponent<MeshRenderer>().sharedMaterials;
            model.GetComponent<MeshCollider>().sharedMesh = tent.GetComponent<MeshCollider>() is MeshCollider collider && collider.sharedMesh
                ? collider.sharedMesh
                : tent.sharedMesh;
            var footprint = Footprint(tent.sharedMesh, bed);
            var shape = root.AddComponent<TentShape>();
            shape.Footprint = footprint;
            shape.WakeAt = new Vector3(footprint.center.x, 0f, footprint.center.z);
            if (rug != null)
            {
                var onRug = new Vector3(-(RugFromCart + RugSize / 2f), 0f, 0f);
                CartLooks.Rug(root.transform, rug, new Bounds(onRug, new Vector3(RugSize, 0f, RugSize)));
                // On the rug, out of the way of a campfire in the middle of the tent.
                shape.WakeAt = onRug;
            }
            Debug.Log($"[Cartheim] Tent pitched at {position}, covering {footprint.min} to {footprint.max} from the middle of the cart's left side");
            return root;
        }

        // The tent's cloth as a tarp over a ruin: its mesh stretched over a box (in the space of a ruin at that position and
        // turn), its lower side towards the box's -x where the mesh shows which side is lower. Solid like the tent, so the
        // game's cover checks see a roof.
        public const string TarpName = "Cartheim tarp";

        public static GameObject Tarp(Vector3 position, Quaternion rotation, Vector3 min, Vector3 max, bool ghost = false)
        {
            var tent = Tent();
            if (!tent)
                return null;
            var (mesh, materials) = Cloth(tent);
            var b = mesh.bounds;
            var root = new GameObject(ghost ? "Cartheim tarp ghost" : TarpName);
            root.transform.SetPositionAndRotation(position, rotation);
            var model = new GameObject("model", typeof(MeshFilter), typeof(MeshRenderer));
            model.layer = tent.gameObject.layer;
            model.transform.SetParent(root.transform, false);
            var flip = LowerAtMaxX(mesh);
            var size = max - min;
            var scale = new Vector3(size.x / b.size.x, size.y / b.size.y, size.z / b.size.z);
            model.transform.localRotation = flip ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            model.transform.localScale = scale;
            // The mesh's bounds land on the box: its middle on the box's middle (turned half round, its middle stays put).
            var middle = Vector3.Scale(b.center, scale);
            if (flip)
                middle = new Vector3(-middle.x, middle.y, -middle.z);
            model.transform.localPosition = (min + max) / 2f - middle;
            model.GetComponent<MeshFilter>().sharedMesh = mesh;
            model.GetComponent<MeshRenderer>().sharedMaterials = materials;
            if (!ghost)
            {
                var collider = model.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }
            if (!ghost)
                Debug.Log($"[Cartheim] Tarp over the ruin at {position}: the cloth's mesh spans {b.min} to {b.max}, stretched by {scale}{(flip ? ", turned so its lower side is at -x" : "")}");
            return root;
        }

        // The tent's cloth alone, for a tarp: its poles and spars left out (stretched over a ruin they hang in the air), each
        // part of the mesh but the one drawn in cloth being poles and spars. Made once. Unreadable, or with no cloth part, the
        // whole tent is used.
        private static Mesh _cloth;
        private static Material[] _clothMaterials;

        private static (Mesh mesh, Material[] materials) Cloth(MeshFilter tent)
        {
            var whole = tent.sharedMesh;
            var materials = tent.GetComponent<MeshRenderer>().sharedMaterials;
            if (_cloth)
                return (_cloth, _clothMaterials);
            Debug.Log($"[Cartheim] Tent mesh: {whole.subMeshCount} parts, readable {whole.isReadable}: " +
                      string.Join(", ", materials.Select((m, i) => $"{i} {(m ? m.name : "none")} ({(i < whole.subMeshCount ? whole.GetIndexCount(i) / 3 : 0)} triangles)")));
            var keep = Enumerable.Range(0, Mathf.Min(whole.subMeshCount, materials.Length))
                .Where(i => materials[i] && materials[i].name.IndexOf("cloth", System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (!whole.isReadable || keep.Count == 0 || keep.Count == whole.subMeshCount)
                return (whole, materials);
            // Only the vertices the cloth uses, so its bounds are the cloth's.
            var vertices = whole.vertices;
            var normals = whole.normals;
            var uv = whole.uv;
            var index = new Dictionary<int, int>();
            var cloth = new Mesh { name = whole.name + " cloth" };
            var newVertices = new List<Vector3>();
            var newNormals = new List<Vector3>();
            var newUv = new List<Vector2>();
            var parts = keep.Select(part => whole.GetTriangles(part).Select(old =>
            {
                if (!index.TryGetValue(old, out var now))
                {
                    now = index[old] = newVertices.Count;
                    newVertices.Add(vertices[old]);
                    if (normals.Length > old)
                        newNormals.Add(normals[old]);
                    if (uv.Length > old)
                        newUv.Add(uv[old]);
                }
                return now;
            }).ToArray()).ToList();
            cloth.SetVertices(newVertices);
            if (newNormals.Count == newVertices.Count)
                cloth.SetNormals(newNormals);
            if (newUv.Count == newVertices.Count)
                cloth.SetUVs(0, newUv);
            cloth.subMeshCount = parts.Count;
            for (var i = 0; i < parts.Count; i++)
                cloth.SetTriangles(parts[i], i);
            cloth.RecalculateBounds();
            if (newNormals.Count != newVertices.Count)
                cloth.RecalculateNormals();
            _cloth = cloth;
            _clothMaterials = keep.Select(i => materials[i]).ToArray();
            Debug.Log($"[Cartheim] Tarp cloth: parts {string.Join(", ", keep)} kept, spanning {cloth.bounds.min} to {cloth.bounds.max}");
            return (_cloth, _clothMaterials);
        }

        // Whether the cloth's mesh is lower along its +x end than its -x end (when it can be read).
        private static bool LowerAtMaxX(Mesh mesh)
        {
            if (!mesh.isReadable)
                return false;
            var vertices = mesh.vertices;
            var b = mesh.bounds;
            var nearMin = vertices.Where(v => v.x < b.min.x + b.size.x * 0.2f).Select(v => v.y).DefaultIfEmpty(0f).Max();
            var nearMax = vertices.Where(v => v.x > b.max.x - b.size.x * 0.2f).Select(v => v.y).DefaultIfEmpty(0f).Max();
            return nearMax < nearMin;
        }

        // Where a player getting up stands: on the rug, or with none, in the middle of the tent's floor.
        public static Vector3 Middle(GameObject tent) => tent.transform.TransformPoint(tent.GetComponent<TentShape>().WakeAt);

        // Whether a point (a player's feet, a fire) is under a pitched tent. Its floor follows the ground's average, so a
        // point on lower ground than that still counts.
        public static bool Under(GameObject tent, Vector3 point, float maxRise)
        {
            var shape = tent ? tent.GetComponent<TentShape>() : null;
            if (!shape)
                return false;
            var footprint = shape.Footprint;
            var local = tent.transform.InverseTransformPoint(point) - new Vector3(footprint.center.x, 0f, footprint.center.z);
            return Cartheim.Core.Tent.Under(local.x, local.y + maxRise, local.z, footprint.extents.x, footprint.extents.z, footprint.max.y + maxRise);
        }
    }

    // What a pitched tent covers, in its own space.
    internal class TentShape : MonoBehaviour
    {
        public Bounds Footprint;
        public Vector3 WakeAt;
    }
}
