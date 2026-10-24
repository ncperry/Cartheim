using System.Linq;
using Cartheim.Core;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cartheim.Plugin
{
    // Shift+E on a cart: use the stations built into it, or build in another upgrade.
    internal class CartPanel
    {
        private const float Width = 620f;
        private const float Height = 520f;
        private const float NameWidth = 150f;
        private const float CostWidth = 290f;
        private const float ButtonWidth = 110f;

        // Leave the panel when the player walks this far from the cart.
        private const float MaxDistance = 6f;

        private GameObject _panel;
        private ScrollRect _scroll;
        private Text _status;
        private CartBase _cart;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        public void Open(CartBase cart)
        {
            if (GUIManager.CustomGUIFront == null)
                return;
            EnsureCreated();
            _cart = cart;
            _status.text = "";
            Redraw();
            if (!IsOpen)
                GUIManager.BlockInput(true);
            _panel.SetActive(true);
            _panel.transform.SetAsLastSibling();
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            _panel.SetActive(false);
            GUIManager.BlockInput(false);
        }

        public void Update()
        {
            if (!IsOpen)
                return;
            var player = Player.m_localPlayer;
            if (!Plugin.Enabled.Value || !_cart || !player ||
                Vector3.Distance(player.transform.position, _cart.transform.position) > MaxDistance ||
                BepInEx.UnityInput.Current.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }
            PanelWidgets.ScrollByWheel(_scroll);
        }

        public void Destroy()
        {
            Close();
            if (_panel != null)
                Object.Destroy(_panel);
            _panel = null;
        }

        private void Redraw()
        {
            var player = Player.m_localPlayer;
            var list = _scroll.content;
            PanelWidgets.Clear(list);

            var stations = _cart.Stations.ToList();
            if (stations.Count > 0)
            {
                PanelWidgets.Header(list, "Stations", Width - 90);
                foreach (var piece in stations)
                {
                    var row = PanelWidgets.Row(list);
                    var station = _cart.Station(piece);
                    var level = _cart.Upgrades.Level(piece);
                    PanelWidgets.Size(PanelWidgets.Label(row, GamePieces.DisplayName(piece), Vector2.zero, 16, Color.white, NameWidth, TextAnchor.MiddleLeft), NameWidth);
                    PanelWidgets.Size(PanelWidgets.Label(row, $"Level {level}", Vector2.zero, 16, Color.white, CostWidth, TextAnchor.MiddleLeft), CostWidth);
                    PanelWidgets.Size(PanelWidgets.Button(row, "Use", Vector2.zero, ButtonWidth, () => Use(station)), ButtonWidth);
                }
            }

            PanelWidgets.Header(list, "Upgrades", Width - 90);
            foreach (var piece in GamePieces.Upgrades)
            {
                var extends = GamePieces.Extends(piece);
                var cost = UpgradeCost.For(GamePieces.Recipe(piece));
                var state = _cart.Upgrades.StateOf(piece, extends, cost, GamePieces.Carried(player, cost));
                var row = PanelWidgets.Row(list);
                var costText = string.Join(", ", cost.Select(c => $"{c.Count} {GamePieces.ItemName(c.Prefab)}"));
                PanelWidgets.Size(PanelWidgets.Label(row, GamePieces.DisplayName(piece), Vector2.zero, 16, Color.white, NameWidth, TextAnchor.MiddleLeft), NameWidth);
                PanelWidgets.Size(PanelWidgets.Label(row, costText, Vector2.zero, 14, Color.white, CostWidth, TextAnchor.MiddleLeft), CostWidth);
                var button = PanelWidgets.Size(PanelWidgets.Button(row, ButtonText(state), Vector2.zero, ButtonWidth, () => Build(piece, extends)), ButtonWidth);
                button.GetComponent<Button>().interactable = state == UpgradeState.Ready || (state == UpgradeState.CannotAfford && player.NoCostCheat());
            }
        }

        private static string ButtonText(UpgradeState state) =>
            state == UpgradeState.Built ? "Built" :
            state == UpgradeState.Locked ? "Locked" :
            state == UpgradeState.CannotAfford ? "Need more" :
            "Build";

        private void Build(string piece, string extends)
        {
            var player = Player.m_localPlayer;
            var cost = UpgradeCost.For(GamePieces.Recipe(piece));
            var state = _cart.Upgrades.StateOf(piece, extends, cost, GamePieces.Carried(player, cost));
            if (state != UpgradeState.Ready && !(state == UpgradeState.CannotAfford && player.NoCostCheat()))
            {
                _status.text = "<color=#ff6060>Can't build that now</color>";
                return;
            }
            if (!player.NoCostCheat())
                GamePieces.Take(player, cost);
            _cart.Add(piece, extends);
            _status.text = $"Built {GamePieces.DisplayName(piece)} into the cart";
            Redraw();
        }

        private void Use(CraftingStation station)
        {
            var player = Player.m_localPlayer;
            if (!station || !player)
                return;
            Close();
            player.SetCraftingStation(station);
            InventoryGui.instance.Show(null, 3);
        }

        private void EnsureCreated()
        {
            if (_panel != null)
                return;
            var middle = new Vector2(0.5f, 0.5f);
            _panel = GUIManager.Instance.CreateWoodpanel(GUIManager.CustomGUIFront.transform, middle, middle, Vector2.zero, Width, Height, draggable: true);
            PanelWidgets.Label(_panel.transform, "Cart", new Vector2(0, Height / 2 - 30), 22, GUIManager.Instance.ValheimOrange, Width - 40);
            _scroll = PanelWidgets.ScrollList(_panel.transform, Width - 40, Height - 150, 10);
            _status = PanelWidgets.Label(_panel.transform, "", new Vector2(0, -Height / 2 + 60), 15, Color.white, Width - 40).GetComponent<Text>();
            PanelWidgets.Button(_panel.transform, "Close", new Vector2(0, -Height / 2 + 30), 120, Close);
            _panel.SetActive(false);
        }
    }
}
