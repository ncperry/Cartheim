namespace Cartheim.Core
{
    // A struct so two counts of the same prefab and amount compare equal.
    public readonly struct ItemCount
    {
        public ItemCount(string prefab, int count)
        {
            Prefab = prefab;
            Count = count;
        }

        public string Prefab { get; }
        public int Count { get; }

        public override string ToString() => $"{Count} {Prefab}";
    }
}
