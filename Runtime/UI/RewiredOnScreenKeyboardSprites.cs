using UnityEngine;

namespace Wagenheimer.RewiredHelper.UI
{
    /// <summary>
    /// A procedurally generated, 9-sliceable rounded-rectangle sprite, so the generated on-screen keyboard has soft keys without
    /// shipping or authoring any art. Created once, at runtime (a generated sprite cannot be saved into a scene).
    /// </summary>
    public static class RewiredOnScreenKeyboardSprites
    {
        public const int Size = 64;
        public const float Radius = 18f;
        public const float BorderPixels = 20f;

        private static Sprite _rounded;

        /// <summary>White rounded rectangle with a 20px sliced border; tint it with <c>Image.color</c>.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (_rounded != null) return _rounded;

                var texture = CreateRoundedTexture();
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.Apply(false, true);

                _rounded = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(BorderPixels, BorderPixels, BorderPixels, BorderPixels));
                _rounded.name = "RewiredOnScreenKeyboard_Rounded";
                _rounded.hideFlags = HideFlags.HideAndDontSave;
                return _rounded;
            }
        }

        /// <summary>A readable white rounded-square texture. The Editor writes it to a PNG so the generated keyboard keeps a real, serialized sprite.</summary>
        public static Texture2D CreateRoundedTexture()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "RewiredOnScreenKeyboard_Rounded",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(CoverageAt(x + 0.5f, y + 0.5f) * 255f));
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>Anti-aliased coverage (0..1) of a rounded square for the pixel centred at (x, y).</summary>
        private static float CoverageAt(float x, float y)
        {
            // Distance from the pixel to the nearest point of the inner (corner-free) square; > Radius means outside the shape.
            var dx = Mathf.Max(Radius - x, 0f, x - (Size - Radius));
            var dy = Mathf.Max(Radius - y, 0f, y - (Size - Radius));
            var distance = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(Radius - distance + 0.5f);
        }
    }
}
