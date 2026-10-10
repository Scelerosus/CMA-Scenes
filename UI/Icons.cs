using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace CMAScenes
{
    // The small pictures of the settings window: the poses as little figures (she in full, the hero lying under her
    // fainter), the pages, and the choices of the ride. There are no image files: each picture is written below as a
    // few strokes and filled shapes on a 100 x 100 canvas, and drawn into a texture the first time it is wanted.
    //
    // The textures are white with the shape in the alpha only, so the window tints them (its accent when picked, grey
    // when not, darkened by the brightness setting) as it does its other white textures.
    //
    // How a picture is written: commands split by ';'. Each starts with its layer, then what it draws.
    //   layers: h = the main figure (full), f = in front of it (her arms: full, with a thin gap cut around them),
    //           m = the second one, the hero or the floor (faint), x = cut out of all
    //   c x y r               a filled circle
    //   o x y r w             a ring of half-thickness w
    //   l x1 y1 x2 y2 ... r   a line through the points, round-ended, of half-thickness r
    //   p r x1 y1 x2 y2 ...   a filled polygon with its corners rounded by r
    //   a x y r from to w     an arc of the circle, from and to in degrees (0 = right, 90 = down), half-thickness w
    // The y axis goes down, as on screen. The figures face right unless the pose has her back to him.
    internal static class Icons
    {
        private const int Size = 96;
        // How strong the second figure is, and the clear gap kept around the main figure where it passes over it.
        private const float SecondAlpha = 0.42f;
        private const float Gap = 2.6f;
        // The arms are drawn in front of her body with a thin gap around them, so an arm across her stays readable.
        private const float FrontGap = 1.8f;

        private static readonly Dictionary<string, string> Specs = new Dictionary<string, string>
        {
            // ---- the ride: she is over the hero, who lies on his back with his head to the right ----
            { "pose.cowgirl", "m c 87 83 6.5; m l 79 86 50 86 9 87 4.5; h l 67 90 60 89 65 63 47 72 56 45 5; h c 60 33 8; h l 53 29 47 37 3; f l 56 47 60 57 60 66 4.5" },
            { "pose.astride", "m c 87 83 6.5; m l 79 86 50 86 9 87 4.5; h l 43 89 64 84 46 73 60 48 5; h c 66 37 8; h l 59 33 54 41 3; f l 60 50 68 63 74 79 4.5" },
            { "pose.over-him", "m c 86 83 6.5; m l 78 86 50 86 9 87 4.5; h l 40 89 60 85 44 74 67 58 5; h c 77 50 8; h l 72 43 66 41 3; f l 67 59 81 71 93 88 4.5" },
            { "pose.leaning-back", "m c 87 83 6.5; m l 79 86 50 86 9 87 4.5; h l 69 90 63 89 68 62 52 72 43 45 5; h c 39 33 8; h l 33 30 28 38 3; f l 43 47 36 63 31 81 4.5" },
            { "pose.reverse", "m c 87 83 6.5; m l 79 86 50 86 9 87 4.5; h l 32 90 39 89 34 63 52 72 39 46 5; h c 34 35 8; h l 41 31 46 39 3; f l 39 48 36 58 40 66 4.5" },
            // Hands under her breasts (the cowgirl, sitting up, her forearms across).
            { "hands.under-breasts", "m c 87 83 6.5; m l 79 86 50 86 9 87 4.5; h l 67 90 60 89 65 63 47 72 52 44 5; h c 54 32 8; h l 47 29 42 37 3; f l 52 47 49 58 62 55 4.5" },
            // On her knees with her hands on her thighs: what "on her knees" means, beside the cowgirl's squat.
            { "legs.knees", "m c 87 83 6.5; m l 79 86 50 86 9 87 4.5; h l 43 89 64 84 46 73 52 46 5; h c 55 34 8; h l 48 30 43 38 3; f l 52 48 53 61 57 76 4.5" },

            // ---- the twerk: standing, bent forward, her bottom to the left ----
            { "pose.bent-over", "m l 20 94 82 94 2.2; h l 59 92 51 91 56 72 42 54 66 39 5; h c 39 56 8; h c 77 32 8; f l 66 41 64 56 57 69 4.5" },
            { "pose.deep-squat", "m l 20 94 82 94 2.2; h l 62 92 54 91 58 62 36 68 49 43 5; h c 33 68 8; h c 55 32 8; h l 49 27 44 34 3; f l 50 45 58 52 59 61 4.5" },
            { "pose.hands-on-hips", "m l 20 94 82 94 2.2; h l 57 92 49 91 54 74 44 58 54 32 5; h c 41 59 8; h c 58 20 8; h l 52 16 47 23 3; f l 54 34 39 42 45 55 4.5" },
            { "pose.low-arch", "m l 20 94 82 94 2.2; h l 52 92 44 91 46 71 38 50 53 53 68 45 5; h c 35 50 8; h c 78 40 8; f l 67 47 60 58 49 68 4.5" },
            // Standing as the game has her, arms down: the "as in the game" of the hands.
            { "pose.standing", "m l 20 94 82 94 2.2; h l 56 92 48 91 49 73 48 55 49 28 5; h c 50 16 8; h l 43 13 39 21 3; f l 49 31 54 44 56 56 4.5" },

            // ---- which hole: abstract marks ----
            { "hole.vagina", "h a 74 50 34 135 225 5; h a 26 50 34 -45 45 5" },
            { "hole.anus", "h o 50 50 15 5; h c 50 50 3.5" },

            // ---- the pages: bold and plain, to read on a small coloured square ----
            { "tab.home", "h p 3 10 50 50 13 90 50; h p 2 22 44 78 44 78 88 22 88; x p 1 42 62 58 62 58 92 42 92" },
            { "tab.twerk", "h l 58 94 48 92 56 70 38 50 64 34 8; h c 34 52 11; h c 78 26 11; f l 64 37 60 54 55 66 7" },
            { "tab.ride", "m c 88 80 8; m l 78 85 8 86 7; h l 68 92 59 90 66 60 44 70 54 38 8; h c 60 22 11; f l 54 42 58 54 58 63 7" },
            { "tab.bikini", "h p 3 14 46 31 22 46 46; h p 3 54 46 69 22 86 46; h l 46 43 54 43 3.5; h l 31 22 41 8 3.5; h l 69 22 59 8 3.5; h l 4 45 14 45 3.5; h l 86 45 96 45 3.5; h p 4 20 60 80 60 57 88 43 88; h l 8 58 20 60 3.5; h l 80 60 92 58 3.5" },
            { "tab.profiles", "h c 50 32 17; h l 26 80 74 80 22; x p 0 0 92 100 92 100 100 0 100" },
            { "tab.settings", "h c 50 50 29; h l 50 50 88 50 8; h l 50 50 76.9 76.9 8; h l 50 50 50 88 8; h l 50 50 23.1 76.9 8; h l 50 50 12 50 8; h l 50 50 23.1 23.1 8; h l 50 50 50 12 8; h l 50 50 76.9 23.1 8; x c 50 50 12" },
        };

        private static readonly Dictionary<string, Texture2D> s_made = new Dictionary<string, Texture2D>();

        // The picture of that name, made the first time; null if there is none (the caller then draws without it).
        internal static Texture2D Get(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            // (A picture that failed is kept as null, so it is not tried again every frame.)
            if (s_made.TryGetValue(name, out Texture2D made))
            {
                return made;
            }
            if (!Specs.TryGetValue(name, out string spec))
            {
                return null;
            }
            try
            {
                made = Draw(spec);
            }
            catch (Exception ex)
            {
                // (A picture that cannot be made is left out; the window draws the tile without it.)
                Plugin.ModLog.LogWarning("Icon '" + name + "' could not be drawn: " + ex.Message);
                made = null;
            }
            s_made[name] = made;
            return made;
        }

        // ---- drawing ----

        private struct Shape
        {
            public char layer;
            public char kind;
            public float[] n;
        }

        private static List<Shape> Parse(string spec)
        {
            var list = new List<Shape>();
            foreach (string part in spec.Split(';'))
            {
                string[] tok = part.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tok.Length < 3)
                {
                    continue;
                }
                var n = new float[tok.Length - 2];
                for (int i = 0; i < n.Length; i++)
                {
                    n[i] = float.Parse(tok[i + 2], CultureInfo.InvariantCulture);
                }
                char layer = tok[0][0];
                char kind = tok[1][0];
                if (kind == 'a')
                {
                    // An arc is made of short straight pieces: a line through points on it.
                    float from = n[3], to = n[4];
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(to - from) / 6f));
                    var pts = new float[(steps + 1) * 2 + 1];
                    for (int i = 0; i <= steps; i++)
                    {
                        float ang = (from + (to - from) * i / steps) * Mathf.Deg2Rad;
                        pts[i * 2] = n[0] + n[2] * Mathf.Cos(ang);
                        pts[i * 2 + 1] = n[1] + n[2] * Mathf.Sin(ang);
                    }
                    pts[pts.Length - 1] = n[5];
                    list.Add(new Shape { layer = layer, kind = 'l', n = pts });
                    continue;
                }
                list.Add(new Shape { layer = layer, kind = kind, n = n });
            }
            return list;
        }

        // Signed distance from (px, py) to the shape, in canvas units: negative inside.
        private static float Dist(Shape s, float px, float py)
        {
            float[] n = s.n;
            switch (s.kind)
            {
                case 'c':
                    return Len(px - n[0], py - n[1]) - n[2];
                case 'o':
                    return Mathf.Abs(Len(px - n[0], py - n[1]) - n[2]) - n[3];
                case 'l':
                {
                    float best = float.MaxValue;
                    int last = (n.Length - 1) / 2 - 1;
                    for (int i = 0; i < last; i++)
                    {
                        best = Mathf.Min(best, Segment(px, py, n[i * 2], n[i * 2 + 1], n[i * 2 + 2], n[i * 2 + 3]));
                    }
                    if (last < 1)
                    {
                        best = Len(px - n[0], py - n[1]);
                    }
                    return best - n[n.Length - 1];
                }
                case 'p':
                    return Polygon(px, py, n) - n[0];
                default:
                    return float.MaxValue;
            }
        }

        private static float Len(float x, float y)
        {
            return Mathf.Sqrt(x * x + y * y);
        }

        private static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float ex = bx - ax, ey = by - ay;
            float wx = px - ax, wy = py - ay;
            float t = Mathf.Clamp01((wx * ex + wy * ey) / Mathf.Max(1e-6f, ex * ex + ey * ey));
            return Len(wx - ex * t, wy - ey * t);
        }

        // (The points of the polygon start at n[1]; n[0] is the rounding.)
        private static float Polygon(float px, float py, float[] n)
        {
            int count = (n.Length - 1) / 2;
            float d = float.MaxValue;
            float sign = 1f;
            for (int i = 0, j = count - 1; i < count; j = i, i++)
            {
                float vix = n[1 + i * 2], viy = n[2 + i * 2];
                float vjx = n[1 + j * 2], vjy = n[2 + j * 2];
                float ex = vjx - vix, ey = vjy - viy;
                float wx = px - vix, wy = py - viy;
                float t = Mathf.Clamp01((wx * ex + wy * ey) / Mathf.Max(1e-6f, ex * ex + ey * ey));
                float bx = wx - ex * t, by = wy - ey * t;
                d = Mathf.Min(d, bx * bx + by * by);
                bool c1 = py >= viy, c2 = py < vjy, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3))
                {
                    sign = -sign;
                }
            }
            return sign * Mathf.Sqrt(d);
        }

        private static Texture2D Draw(string spec)
        {
            List<Shape> shapes = Parse(spec);
            float unit = Size / 100f;
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float px = (x + 0.5f) / unit;
                    float py = (y + 0.5f) / unit;
                    float main = float.MaxValue, front = float.MaxValue, second = float.MaxValue, cut = float.MaxValue;
                    foreach (Shape s in shapes)
                    {
                        float d = Dist(s, px, py);
                        if (s.layer == 'm')
                        {
                            second = Mathf.Min(second, d);
                        }
                        else if (s.layer == 'x')
                        {
                            cut = Mathf.Min(cut, d);
                        }
                        else if (s.layer == 'f')
                        {
                            front = Mathf.Min(front, d);
                        }
                        else
                        {
                            main = Mathf.Min(main, d);
                        }
                    }
                    // Edges a pixel wide, so the strokes are smooth at any size; the second figure stops short of the first.
                    float keep = Mathf.Clamp01(0.5f + cut * unit);
                    float a = Mathf.Max(Mathf.Clamp01(0.5f - front * unit), Mathf.Clamp01(0.5f - main * unit) * Mathf.Clamp01((front - FrontGap) * unit + 0.5f)) * keep;
                    float all = Mathf.Min(main, front);
                    float b = Mathf.Clamp01(0.5f - second * unit) * keep * Mathf.Clamp01((all - Gap) * unit + 0.5f) * SecondAlpha;
                    float alpha = a + b * (1f - a);
                    // (Texture rows go up from the bottom; the canvas goes down. One pixel at a time, as Menu's
                    // textures are made: an array handed across to the game's side is a copy of its own.)
                    t.SetPixel(x, Size - 1 - y, new Color(1f, 1f, 1f, alpha));
                }
            }
            t.Apply(true);
            t.filterMode = FilterMode.Trilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
    }
}
