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

        // How far a cart bed may have moved since the last frame for the spawn point to still follow it.
        private const float SpawnPointDrift = 3f;

        public static readonly List<CartBase> All = new List<CartBase>();

        private ZNetView _nview;
        private WearNTear _wear;
        private float _ownHealth;
        private readonly Dictionary<string, CraftingStation> _stations = new Dictionary<string, CraftingStation>();
        // The pieces riding hidden in the cart (chest, bed, rug, light...), by piece, once loaded.
        private readonly Dictionary<string, GameObject> _riders = new Dictionary<string, GameObject>();
        private CraftingStation _upgradeStation;
        private Vagon _vagon;
        // The small copies of built pieces standing in the cart, and which pieces they are.
        private GameObject _looks;
        private string _looksKey = "";
        private GameObject _hearth;
        private GameObject _wisp;
        private GameObject _baseArea;

        public CartUpgrades Upgrades { get; private set; } = new CartUpgrades();

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _wear = GetComponent<WearNTear>();
            _vagon = GetComponent<Vagon>();
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
            if (_looks)
                Destroy(_looks);
            if (_upgradeStation)
                Destroy(_upgradeStation.gameObject);
            if (_hearth)
                Destroy(_hearth);
            if (_wisp)
                Destroy(_wisp);
            if (_baseArea)
                Destroy(_baseArea);
        }

        // What the cart offers right now: nothing while the mod is switched off.
        public bool HasLitHearth => Plugin.Enabled.Value && _hearth;
        public bool HasRoof => Plugin.Enabled.Value && Upgrades.RoofLevel > 0;

        // Whether the cart shows any built piece as itself, in place of the game's crates and sacks.
        public bool HasLooks => Plugin.Enabled.Value && _looks;

        private void DropRefund()
        {
            if (!_nview || !_nview.IsValid() || !_nview.IsOwner())
                return;
            var refund = Load().Refund(GamePieces.Recipe);
            Debug.Log($"[Cartheim] Cart {_nview.GetZDO().m_uid} destroyed; dropping {string.Join(", ", refund)}");
            foreach (var item in refund)
                GamePieces.Drop(item, transform.position);
            foreach (var rider in _riders.Values.Where(r => r).ToList())
                CartRiders.Break(rider, transform.position);
        }

        // The containers built into the cart, in panel order, while the mod is on and they are loaded.
        public List<Container> Containers =>
            Plugin.Enabled.Value
                ? GamePieces.Containers.Select(Rider).Where(r => r).Select(r => r.GetComponent<Container>()).Where(c => c).ToList()
                : new List<Container>();

        // The bed built into the cart, while the mod is on and it is loaded.
        public Bed Bed => Plugin.Enabled.Value ? GamePieces.Beds.Select(Rider).Where(r => r).Select(r => r.GetComponent<Bed>()).FirstOrDefault(b => b) : null;

        private GameObject Rider(string piece) => _riders.TryGetValue(piece, out var r) && r ? r : null;

        // The beehives built into the cart, while the mod is on and they are loaded.
        public List<Beehive> Beehives =>
            Plugin.Enabled.Value
                ? GamePieces.BeehiveSlots.Select(Rider).Where(r => r).Select(r => r.GetComponent<Beehive>()).Where(b => b).ToList()
                : new List<Beehive>();

        // Builds the piece into its first free slot (a second beehive goes in the second).
        public void BuildRider(string piece)
        {
            var slot = GamePieces.SlotsFor(piece).FirstOrDefault(s => !Rider(s) && _nview.GetZDO().GetZDOID(CartRiders.KeyFor(s)) == ZDOID.None) ?? piece;
            var id = CartRiders.Build(piece, transform, _nview.GetZDO().m_uid);
            _nview.InvokeRPC(ChestRpc, slot, id);
        }

        private void RPC_SetChest(long sender, string piece, ZDOID chest)
        {
            if (_nview.IsOwner())
                _nview.GetZDO().Set(CartRiders.KeyFor(piece), chest);
        }

        private void Update()
        {
            foreach (var rider in _riders.Values)
                if (rider)
                    CartRiders.Follow(rider, transform);
            KeepSpawnPointOnBed();
            if (_wisp)
                CartWisp.Float(_wisp, transform);
        }

        public CraftingStation Station(string piece) => _stations.TryGetValue(piece, out var s) ? s : null;

        // The real stations built in, in the hammer's order.
        public IEnumerable<string> Stations => GamePieces.Stations.Where(_stations.ContainsKey);

        // The Cart tab's station, where the cart's upgrades are built; there whenever the mod is on.
        public CraftingStation UpgradeStation => Plugin.Enabled.Value ? _upgradeStation : null;

        // The cart's health now, and at most.
        public float Health => _nview && _nview.IsValid() && _wear ? _nview.GetZDO().GetFloat(ZDOVars.s_health, _wear.m_health) : 0f;
        public float MaxHealth => _wear ? _wear.m_health : 0f;

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
            FindRiders();
            var on = Plugin.Enabled.Value;
            foreach (var rider in _riders.Values)
                if (rider)
                    CartRiders.SetWorking(rider, on);
            if (_wear)
                _wear.m_health = on ? Upgrades.Health(_ownHealth) : _ownHealth;
            if (on && !_baseArea)
                _baseArea = CartBaseArea.Make(transform);
            else if (!on && _baseArea)
            {
                Destroy(_baseArea);
                _baseArea = null;
            }
            var wantWisp = on && Upgrades.Has(GamePieces.WispTorch);
            if (wantWisp && !_wisp)
                _wisp = CartWisp.Make(transform);
            else if (!wantWisp && _wisp)
            {
                Destroy(_wisp);
                _wisp = null;
            }
            var wantHearth = on && Upgrades.HasHearth;
            if (wantHearth && !_hearth)
                _hearth = CartHearth.Light(transform);
            else if (!wantHearth && _hearth)
            {
                Destroy(_hearth);
                _hearth = null;
            }
            var wantLooks = on ? CartLooks.Shown(Upgrades) : new List<string>();
            var looksKey = string.Join(",", wantLooks);
            if (looksKey != _looksKey)
            {
                if (_looks)
                    Destroy(_looks);
                _looks = wantLooks.Count > 0 && _vagon ? CartLooks.Make(_vagon, wantLooks) : null;
                _looksKey = looksKey;
            }
            if (on && !_upgradeStation)
                _upgradeStation = MakeUpgradeStation();
            else if (!on && _upgradeStation)
            {
                Destroy(_upgradeStation.gameObject);
                _upgradeStation = null;
            }
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

        // A bed that is the local player's spawn point moves it along with the cart, so they wake up wherever the cart went.
        // Only while this player has the cart loaded: if it travels while they are far away, the game won't find the bed.
        private void KeepSpawnPointOnBed()
        {
            var bed = Bed;
            var profile = Game.instance ? Game.instance.GetPlayerProfile() : null;
            if (!bed || profile == null || !profile.HaveCustomSpawnPoint())
                return;
            if (bed.GetComponent<ZNetView>()?.GetZDO()?.GetLong(ZDOVars.s_owner) != profile.GetPlayerID())
                return;
            var spawn = bed.GetSpawnPoint();
            var distance = Vector3.Distance(profile.GetCustomSpawnPoint(), spawn);
            if (distance > 0.1f && distance < SpawnPointDrift)
                profile.SetCustomSpawnPoint(spawn);
        }

        private void FindRiders()
        {
            foreach (var piece in GamePieces.Riders)
            {
                if (_riders.TryGetValue(piece, out var known) && known)
                    continue;
                // piece is a slot here: a second beehive rides as "piece_beehive#2".
                var id = _nview.GetZDO().GetZDOID(CartRiders.KeyFor(piece));
                var go = id == ZDOID.None ? null : ZNetScene.instance.FindInstance(id);
                if (!go)
                    continue;
                CartRiders.Hide(go);
                _riders[piece] = go;
                Debug.Log($"[Cartheim] Cart {_nview.GetZDO().m_uid} carries {piece} {id}");
            }
        }

        // A copy of the real station's crafting settings, riding on the cart. It has no model, collider or area marker of its
        // own, and needs neither roof nor fire.
        private CraftingStation MakeStation(string piece) => MakeStation(piece, GamePieces.Prefab(piece).GetComponent<CraftingStation>());

        // The Cart tab's station: a workbench in all but name, whose only recipes are the cart's upgrades (see CartCrafting).
        private CraftingStation MakeUpgradeStation()
        {
            var workbench = GamePieces.Prefab(GamePieces.WorkbenchPiece)?.GetComponent<CraftingStation>();
            if (!workbench)
                return null;
            var station = MakeStation(CartCrafting.UpgradeStationPiece, workbench);
            station.m_name = "Cart";
            var icon = GamePieces.Icon("Cart");
            if (icon)
                station.m_icon = icon;
            station.m_showBasicRecipies = false;
            station.m_craftingSkill = Skills.SkillType.None;
            // It lends the cart no build range: only the real stations built in do.
            station.m_rangeBuild = 0f;
            station.m_extraRangePerLevel = 0f;
            return station;
        }

        private CraftingStation MakeStation(string piece, CraftingStation original)
        {
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
