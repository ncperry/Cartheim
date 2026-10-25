using System.Collections.Generic;
using HarmonyLib;
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
        private const string AddCoalRpc = "Cartheim_AddCoal";
        private const string TentRpc = "Cartheim_Tent";
        // Whether the cart's tent is pitched.
        private const string TentKey = "cartheim_tent_up";
        // The coal in the steam engine, and when it last burnt.
        private const string CoalKey = "cartheim_coal";
        private const string CoalTimeKey = "cartheim_coal_time";
        // How hard the engine pushes the cart towards the player's pace: the share of the gap closed per second, at full assist.
        private const float PushGain = 10f;
        // No longer used (riders find the cart by its id); unregistered in case a copy from before a hot reload has it.
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
        private CartLooks _looks;
        private string _looksKey = "";
        private GameObject _hearth;
        private GameObject _wisp;
        private GameObject _baseArea;
        private Rigidbody[] _bodies = new Rigidbody[0];
        // How much of the pull the steam engine takes off the player right now (0 to 1).
        private float _assist;
        // The tent pitched beside the cart, and how each of the cart's bodies was held before the tent held it still.
        private GameObject _tent;
        private string _tentRug;
        private readonly Dictionary<Rigidbody, RigidbodyConstraints> _unfrozen = new Dictionary<Rigidbody, RigidbodyConstraints>();

        public CartUpgrades Upgrades { get; private set; } = new CartUpgrades();

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            _wear = GetComponent<WearNTear>();
            _vagon = GetComponent<Vagon>();
            // Already awake before this ran (a hot reload): its storage may not be doubled yet.
            if (_vagon && _vagon.m_container)
                CartStorage.Double(_vagon.m_container);
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
            _nview.Unregister(AddCoalRpc);
            _nview.Register(AddCoalRpc, RPC_AddCoal);
            _nview.Unregister(TentRpc);
            _nview.Register<bool>(TentRpc, RPC_Tent);
            _bodies = GetComponentsInChildren<Rigidbody>();
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
            if (_nview)
                _nview.Unregister(AddCoalRpc);
            if (_nview)
                _nview.Unregister(TentRpc);
            if (_tent)
                Destroy(_tent);
            Freeze(false);
            if (_wear)
            {
                _wear.m_onDestroyed -= DropRefund;
                // Leave the next copy (after a hot reload) the cart's own health to start from.
                _wear.m_health = _ownHealth;
            }
            foreach (var station in _stations.Values)
                if (station)
                    Destroy(station.gameObject);
            _looks?.Destroy();
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
        public bool HasLooks => Plugin.Enabled.Value && _looks != null && _looks.Root;

        private void DropRefund()
        {
            if (!_nview || !_nview.IsValid() || !_nview.IsOwner())
                return;
            var refund = Load().Refund(GamePieces.Recipe);
            Debug.Log($"[Cartheim] Cart {_nview.GetZDO().m_uid} destroyed; dropping {string.Join(", ", refund)}");
            foreach (var item in refund)
                GamePieces.Drop(item, transform.position);
            // Coal not yet burnt in the steam engine spills too.
            var coal = Mathf.FloorToInt(Coal);
            if (coal > 0)
            {
                Debug.Log($"[Cartheim] Cart {Uid()}'s steam engine spills {coal} coal");
                GamePieces.Drop(new ItemCount(SteamEngine.Coal, coal), transform.position);
            }
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

        // Builds the piece into its first free slot (a second beehive goes in the second), marked with the cart's id.
        public void BuildRider(string piece)
        {
            EnsureUid();
            var uid = Uid();
            if (uid == 0)
            {
                Debug.LogWarning("[Cartheim] This cart has no id yet (its owner gives it one); can't build a riding piece into it");
                return;
            }
            var slot = GamePieces.SlotsFor(piece).FirstOrDefault(s => !Rider(s)) ?? piece;
            CartRiders.Build(piece, transform, _nview.GetZDO().m_uid, uid, slot);
        }

        private void Update()
        {
            foreach (var (slot, rider) in _riders)
                if (rider)
                    CartRiders.Follow(rider, transform, AnchorFor(slot));
            KeepSpawnPointOnBed();
            if (_wisp)
                CartWisp.Float(_wisp, transform);
            // The engine's fire burns in its firebox.
            var engine = AnchorFor(SteamEngine.Engine);
            if (_hearth && engine)
                _hearth.transform.position = engine.position + Vector3.up * EngineFireHeight;
        }

        private const float EngineFireHeight = 0.1f;

        public bool HasTent => Plugin.Enabled.Value && Upgrades.Has(Tent.Piece);

        // Pitched beside the cart; the cart stays put (and can't be hitched) until it's packed up.
        public bool TentUp => HasTent && _nview && _nview.IsValid() && _nview.GetZDO().GetBool(TentKey);

        // Where a player getting up should stand: under the pitched tent.
        public Vector3? TentFloor => _tent ? TentAssets.Middle(_tent) : (Vector3?)null;

        // Whether a point (a player, a fire) is under the cart's pitched tent.
        public bool Shelters(Vector3 point) => _tent && TentAssets.Under(_tent, point, Plugin.TentMaxRise.Value);

        // The tent button: packs the tent up, or pitches it where the ground beside the cart is flat enough.
        public void ToggleTent(Player player)
        {
            if (!HasTent)
                return;
            if (TentUp)
            {
                _nview.InvokeRPC(TentRpc, false);
                player.Message(MessageHud.MessageType.Center, "Packed up the tent");
                Debug.Log($"[Cartheim] Cart {Uid()}'s tent packed up");
                return;
            }
            if (_vagon && _vagon.IsAttached())
            {
                player.Message(MessageHud.MessageType.Center, "Unhitch the cart first");
                return;
            }
            if (!_vagon || !TentAssets.Spot(transform, CartLooks.BedOf(_vagon), out var at, out _, out var heights))
            {
                player.Message(MessageHud.MessageType.Center, "There's no tent to pitch");
                return;
            }
            var rise = heights.Max() - heights.Min();
            Debug.Log($"[Cartheim] Pitching cart {Uid()}'s tent at {at}: the ground rises {rise:0.00} m (at most {Plugin.TentMaxRise.Value} m)");
            if (!Tent.FlatEnough(heights, Plugin.TentMaxRise.Value))
            {
                player.Message(MessageHud.MessageType.Center, $"The ground beside the cart is too uneven for the tent ({rise:0.0} m from high to low)");
                return;
            }
            _nview.InvokeRPC(TentRpc, true);
            player.Message(MessageHud.MessageType.Center, "Pitched the tent");
        }

        private void RPC_Tent(long sender, bool up)
        {
            if (!_nview.IsOwner())
                return;
            _nview.GetZDO().Set(TentKey, up);
            Refresh();
        }

        // Held still (while the tent is pitched): every part of the cart frozen where it is; let go, each as it was.
        private void Freeze(bool still)
        {
            foreach (var body in _bodies)
            {
                if (!body)
                    continue;
                if (still && !_unfrozen.ContainsKey(body))
                {
                    _unfrozen[body] = body.constraints;
                    body.constraints = RigidbodyConstraints.FreezeAll;
                }
                else if (!still && _unfrozen.TryGetValue(body, out var was))
                {
                    body.constraints = was;
                    body.WakeUp();
                }
            }
            if (!still)
                _unfrozen.Clear();
        }

        // The coal in the steam engine, as its owner keeps it.
        public float Coal => _nview && _nview.IsValid() ? _nview.GetZDO().GetFloat(CoalKey) : 0f;

        public bool EngineBurning => Plugin.Enabled.Value && Upgrades.Has(SteamEngine.Engine) && Coal > 0f;

        // As long to burn a coal as the game's hearth takes over a log.
        private static float SecondsPerCoal
        {
            get
            {
                var hearth = GamePieces.Prefab("hearth")?.GetComponent<Fireplace>();
                return hearth && hearth.m_secPerFuel > 0f ? hearth.m_secPerFuel : 5000f;
            }
        }

        // One coal from the player's inventory into the engine, if it has room and they have one.
        public bool AddCoal(Player player)
        {
            if (!Upgrades.Has(SteamEngine.Engine))
                return false;
            // Said as at any fire.
            var coal = GamePieces.Item(SteamEngine.Coal);
            var name = coal ? coal.m_itemData.m_shared.m_name : SteamEngine.Coal;
            if (!coal || !player.GetInventory().HaveItem(name))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_outof " + name);
                return false;
            }
            if (!SteamEngine.HasRoomFor(Coal))
            {
                player.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_cantaddmore", name));
                return false;
            }
            player.GetInventory().RemoveItem(name, 1);
            _nview.InvokeRPC(AddCoalRpc);
            player.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_fireadding", name));
            return true;
        }

        private void RPC_AddCoal(long sender)
        {
            if (!_nview.IsOwner())
                return;
            var zdo = _nview.GetZDO();
            var coal = zdo.GetFloat(CoalKey);
            // Nowhere to put it (another player filled it first): it isn't lost, it drops by the cart.
            if (!SteamEngine.HasRoomFor(coal))
            {
                GamePieces.Drop(new ItemCount(SteamEngine.Coal, 1), transform.position);
                return;
            }
            if (coal <= 0f)
                zdo.Set(CoalTimeKey, ZNet.instance.GetTime().Ticks);
            zdo.Set(CoalKey, coal + 1f);
            Debug.Log($"[Cartheim] Cart {Uid()}'s steam engine now has {coal + 1f:0.00} coal");
            Refresh();
        }

        // The cart's owner burns the engine's coal for the time gone by, as a hearth burns its wood (even while nobody was
        // near, as the game's own fires do). While the mod is off it neither burns nor counts the time.
        private void BurnCoal(bool on)
        {
            if (!_nview.IsOwner() || !Upgrades.Has(SteamEngine.Engine) || !ZNet.instance)
                return;
            var zdo = _nview.GetZDO();
            var now = ZNet.instance.GetTime();
            var last = zdo.GetLong(CoalTimeKey);
            zdo.Set(CoalTimeKey, now.Ticks);
            var coal = zdo.GetFloat(CoalKey);
            if (!on || last == 0 || coal <= 0f)
                return;
            var seconds = (now - new System.DateTime(last)).TotalSeconds;
            if (seconds <= 0)
                return;
            var left = SteamEngine.Burn(coal, seconds, SecondsPerCoal);
            zdo.Set(CoalKey, left);
            if (Mathf.CeilToInt(left) != Mathf.CeilToInt(coal))
                Debug.Log($"[Cartheim] Cart {Uid()}'s steam engine burnt down to {left:0.00} coal ({SecondsPerCoal} s a coal)");
        }

        private float _nextPushLog;

        // While the local player pulls the cart, the engine pushes it along at their pace, so the cart drags at them less
        // (not at all, fully upgraded). Only sideways and forwards: the cart still rolls down and up hills under its weight.
        private void FixedUpdate()
        {
            if (_assist <= 0f || !_vagon || !_nview || !_nview.IsValid() || !_nview.IsOwner())
                return;
            var player = Player.m_localPlayer;
            if (!player || !_vagon.IsAttached(player))
                return;
            var pace = player.GetVelocity();
            foreach (var body in _bodies)
            {
                if (!body)
                    continue;
                var gap = pace - body.linearVelocity;
                gap.y = 0f;
                body.AddForce(gap * (_assist * PushGain), ForceMode.Acceleration);
            }
            if (Time.time >= _nextPushLog && pace.sqrMagnitude > 0.5f)
            {
                _nextPushLog = Time.time + 5f;
                Debug.Log($"[Cartheim] Steam engine pushing at {_assist:0.00}: player at {new Vector3(pace.x, 0f, pace.z).magnitude:0.00} m/s, " +
                          $"cart at {(_bodies.Length > 0 && _bodies[0] ? new Vector3(_bodies[0].linearVelocity.x, 0f, _bodies[0].linearVelocity.z).magnitude : 0f):0.00} m/s");
            }
        }

        // Where a riding piece shows on the cart, by its slot (each hive has its own) or else its piece.
        private Transform AnchorFor(string slot)
        {
            if (_looks == null)
                return null;
            return _looks.Anchors.TryGetValue(slot, out var anchor) || _looks.Anchors.TryGetValue(GamePieces.PieceIn(slot), out anchor) ? anchor : null;
        }

        // The fermenter riding in the cart, while the mod is on and it is loaded.
        public Fermenter Fermenter
        {
            get
            {
                var rider = Plugin.Enabled.Value ? Rider(GamePieces.Fermenter) : null;
                return rider ? rider.GetComponent<Fermenter>() : null;
            }
        }

        // The fire riding as a piece (the sconce), while the mod is on and it is loaded.
        public Fireplace FireIn(string piece)
        {
            var rider = Plugin.Enabled.Value ? Rider(piece) : null;
            return rider ? rider.GetComponentInChildren<Fireplace>() : null;
        }

        // The beehive riding in a slot, while the mod is on and it is loaded.
        public Beehive BeehiveIn(string slot)
        {
            var rider = Plugin.Enabled.Value ? Rider(slot) : null;
            return rider ? rider.GetComponent<Beehive>() : null;
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
            EnsureUid();
            // Keeps the cart's own storage growing, whatever has resized it since.
            if (_vagon && _vagon.m_container)
                CartStorage.Double(_vagon.m_container);
            FindRiders();
            var on = Plugin.Enabled.Value;
            foreach (var (slot, rider) in _riders)
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
            var wantTent = on && TentUp;
            var rug = GamePieces.Rugs.FirstOrDefault(Upgrades.Has);
            // A rug built while the tent is up goes down when it's pitched again, here and now.
            if (_tent && rug != _tentRug)
            {
                Destroy(_tent);
                _tent = null;
            }
            if (wantTent && !_tent && _vagon && TentAssets.Spot(transform, CartLooks.BedOf(_vagon), out var tentAt, out var tentFacing, out _))
            {
                _tent = TentAssets.Pitch(tentAt, tentFacing, CartLooks.BedOf(_vagon), rug);
                _tentRug = rug;
            }
            else if (!wantTent && _tent)
            {
                Destroy(_tent);
                _tent = null;
            }
            Freeze(wantTent);
            BurnCoal(on);
            _assist = on ? SteamEngine.Assist(Upgrades.Level(SteamEngine.Engine), Coal) : 0f;
            // A hearth built before the steam engine replaced it still burns; the engine burns while it has coal.
            var wantHearth = on && (Upgrades.HasHearth || EngineBurning);
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
                _looks?.Destroy();
                _looks = wantLooks.Count > 0 && _vagon ? new CartLooks(this, _vagon, wantLooks) : null;
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

        private static readonly AccessTools.FieldRef<ZNetScene, Dictionary<ZDO, ZNetView>> Instances =
            AccessTools.FieldRefAccess<ZNetScene, Dictionary<ZDO, ZNetView>>("m_instances");

        // How near the cart a piece built before riders carried the cart's id must be for the cart to claim it.
        private const float ClaimDistance = 4f;

        // The slots the cart's upgrades fill with riding pieces: one per rider piece built, the second hive's own slot.
        private List<string> ExpectedSlots() =>
            GamePieces.Riders.Where(slot => Upgrades.Count(GamePieces.PieceIn(slot)) > (slot.Contains('#') ? 1 : 0)).ToList();

        // Finds the pieces riding in the cart by the cart's own id, which (unlike the game's object ids, renumbered on every
        // world load) stays the same for good. Pieces built before that, carrying only an old object id, are claimed by
        // the cart they sit in and given its id.
        private void FindRiders()
        {
            var missing = ExpectedSlots().Where(slot => !Rider(slot)).ToList();
            if (missing.Count == 0 || !ZNetScene.instance)
                return;
            var uid = Uid();
            foreach (var pair in Instances(ZNetScene.instance).ToList())
            {
                var zdo = pair.Key;
                var view = pair.Value;
                if (zdo == null || !view || missing.Count == 0)
                    continue;
                var slot = zdo.GetString(CartRiders.SlotKey, "");
                var ridesHere = uid != 0 && zdo.GetLong(CartRiders.UidKey) == uid && missing.Contains(slot);
                if (!ridesHere)
                {
                    // A piece from before cart ids: still marked as riding some cart, right where this one is.
                    if (zdo.GetLong(CartRiders.UidKey) != 0 || zdo.GetZDOID(CartRiders.CartKey) == ZDOID.None ||
                        Vector3.Distance(zdo.GetPosition(), transform.position) > ClaimDistance)
                        continue;
                    var prefab = Utils.GetPrefabName(view.gameObject);
                    slot = missing.FirstOrDefault(s => GamePieces.PieceIn(s) == prefab);
                    if (slot == null || uid == 0)
                        continue;
                    view.ClaimOwnership();
                    zdo.Set(CartRiders.UidKey, uid);
                    zdo.Set(CartRiders.SlotKey, slot);
                    Debug.Log($"[Cartheim] Cart {uid} claims its {slot} from before cart ids");
                }
                var go = view.gameObject;
                CartRiders.Hide(go);
                var container = go.GetComponent<Container>();
                if (container)
                    CartStorage.Double(container);
                _riders[slot] = go;
                missing.Remove(slot);
                Debug.Log($"[Cartheim] Cart {uid} carries {slot} {zdo.m_uid}");
            }
        }

        private const string UidKey = "cartheim_uid";

        // The cart's own id, kept in its save data; 0 until its owner has given it one.
        private long Uid() => _nview && _nview.IsValid() ? _nview.GetZDO().GetLong(UidKey) : 0;

        private void EnsureUid()
        {
            if (Uid() != 0 || !_nview.IsOwner())
                return;
            var uid = ((long)Random.Range(1, int.MaxValue) << 31) | (long)Random.Range(1, int.MaxValue);
            _nview.GetZDO().Set(UidKey, uid);
            Debug.Log($"[Cartheim] Cart {_nview.GetZDO().m_uid} now has id {uid}");
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
