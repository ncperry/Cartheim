using System;
using System.Collections.Generic;

namespace Cartheim.Plugin
{
    // Cartheim's buttons under its switch on Valheim Inventory's Mods page, which finds this class by its name.
    public static class ModButtons
    {
        public static IEnumerable<KeyValuePair<string, Action>> Buttons()
        {
            // A cart to test with from the start of a world; destroyed, it drops only what was built into it.
            if (Plugin.Enabled.Value && Player.m_localPlayer)
                yield return new KeyValuePair<string, Action>("Spawn cart", CartSpawn.Spawn);
        }
    }
}
