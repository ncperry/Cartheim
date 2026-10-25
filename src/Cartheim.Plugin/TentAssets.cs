using System.Collections.Generic;
using System.Linq;
using Cartheim.Core;
using UnityEngine;

namespace Cartheim.Plugin
{
    // The cloth tent from Haldor's camp, pitched beside a cart: a copy of its mesh, materials and collider (so it stands
    // solid, and the game's own cover checks see a roof). The camp is a location, which the game loads only when it
    // places one; its prefab is loaded here once and held while the game runs, since the tent's mesh belongs to it.
    internal static class TentAssets
    {
        private const string Camp = "Vendor_BlackForest";
        private const string TentPart = "TraderTent";

        // Haldor's tent is big (8 m by 7); a cart's is this much of it.
        private const float Scale = 0.6f;
        // How far the tent's near wall stands from the cart's middle: clear of the workbench hanging off that side.
        private const float FromCart = 1.3f;
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

        // Where the tent would stand beside the cart: its floor's middle, turned with its long side along the cart, and
        // the ground heights under it.
        public static bool Spot(Transform cart, out Vector3 position, out Quaternion rotation, out float[] heights)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            heights = new float[0];
            var tent = Tent();
            if (!tent || !ZoneSystem.instance)
                return false;
            var size = tent.sharedMesh.bounds.size * Scale;
            var forward = Vector3.ProjectOnPlane(cart.forward, Vector3.up).normalized;
            var left = Vector3.Cross(forward, Vector3.up).normalized;
            // The tent's long side (its x) runs along the cart.
            rotation = Quaternion.LookRotation(left, Vector3.up);
            var middle = cart.position + left * (FromCart + size.z / 2f);
            var samples = new List<float>();
            for (var i = 0; i < Samples; i++)
            for (var j = 0; j < Samples; j++)
            {
                var along = (i / (Samples - 1f) - 0.5f) * size.x;
                var across = (j / (Samples - 1f) - 0.5f) * size.z;
                samples.Add(ZoneSystem.instance.GetGroundHeight(middle + forward * along + left * across));
            }
            heights = samples.ToArray();
            position = new Vector3(middle.x, Cartheim.Core.Tent.GroundLevel(heights), middle.z);
            return true;
        }

        // The tent standing at a spot: its floor's middle there.
        public static GameObject Pitch(Vector3 position, Quaternion rotation)
        {
            var tent = Tent();
            if (!tent)
                return null;
            var root = new GameObject("Cartheim tent");
            root.transform.SetPositionAndRotation(position, rotation);
            var model = new GameObject("model", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            model.layer = tent.gameObject.layer;
            model.transform.SetParent(root.transform, false);
            model.transform.localScale = Vector3.one * Scale;
            var bounds = tent.sharedMesh.bounds;
            // Its floor's middle at the root.
            model.transform.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * Scale;
            model.GetComponent<MeshFilter>().sharedMesh = tent.sharedMesh;
            model.GetComponent<MeshRenderer>().sharedMaterials = tent.GetComponent<MeshRenderer>().sharedMaterials;
            model.GetComponent<MeshCollider>().sharedMesh = tent.GetComponent<MeshCollider>() is MeshCollider collider && collider.sharedMesh
                ? collider.sharedMesh
                : tent.sharedMesh;
            Debug.Log($"[Cartheim] Tent pitched at {position}, {bounds.size * Scale} big");
            return root;
        }

        // Whether a point (a player's feet, a fire) is under a pitched tent. Its floor follows the ground's average, so a
        // point on lower ground than that still counts.
        public static bool Under(GameObject tent, Vector3 point, float maxRise)
        {
            var mesh = Tent();
            if (!tent || !mesh)
                return false;
            var size = mesh.sharedMesh.bounds.size * Scale;
            var local = tent.transform.InverseTransformPoint(point);
            return Cartheim.Core.Tent.Under(local.x, local.y + maxRise, local.z, size.x / 2f, size.z / 2f, size.y + maxRise);
        }
    }
}
