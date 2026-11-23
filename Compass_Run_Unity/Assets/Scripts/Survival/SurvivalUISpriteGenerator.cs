using UnityEngine;

namespace CompassRun.Survival
{
    /// <summary>
    /// Generates clean, anti-aliased SDF textures and sprites for modern rounded glassmorphism UI panels,
    /// subtle borders, segmented pills, and vector-like minimalist survival icons (Heart, Bolt, Hunger).
    /// Used both at Editor setup time (to bake PNG assets) and as a safe runtime fallback.
    /// </summary>
    public static class SurvivalUISpriteGenerator
    {
        /// <summary>
        /// Creates an anti-aliased filled rounded rectangle texture suitable for 9-slice UI scaling.
        /// </summary>
        public static Texture2D CreateRoundedRectTexture(int size, float cornerRadius, float borderThickness = 0f, bool outlineOnly = false)
        {
            return CreateRoundedRectTexture(size, size, cornerRadius, borderThickness, outlineOnly);
        }

        /// <summary>
        /// Creates an anti-aliased rounded rectangle texture with custom width and height (ideal for horizontal Filled pills).
        /// </summary>
        public static Texture2D CreateRoundedRectTexture(int width, int height, float cornerRadius, float borderThickness = 0f, bool outlineOnly = false)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            float maxExtentX = halfW - 1.5f;
            float maxExtentY = halfH - 1.5f;
            float radius = Mathf.Clamp(cornerRadius, 1f, Mathf.Min(maxExtentX, maxExtentY));
            float aa = 1.25f;

            var pixels = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = (x + 0.5f) - halfW;
                    float py = (y + 0.5f) - halfH;
                    float dist = SdRoundedBox(px, py, maxExtentX, maxExtentY, radius);

                    float alpha;
                    if (outlineOnly && borderThickness > 0f)
                    {
                        float outerAlpha = 1f - SmoothStep(-aa, aa, dist);
                        float innerAlpha = 1f - SmoothStep(-aa, aa, dist + borderThickness);
                        alpha = Mathf.Clamp01(outerAlpha - innerAlpha);
                    }
                    else
                    {
                        alpha = 1f - SmoothStep(-aa, aa, dist);
                    }

                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>
        /// Creates a crisp, minimalist anti-aliased Heart icon texture (64x64).
        /// </summary>
        public static Texture2D CreateHeartIconTexture(int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[size * size];
            float inv = 1f / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Normalizare [-1.25, 1.25], centrat ușor sus
                    float nx = ((x + 0.5f) * inv - 0.5f) * 2.55f;
                    float ny = ((y + 0.5f) * inv - 0.45f) * 2.55f;

                    // Ecuație implicită netedă pentru inimă: (x^2 + y^2 - 1)^3 - x^2 * y^3 <= 0
                    float xx = nx * nx;
                    float yy = ny * ny;
                    float a = xx + yy - 0.82f;
                    float f = a * a * a - xx * ny * ny * ny * 1.15f;

                    // Aproximare gradient pentru anti-aliasing uniform
                    float grad = Mathf.Sqrt(
                        Mathf.Pow(6f * nx * a * a - 2f * nx * ny * ny * ny * 1.15f, 2f) +
                        Mathf.Pow(6f * ny * a * a - 3f * xx * yy * 1.15f, 2f)
                    ) + 0.0001f;

                    float signedDist = f / grad;
                    float pixelWidth = 2.55f * inv * 1.3f;
                    float alpha = 1f - SmoothStep(-pixelWidth, pixelWidth, signedDist);

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>
        /// Creates a crisp, minimalist anti-aliased Lightning Bolt icon texture (64x64) for Stamina.
        /// </summary>
        public static Texture2D CreateBoltIconTexture(int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            // Două triunghiuri curate care formează un fulger modern
            Vector2 t1A = new Vector2(0.58f, 0.90f);
            Vector2 t1B = new Vector2(0.22f, 0.46f);
            Vector2 t1C = new Vector2(0.55f, 0.48f);

            Vector2 t2A = new Vector2(0.45f, 0.54f);
            Vector2 t2B = new Vector2(0.78f, 0.54f);
            Vector2 t2C = new Vector2(0.40f, 0.10f);

            var pixels = new Color[size * size];
            float inv = 1f / size;
            float aa = 1.4f * inv;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) * inv, (y + 0.5f) * inv);
                    float d = Mathf.Min(SdTriangle(p, t1A, t1B, t1C), SdTriangle(p, t2A, t2B, t2C));
                    float alpha = 1f - SmoothStep(-aa, aa, d);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>
        /// Creates a crisp, minimalist anti-aliased Apple/Berry/Food icon texture (64x64) for Hunger.
        /// </summary>
        public static Texture2D CreateHungerIconTexture(int size = 64)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[size * size];
            float inv = 1f / size;
            float aa = 1.4f * inv;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) * inv, (y + 0.5f) * inv);

                    // Siluetă stilizată de măr/fruct de pădure (două cercuri ușor suprapuse + o frunzuliță sus)
                    Vector2 qL = new Vector2((p.x - 0.39f) / 0.25f, (p.y - 0.42f) / 0.29f);
                    Vector2 qR = new Vector2((p.x - 0.61f) / 0.25f, (p.y - 0.42f) / 0.29f);
                    float dLeft = (qL.magnitude - 1f) * 0.26f;
                    float dRight = (qR.magnitude - 1f) * 0.26f;
                    float dBody = SmoothMin(dLeft, dRight, 0.12f);

                    // Mică scobitură la bază și vârf pentru forma naturală
                    float dTopNotch = Vector2.Distance(p, new Vector2(0.50f, 0.77f)) - 0.10f;
                    dBody = Mathf.Max(dBody, -dTopNotch);

                    // Frunzuliță/codiță stilizată sus-dreapta
                    Vector2 leafCenter = new Vector2(0.56f, 0.81f);
                    Vector2 lp = p - leafCenter;
                    float cosA = 0.766f;
                    float sinA = 0.642f;
                    Vector2 rlp = new Vector2(lp.x * cosA + lp.y * sinA, -lp.x * sinA + lp.y * cosA);
                    float dLeaf = (new Vector2(rlp.x / 0.13f, rlp.y / 0.065f).magnitude - 1f) * 0.07f;

                    // Mică codiță verticală
                    float dStem = SdSegment(p, new Vector2(0.50f, 0.67f), new Vector2(0.49f, 0.83f)) - 0.028f;

                    float d = Mathf.Min(dBody, Mathf.Min(dLeaf, dStem));
                    float alpha = 1f - SmoothStep(-aa, aa, d);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        public static Sprite CreateSprite(Texture2D texture, Vector4 border)
        {
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border
            );
        }

        private static float SdRoundedBox(float px, float py, float bx, float by, float r)
        {
            float qx = Mathf.Abs(px) - bx + r;
            float qy = Mathf.Abs(py) - by + r;
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - r;
        }

        private static float SdSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(Vector2.Dot(ba, ba), 0.00001f));
            return (pa - ba * h).magnitude;
        }

        private static float SdTriangle(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
            Vector2 v0 = p - p0, v1 = p - p1, v2 = p - p2;

            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));

            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            Vector2 d = Vector2.Min(
                Vector2.Min(
                    new Vector2(Vector2.Dot(pq0, pq0), s * (v0.x * e0.y - v0.y * e0.x)),
                    new Vector2(Vector2.Dot(pq1, pq1), s * (v1.x * e1.y - v1.y * e1.x))),
                new Vector2(Vector2.Dot(pq2, pq2), s * (v2.x * e2.y - v2.y * e2.x)));

            return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
        }

        private static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
