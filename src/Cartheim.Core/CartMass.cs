namespace Cartheim.Core
{
    // The game makes a cart heavier to pull the more its storage weighs. A cart that carries everything would drag, and an
    // empty-feeling one rattles about; it pulls as if it carried a set load instead, whatever it holds.
    public static class CartMass
    {
        public static float Pulled(float baseMass, float itemWeightMassFactor, float feltLoad) =>
            baseMass + System.Math.Max(0f, feltLoad) * itemWeightMassFactor;
    }
}
