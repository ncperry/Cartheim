using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cartheim.Core;
using UnityEngine;

namespace Cartheim.Plugin
{
    // Sits on every cart: keeps its upgrades in the cart's save data, and gives the cart the stations and health they add.
    internal class CartBase : MonoBehaviour
    {
        private const string SaveKey = "cartheim_upgrades";
        private const string AddRpc = "Cartheim_Add";
        private const string ChestRpc = "Cartheim_SetChest";

        // Close enough to use a cart station from anywhere beside the cart.
        private const float UseDistance = 5f;

        public static readonly List<CartBase> All = new List<CartBase>();

        private ZNetView _nview;
        private WearNTear _wear;
        private float _ownHealth;
        private readonly Dictionary<string, CraftingStation> _stations = new Dictionary<string, CraftingStation>();
        // The containers built into the cart (its chest, barrel, wardrobe), by piece, once loaded.
        private readonly Dictionary<string, Container> _containers = new Dictionary<string, Container>();

        public CartUpgrades Upgrades { get; private set; } = new CartUpgrades();

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _wear = GetComponent<WearNTear>();
            if (_wear)
            {
                _ownHealth = _wear.m_health;
                // Runs on the cart's owner as it breaks, before the cart is gone. Not gated on the mod switch: what was paid
                // for comes back either way.
                _wear.m_onDestroyed += DropRefund;
            }
            // An old copy from before a hot reload may not have let go of it yet.
            _nview.Unregister(AddRpc);
            _nview.Unregister(ChestRpc);
            _nview.Register<string, string>(AddRpc, RPC_Add);
            _nview.Register<string, ZDOID>(ChestRpc, RPC_SetChest);
            All.Add(this);
            InvokeRepeating(nameof(Refresh), 0.5f, 1f);
            Debug.Log($"[Cartheim] Cart {_nview.GetZDO()?.m_uid} ready; health {_ownHealth}, has WearNTear {(bool)_wear}");
        }

        private void OnDestroy()
        {
            All.Remove(this);
            // A hot reload makes a new CartBase on the same cart, which registers the same RPC again.
            if (_nview)
                _nview.Unregister(AddRpc);
            if (_nview)
                _nview.Unregister(ChestRpc);
            if (_wear)
            {
                _wear.m_onDestroyed -= DropRefund;
                // Leave the next copy (after a hot reload) the cart's own health to start from.
                _wear.m_health = _ownHealth;
            }
            foreach (var station in _stations.Values)
                if (station)
                    Destroy(station.gameObject);
        }

        private void DropRefund()
        {
            if (!_nview || !_nview.IsValid() || !_nview.IsOwner())
                return;
            var refund = Load().Refund(GamePieces.Recipe);
            Debug.Log($"[Cartheim] Cart {_nview.GetZDO().m_uid} destroyed; dropping {string.Join(", ", refund)}");
            foreach (var item in refund)
                GamePieces.Drop(item, transform.position);
            foreach (var container in _containers.Values.Where(c => c))
                CartChest.Break(container, transform.position);
        }

        // The containers built into the cart, in panel order, while the mod is on and they are loaded.
        public List<Container> Containers =>
            Plugin.Enabled.Value
                ? GamePieces.Containers.Where(p => _containers.TryGetValue(p, out var c) && c).Select(p => _containers[p]).ToList()
                : new List<Container>();

        public void BuildContainer(string piece)
        {
            var id = CartChest.Build(piece, transform, _nview.GetZDO().m_uid);
            _nview.InvokeRPC(ChestRpc, piece, id);
        }

        private void RPC_SetChest(long sender, string piece, ZDOID chest)
        {
            if (_nview.IsOwner())
                _nview.GetZDO().Set(CartChest.KeyFor(piece), chest);
        }

        private void Update()
        {
            foreach (var container in _containers.Values)
                if (container)
                    CartChest.Follow(container, transform);
        }

        public CraftingStation Station(string piece) => _stations.TryGetValue(piece, out var s) ? s : null;

        public IEnumerable<string> Stations => _stations.Keys;

