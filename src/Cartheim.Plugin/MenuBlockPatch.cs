using HarmonyLib;

namespace Cartheim.Plugin
{
    // While the cart panel is open, Esc belongs to it: stop the game menu from reacting to the same key press.
    [HarmonyPatch(typeof(Menu), "Update")]
    internal static class MenuBlockPatch
    {
        public static bool PanelOpen;

        private static bool Prefix() => !PanelOpen;
    }
}
