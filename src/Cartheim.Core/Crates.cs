using System;
using System.Collections.Generic;
using System.Linq;

namespace Cartheim.Core
{
    // The cart's storage, sorted by type into crates, each shown under its own tab. The types are Valheim Inventory's
    // categories; a crate takes a few of them. The misc crate is the cart's own storage, and takes whatever no other crate
    // does.
    public class Crate
    {
        private const string SlotPrefix = "cartheim_crate:";

        internal Crate(string name, string icon, params string[] categories)
        {
            Name = name;
            Icon = icon;
            Categories = categories;
        }

        public string Name { get; }

        // An item standing for what the crate holds, shown on its tab.
        public string Icon { get; }

        public IReadOnlyList<string> Categories { get; }

        // Where it rides in the cart; the misc crate, being the cart's own storage, rides nowhere.
        public string Slot => this == Crates.Misc ? null : SlotPrefix + Name;

        public bool Takes(string category) => Crates.For(category) == this;

        // "the food crate in the cart", for telling the player where something went.
        public string InTheCart => $"the {Name.ToLowerInvariant()} crate in the cart";

        internal static bool IsSlot(string slot) => slot != null && slot.StartsWith(SlotPrefix, StringComparison.Ordinal);
    }

    public static class Crates
    {
        public static readonly Crate Misc = new Crate("Misc", "Ectoplasm", "Misc");

        // In tab order.
        public static readonly IReadOnlyList<Crate> All = new[]
        {
            new Crate("Building", "Wood", "Wood", "Stone"),
            new Crate("Metals", "Bronze", "Ore", "Metal"),
            new Crate("Food", "CarrotSoup", "Food", "Fish"),
            new Crate("Ingredients", "RawMeat", "Ingredients", "Seeds"),
            new Crate("Hides & drops", "DeerHide", "Textiles", "MonsterDrops"),
            new Crate("Trophies", "TrophyDeer", "Trophies"),
            new Crate("Meads", "MeadHealthMinor", "Meads"),
            new Crate("Gear", "AxeBronze", "Gear", "Ammo"),
            new Crate("Valuables", "Coins", "Valuables"),
            Misc,
        };

        public static Crate For(string category) => All.FirstOrDefault(c => c.Categories.Contains(category)) ?? Misc;

        public static bool IsSlot(string slot) => Crate.IsSlot(slot);

        public static Crate InSlot(string slot) => All.FirstOrDefault(c => c.Slot == slot);

        // The items (by their index) that belong in another crate than the one they're in, and which.
        public static List<(int index, Crate crate)> Misplaced(Crate @in, IReadOnlyList<string> prefabs, Func<string, string> categoryOf) =>
            prefabs.Select((prefab, i) => (i, crate: For(categoryOf(prefab))))
                .Where(m => m.crate != @in)
                .ToList();

        // How the totals of each item differ between two counts of the same storage, one line per item; none when they match.
        public static List<string> Differences(IEnumerable<ItemCount> before, IEnumerable<ItemCount> after)
        {
            var was = Totals(before);
            var now = Totals(after);
            return was.Keys.Union(now.Keys).OrderBy(k => k, StringComparer.Ordinal)
                .Select(k => (k, b: was.TryGetValue(k, out var b) ? b : 0, a: now.TryGetValue(k, out var a) ? a : 0))
                .Where(d => d.b != d.a)
                .Select(d => $"{d.k}: {d.b} before, {d.a} after")
                .ToList();
        }

        private static Dictionary<string, int> Totals(IEnumerable<ItemCount> counts) =>
            counts.GroupBy(c => c.Prefab).ToDictionary(g => g.Key, g => g.Sum(c => c.Count));
    }
}