        // Pay first, then ask the cart's owner to record it; everyone picks it up from the save data within a second.
        public void Add(string piece, string extends)
        {
            Debug.Log($"[Cartheim] Adding {piece} (extends {extends ?? "nothing"}) to cart {_nview.GetZDO().m_uid}");
            _nview.InvokeRPC(AddRpc, piece, extends ?? "");
            Upgrades.Add(piece, extends);
            Refresh();
        }

        private void RPC_Add(long sender, string piece, string extends)
        {
            if (!_nview.IsOwner())
                return;
            var upgrades = Load();
            upgrades.Add(piece, extends == "" ? null : extends);
            _nview.GetZDO().Set(SaveKey, upgrades.Save());
            Debug.Log($"[Cartheim] Cart {_nview.GetZDO().m_uid} now has: {upgrades.Save().Replace("\n", ", ")}");
        }

        private CartUpgrades Load() => CartUpgrades.Load(_nview.GetZDO().GetString(SaveKey, ""));

        private void Refresh()
        {
            if (!_nview || !_nview.IsValid())
                return;
            Upgrades = Load();
            FindContainers();
            var on = Plugin.Enabled.Value;
            if (_wear)
                _wear.m_health = on ? Upgrades.Health(_ownHealth) : _ownHealth;
            foreach (var piece in GamePieces.Stations)
            {
                var wanted = on && Upgrades.Level(piece) > 0 && GamePieces.Prefab(piece)?.GetComponent<CraftingStation>();
                if (wanted && !_stations.ContainsKey(piece))
                    _stations[piece] = MakeStation(piece);
                else if (!wanted && _stations.TryGetValue(piece, out var station))
                {
                    _stations.Remove(piece);
                    if (station)
                        Destroy(station.gameObject);
                }
            }
        }

        private void FindContainers()
        {
            foreach (var piece in GamePieces.Containers)
            {
                if (_containers.TryGetValue(piece, out var known) && known)
                    continue;
                var id = _nview.GetZDO().GetZDOID(CartChest.KeyFor(piece));
                var go = id == ZDOID.None ? null : ZNetScene.instance.FindInstance(id);
                if (!go)
                    continue;
                CartChest.Hide(go);
                _containers[piece] = go.GetComponent<Container>();
                Debug.Log($"[Cartheim] Cart {_nview.GetZDO().m_uid} carries {piece} {id}");
            }
        }

        // A copy of the real station's crafting settings, riding on the cart. It has no model, collider or area marker of its
        // own, and needs neither roof nor fire.
        private CraftingStation MakeStation(string piece)
        {
            var original = GamePieces.Prefab(piece).GetComponent<CraftingStation>();
            var holder = new GameObject("Cartheim " + piece);
            holder.transform.SetParent(transform, false);
            var station = holder.AddComponent<CraftingStation>();
            foreach (var field in typeof(CraftingStation).GetFields(BindingFlags.Public | BindingFlags.Instance))
                field.SetValue(station, field.GetValue(original));
            station.m_craftRequireRoof = false;
            // A station that needs fire (the cauldron) gets it from a fire near the cart instead; see CartPatches.
            station.m_useDistance = UseDistance;
            station.m_roofCheckPoint = holder.transform;
            station.m_connectionPoint = null;
            station.m_areaMarker = null;
            station.m_inUseObject = null;
            station.m_haveFireObject = null;
            station.m_effectAreaCollider = null;
            holder.AddComponent<CartStation>().Init(this, piece);
            Debug.Log($"[Cartheim] Cart {_nview.GetZDO().m_uid} is now a {original.m_name} (level {Upgrades.Level(piece)})");
            return station;
        }
    }

    // Marks a crafting station as one riding on a cart, so its level comes from the cart's upgrades.
    internal class CartStation : MonoBehaviour
    {
        public CartBase Cart { get; private set; }
        public string Piece { get; private set; }

        public void Init(CartBase cart, string piece)
        {
            Cart = cart;
            Piece = piece;
        }
    }
}
