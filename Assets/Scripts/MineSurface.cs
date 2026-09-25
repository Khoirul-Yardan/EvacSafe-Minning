using UnityEngine;

namespace SafeMining
{
    // Tileable surface maps generated once per material, without external texture dependencies.
    public static class MineSurface
    {
        public enum Kind { Rock, Gravel, Timber, Steel }

        public static Material Create(string name, Kind kind)
        {
            const int size = 256;
            var heights = new float[size * size];
            var albedo = new Color[size * size];
            var normals = new Color[size * size];
            var metallic = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float broad = Noise(u, v, 6, 6), fine = Noise(u, v, 75, 75);
                float grain = Noise(u, v, 27, 27);
                float h, shine = .08f, metal = 0;
                Color dark, light;
                switch (kind)
                {
                    case Kind.Timber:
                        float wood = Noise(u, v, 68, 3);
                        h = wood * .65f + grain * .12f;
                        dark = new Color(.105f, .075f, .047f); light = new Color(.38f, .29f, .18f);
                        break;
                    case Kind.Gravel:
                        h = grain * .4f + fine * .4f + broad * .2f;
                        dark = new Color(.13f, .12f, .105f); light = new Color(.40f, .37f, .31f);
                        shine = Mathf.Lerp(.06f, .24f, Mathf.SmoothStep(.45f, .72f, broad));
                        break;
                    case Kind.Steel:
                        h = fine * .12f + grain * .1f;
                        float rust = Mathf.SmoothStep(.48f, .72f, broad);
                        dark = Color.Lerp(new Color(.18f, .20f, .21f), new Color(.19f, .095f, .044f), rust);
                        light = Color.Lerp(new Color(.49f, .51f, .5f), new Color(.40f, .23f, .105f), rust);
                        metal = Mathf.Lerp(.8f, .1f, rust); shine = Mathf.Lerp(.48f, .16f, rust);
                        break;
                    default:
                        float strata = Mathf.Abs(Mathf.Sin((v * 12 + Noise(u, v, 5, 5) * .7f) * Mathf.PI * 2));
                        float fissure = Mathf.SmoothStep(.06f, .22f, strata);
                        h = broad * .36f + grain * .21f + fine * .13f + fissure * .3f;
                        dark = new Color(.12f, .13f, .125f); light = new Color(.48f, .46f, .40f);
                        break;
                }
                int i = y * size + x; heights[i] = h;
                float tone = kind == Kind.Steel ? grain : Mathf.Clamp01((h - .15f) * 1.5f);
                albedo[i] = Color.Lerp(dark, light, tone);
                metallic[i] = new Color(metal, 0, 0, shine);
            }
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = heights[y * size + (x + 1) % size] - heights[y * size + (x + size - 1) % size];
                float dy = heights[((y + 1) % size) * size + x] - heights[((y + size - 1) % size) * size + x];
                Vector3 n = new Vector3(-dx * 2.4f, -dy * 2.4f, 1).normalized;
                normals[y * size + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
            }
            var material = MineLayout.Material(name, Color.white);
            material.mainTexture = Texture(name + " | albedo", albedo, size, false);
            material.SetTexture("_BumpMap", Texture(name + " | normal", normals, size, true));
            material.SetTexture("_MetallicGlossMap", Texture(name + " | metal and smoothness", metallic, size, true));
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_BumpScale", 1); material.SetFloat("_Smoothness", 1);
            return material;
        }

        static Texture2D Texture(string name, Color[] pixels, int size, bool linear)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, linear);
            texture.name = name; texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear; texture.anisoLevel = 8;
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }

        static float Noise(float u, float v, float sx, float sy)
        {
            // Cross-fade opposite noise domains so the texture wraps without a hard seam.
            float a = Mathf.PerlinNoise(40 + u * sx, 40 + v * sy);
            float b = Mathf.PerlinNoise(40 + (u - 1) * sx, 40 + v * sy);
            float c = Mathf.PerlinNoise(40 + u * sx, 40 + (v - 1) * sy);
            float d = Mathf.PerlinNoise(40 + (u - 1) * sx, 40 + (v - 1) * sy);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
    }
}
