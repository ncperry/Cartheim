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
        // its z, either side of z = 0; its tall pole stands out at the other side. Pitched by a cart, that edge runs along
        // the cart's left side, the boards at the cart's corners, and the pole stands out away from the cart.
        private static readonly Vector3 Edge = new Vector3(0.48f, 0f, 0f);
        private const float Boards = 3.8f;

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
            root.AddComponent<TentShape>().Footprint = footprint;
            if (rug != null)
                CartLooks.Rug(root.transform, rug, new Bounds(new Vector3(-(RugFromCart + RugSize / 2f), 0f, 0f), new Vector3(RugSize, 0f, RugSize)));
            Debug.Log($"[Cartheim] Tent pitched at {position}, covering {footprint.min} to {footprint.max} from the middle of the cart's left side");
            return root;
        }

        // The middle of the tent's floor, where a player getting up stands.
        public static Vector3 Middle(GameObject tent)
        {
            var footprint = tent.GetComponent<TentShape>().Footprint;
            return tent.transform.TransformPoint(new Vector3(footprint.center.x, 0f, footprint.center.z));
        }

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
    }
}
