namespace Cartheim.Core
{
    // How a piece's model sits in its spot on the cart: shrunk evenly until it fits.
    public readonly struct Fit
    {
        public float Scale { get; }

        private Fit(float scale)
        {
            Scale = scale;
        }

        public static Fit Into(float spotWidth, float spotLength, float maxHeight, float width, float length, float height) =>
            new Fit(System.Math.Min(System.Math.Min(spotWidth / width, spotLength / length), maxHeight / height));
    }
}
