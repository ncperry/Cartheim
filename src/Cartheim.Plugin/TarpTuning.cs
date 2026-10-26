using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Cartheim.Core;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cartheim.Plugin
{
    // Tuning a ruin's tarp by hand, in a dev session (switched on in the config file, [Dev] Tarp tuning, and off again
    // after): its key opens a panel by the nearest settled cart with a tent, whose buttons nudge the tarp's edges, heights
    // and turn, move it, or pick it up to drag about (see CartRaft.BeginTarp). The tarp is made again at each change; what's
    // tuned is kept in the config file, and logged as the camp takes it, to be put into the camp.
    internal static class TarpTuning
    {
        private static ConfigFile _config;
        private static ConfigEntry<bool> _on;
        private static ConfigEntry<KeyboardShortcut> _key;
        private static ConfigEntry<string> _tuned;

        public static void Bind(ConfigFile config)
        {
            _config = config;
            _on = config.Bind("Dev", "Tarp tuning", false,
                "Tune a ruin's tarp by hand with its key. Switched on for a dev session and off again afterwards.");
            _key = config.Bind("Dev", "Tarp tuning key", new KeyboardShortcut(KeyCode.F7), "Opens the tarp tuning panel, while tarp tuning is on.");
            _tuned = config.Bind("Dev", "Tarp tuned", "", "The tarp as last tuned (as the camp takes it); used while tarp tuning is on.");
        }

        // The tarp's box: as tuned, while tuning is on and something's been tuned; else the camp's own.
        public static TarpBox BoxFor(RuinCamp camp) =>
            _on != null && _on.Value && TarpTune.TryParse(_tuned.Value, out var tuned) ? tuned : TarpTune.From(camp);

        private static GameObject _panel;
        private static CartBase _cart;
        private static TarpBox _box;
        private static bool _bigSteps;
        private static readonly List<(Text label, System.Func<string> text)> Labels = new List<(Text, System.Func<string>)>();
        private static Text _numbers;
        private static Text _stepText;

        public static void Update()
        {
            if (_panel && BepInEx.UnityInput.Current.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }
            if (_key == null || !_key.Value.IsDown())
                return;
            if (_panel)
            {
                Close();
                return;
            }
            // As the file has it now: switched there for a dev session.
            try
            {
                _config.Reload();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Cartheim] Couldn't read the config file again: {e.Message}");
            }
            var player = Player.m_localPlayer;
            if (!_on.Value)
            {
                Debug.Log("[Cartheim] Tarp tuning key pressed, but tarp tuning is switched off");
                return;
            }
            var cart = player ? CartBase.All.Where(c => c && c.InRuin && c.Tarp)
                .OrderBy(c => Vector3.Distance(c.transform.position, player.transform.position)).FirstOrDefault() : null;
            if (!cart)
            {
                player?.Message(MessageHud.MessageType.Center, "No settled cart with a tarp near");
                return;
            }
            Open(cart, BoxFor(cart.Camp));
        }

        private const float Width = 520f;
        private const float RowHeight = 40f;

        private static void Open(CartBase cart, TarpBox box)
        {
            Close();
            if (GUIManager.CustomGUIFront == null)
                return;
            _cart = cart;
            _box = box;
            Labels.Clear();
            var rows = new (string name, System.Func<float> value, System.Action<float> nudge, bool turn)[]
            {
                ("Move across", () => (_box.MinX + _box.MaxX) / 2f, d => _box = TarpTune.Moved(_box, d, 0f, 0f), false),
                ("Move back", () => (_box.MinZ + _box.MaxZ) / 2f, d => _box = TarpTune.Moved(_box, 0f, d, 0f), false),
                ("Raise", () => (_box.Low + _box.High) / 2f, d => _box = TarpTune.Moved(_box, 0f, 0f, d), false),
                ("Left edge", () => _box.MinX, d => _box.MinX += d, false),
                ("Right edge", () => _box.MaxX, d => _box.MaxX += d, false),
                ("Front edge", () => _box.MinZ, d => _box.MinZ += d, false),
                ("Back edge", () => _box.MaxZ, d => _box.MaxZ += d, false),
                ("Low side", () => _box.Low, d => _box.Low += d, false),
                ("High side", () => _box.High, d => _box.High += d, false),
                ("Turn", () => _box.Turn, d => _box.Turn += d, true),
            };
            var height = 190f + rows.Length * RowHeight;
            var middle = new Vector2(0.5f, 0.5f);
            _panel = GUIManager.Instance.CreateWoodpanel(GUIManager.CustomGUIFront.transform, middle, middle, new Vector2(Width / 2f + 40f, 0f), Width, height, draggable: true);
            Text(_panel.transform, "Tarp tuning", new Vector2(0f, height / 2f - 30f), 22, GUIManager.Instance.ValheimOrange, Width - 40f);
            _numbers = Text(_panel.transform, "", new Vector2(0f, height / 2f - 70f), 13, Color.white, Width - 40f);
            for (var i = 0; i < rows.Length; i++)
            {
                var (name, value, nudge, turn) = rows[i];
                var y = height / 2f - 115f - i * RowHeight;
                var label = Text(_panel.transform, "", new Vector2(-70f, y), 16, Color.white, 300f, TextAnchor.MiddleLeft);
                Labels.Add((label, () => $"{name}: {value():0.00}{(turn ? "°" : " m")}"));
                Button(_panel.transform, "-", new Vector2(Width / 2f - 110f, y), 50f, () => Change(nudge, -Step(turn)));
                Button(_panel.transform, "+", new Vector2(Width / 2f - 50f, y), 50f, () => Change(nudge, Step(turn)));
            }
            var bottom = -height / 2f + 30f;
            _stepText = Button(_panel.transform, "", new Vector2(-180f, bottom), 140f, () =>
            {
                _bigSteps = !_bigSteps;
                Show();
            }).GetComponentInChildren<Text>();
            Button(_panel.transform, "Grab", new Vector2(-30f, bottom), 120f, Grab);
            Button(_panel.transform, "Reset", new Vector2(100f, bottom), 100f, () => Apply(TarpTune.From(_cart.Camp)));
            Button(_panel.transform, "Close", new Vector2(205f, bottom), 90f, Close);
            GUIManager.BlockInput(true);
            Show();
        }

        private static float Step(bool turn) => turn ? (_bigSteps ? 5f : 1f) : (_bigSteps ? 0.5f : 0.1f);

        private static void Change(System.Action<float> nudge, float by)
        {
            nudge(by);
            Apply(_box);
        }

        // The tarp made again as tuned, kept in the file and logged.
        private static void Apply(TarpBox box)
        {
            _box = TarpTune.Tidy(box);
            _tuned.Value = TarpTune.Format(_box);
            if (_cart)
                _cart.RebuildTarp();
            Debug.Log($"[Cartheim] Tarp tuned: {_tuned.Value}");
            Show();
        }

        private static void Show()
        {
            if (!_panel)
                return;
            foreach (var (label, text) in Labels)
                label.text = text();
            _numbers.text = TarpTune.Format(_box);
            _stepText.text = _bigSteps ? "Steps: 0.5 m, 5°" : "Steps: 0.1 m, 1°";
        }

        // Picked up to drag about: the panel goes while the tarp is placed, and comes back when it's put down.
        private static void Grab()
        {
            var cart = _cart;
            var box = _box;
            Close();
            CartRaft.BeginTarp(cart, box, dropped =>
            {
                Open(cart, box);
                Apply(dropped);
            });
        }

        public static void Close()
        {
            if (!_panel)
                return;
            Object.Destroy(_panel);
            _panel = null;
            Labels.Clear();
            GUIManager.BlockInput(false);
        }

        private static Text Text(Transform parent, string text, Vector2 at, int size, Color color, float width, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var middle = new Vector2(0.5f, 0.5f);
            var label = GUIManager.Instance.CreateText(text, parent, middle, middle, at, GUIManager.Instance.AveriaSerifBold, size, color, true, Color.black, width, 30f, false)
                .GetComponent<Text>();
            label.alignment = anchor;
            return label;
        }

        private static GameObject Button(Transform parent, string text, Vector2 at, float width, System.Action onClick)
        {
            var middle = new Vector2(0.5f, 0.5f);
            var button = GUIManager.Instance.CreateButton(text, parent, middle, middle, at, width, 34f);
            button.GetComponent<Button>().onClick.AddListener(() => onClick());
            return button;
        }
    }
}
