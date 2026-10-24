using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Cartheim.Plugin
{
    // Shared building blocks for the mod's panels, so they look and behave alike.
    internal static class PanelWidgets
    {
        private static readonly Vector2 Middle = new Vector2(0.5f, 0.5f);

        public static int ScrollRows = 3;
        // Every list row is 32 high with 2 between, so rows sit RowHeight apart.
        public const float RowSpacing = 2f;
        public const float RowHeight = 32f + RowSpacing;
        private static int _wheelLogs;

        // A vertical scrolling list whose rows get its full width.
        public static ScrollRect ScrollList(Transform parent, float width, float height, float offsetY)
        {
            var gui = GUIManager.Instance;
            var scroll = gui.CreateScrollView(parent, false, true, 8f, 6f, gui.ValheimScrollbarHandleColorBlock, new Color(0, 0, 0, 0.3f), width, height);
            scroll.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, offsetY);
            var scrollRect = scroll.GetComponentInChildren<ScrollRect>();
            // Unity's own wheel scrolling depends on the size of the wheel step, which is tiny here; ScrollByWheel scrolls instead.
            scrollRect.scrollSensitivity = 0f;
            // Jotunn's list doesn't size its rows' width without a horizontal scrollbar; rows then collapse.
            var list = scrollRect.content.GetComponent<VerticalLayoutGroup>();
            list.childControlWidth = true;
            list.childForceExpandWidth = true;
            list.spacing = RowSpacing;
            list.padding = new RectOffset(8, 8, 4, 4);
            return scrollRect;
        }

        // Like Valheim's own lists: read the wheel and move a fixed number of rows per notch.
        public static void ScrollByWheel(ScrollRect list)
        {
            // Raw Unity input: Valheim's ZInput reads nothing while Jotunn blocks game input for an open panel.
            var wheel = BepInEx.UnityInput.Current.mouseScrollDelta.y;
            if (list == null || wheel == 0f)
                return;
            if (_wheelLogs++ < 5)
                Debug.Log($"[Cartheim] Wheel delta {wheel}, scrolling {ScrollRows} rows");
            var content = list.content;
            var furthest = Mathf.Max(0f, content.rect.height - list.viewport.rect.height);
            var y = content.anchoredPosition.y - Mathf.Sign(wheel) * ScrollRows * RowHeight;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Mathf.Clamp(y, 0f, furthest));
        }

        public static void Clear(Transform list)
        {
            foreach (Transform row in list)
                Object.Destroy(row.gameObject);
        }

        public static Transform Row(Transform list)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(list, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            var element = row.GetComponent<LayoutElement>();
            element.minHeight = 32;
            element.preferredHeight = 32;
            return row.transform;
        }

        public static void Header(Transform list, string text, float width) =>
            Size(Label(Row(list), text, Vector2.zero, 18, GUIManager.Instance.ValheimOrange, width, TextAnchor.MiddleLeft), width);

        // Fixed column widths: min and preferred both set, so a narrow row can't squeeze them.
        public static GameObject Size(GameObject element, float width)
        {
            var layout = element.GetComponent<LayoutElement>();
            if (layout == null)
                layout = element.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0;
            layout.minHeight = 30;
            layout.preferredHeight = 30;
            return element;
        }

        public static GameObject Label(Transform parent, string text, Vector2 position, int size, Color color, float width,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var label = GUIManager.Instance.CreateText(text, parent, Middle, Middle, position, GUIManager.Instance.AveriaSerifBold,
                size, color, true, Color.black, width, 30, false);
            label.GetComponent<Text>().alignment = anchor;
            return label;
        }

        public static GameObject Button(Transform parent, string text, Vector2 position, float width, Action onClick)
        {
            var button = GUIManager.Instance.CreateButton(text, parent, Middle, Middle, position, width, 32);
            button.GetComponent<Button>().onClick.AddListener(() => onClick());
            return button;
        }
    }
}
