namespace Cartheim.Core
{
    // Paying for something built or crafted beside the cart: from the player's own inventory first, then from the cart's
    // storage (its own, then each container built into it), in that order.
    public static class MaterialTake
    {
        // How many to take from each place, given how many each has (the player's inventory first).
        public static int[] Split(int need, int[] have)
        {
            var take = new int[have.Length];
            for (var i = 0; i < have.Length && need > 0; i++)
            {
                take[i] = System.Math.Min(need, have[i]);
                need -= take[i];
            }
            return take;
        }
    }
}
