namespace Cartheim.Core
{
    // How a piece's model sits in its spot on the cart: shrunk evenly until it fits (never enlarged), and turned a quarter if it fits
    // bigger that way.
    public readonly struct Fit
    {
        public float Scale { get; }
        public bool Turned { get; }

        private Fit(float scale, bool turned)
        {
            Scale = scale;
            Turned = turned;
        }

        public static Fit Into(float spotWidth, float spotLength, float maxHeight, float width, float length, float height)
        {
            var straight = ScaleFor(spotWidth, spotLength, maxHeight, width, length, height);
            var turned = ScaleFor(spotWidth, spotLength, maxHeight, length, width, height);
            return turned > straight ? new Fit(turned, true) : new Fit(straight, false);
        }

        private static float ScaleFor(float spotWidth, float spotLength, float maxHeight, float width, float length, float height) =>
            System.Math.Min(1f, System.Math.Min(System.Math.Min(spotWidth / width, spotLength / length), maxHeight / height));
    }
}
