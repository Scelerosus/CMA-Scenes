using System;

namespace CMAScenes
{
    // What the X-ray inset shows, in the picture's own flat frame: metres, x toward her front, y into her, the
    // entrance of the hole the sex is set to at the origin. (Made each frame from her bones and his, XRayInset.cs.)
    internal sealed class XRayScene
    {
        public const int Most = 32;
        // Her vagina from its entrance in, and her rectum from her anus in: the middle lines of them.
        public readonly float[] vagX = new float[Most], vagY = new float[Most];
        public int vagN;
        public readonly float[] anX = new float[Most], anY = new float[Most];
        public int anN;
        // The neck of her womb and its top.
        public bool hasUterus;
        public float cervixX, cervixY, wombX, wombY;
        // His penis from its root to its tip, and how thick he is at each point (from his middle line).
        public readonly float[] pX = new float[Most], pY = new float[Most], pR = new float[Most];
        public int pN;
        // The hole he is at is her anus (else her vagina).
        public bool anal;
        // How far along him from his root her entrance is; below zero while he is not in her.
        public float inFrom = -1f;
        // How hard she squeezes him (a contraction of her climax), how wet she is, how much of his semen is in her (in the
        // hole he is at; and in each of the two), as the game has it.
        public float squeeze, wet, semen, semenV, semenA;
        // His semen leaving her vagina and her anus (0 none, 1 the most), and how far its lumps have run (metres).
        public float flowV, flowA, flowRun;
        public bool condom;
        public int condomRgb = 0xE8E8F0;
        // How full the teat of the condom is (0..1) and how round (its radius, metres).
        public float condomFill, teatR = 0.0042f;
    }

    // The picture itself: a cut through her from front to back, with her vagina, her womb and her rectum where her
    // bones have them, and him in her.
    internal static class XRayPaint
    {
        private const int Skin = 0xE4AA9E, Flesh = 0xB2626C, FleshDeep = 0x8E4652, Wall = 0xDC7C88, WallEdge = 0x8A3246, Lumen = 0x5A1C2C;
        private const int Gut = 0xC87A70, GutEdge = 0x7E3A36, GutLumen = 0x4E2222, Womb = 0xCC6C78, WombEdge = 0x7C2C40;
        private const int His = 0xE2AC96, HisEdge = 0x7A4638, Glans = 0xD8848A, Light = 0xFFEEE6, Fluid = 0xFFE8EA, Semen = 0xFBF7EE, Bone = 0xE9DFCB, BoneEdge = 0x9A8C78;
        private const int Lips = 0xEC9A9C, LipsEdge = 0x8E4048;

        private static readonly float[] s_x = new float[XRayScene.Most * 2], s_y = new float[XRayScene.Most * 2], s_r = new float[XRayScene.Most * 2];
        private static readonly float[] s_cx = new float[XRayScene.Most * 2], s_cy = new float[XRayScene.Most * 2];

        private static int Mix(int a, int b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            int r = (int)(((a >> 16) & 255) + (((b >> 16) & 255) - ((a >> 16) & 255)) * t);
            int g = (int)(((a >> 8) & 255) + (((b >> 8) & 255) - ((a >> 8) & 255)) * t);
            int bl = (int)((a & 255) + ((b & 255) - (a & 255)) * t);
            return (r << 16) | (g << 8) | bl;
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        private static float Noise(float x, float y)
        {
            int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }

        // How far a point is from her skin, in metres (below zero: in her). Her body in the cut: a deep rounded shape
        // whose lowest line runs through her two entrances. "vx, vy" is where the entrance of her vagina is.
        private static float Body(float x, float y, float vx, float vy)
        {
            float cx = vx - 0.015f, halfW = 0.103f, round = 0.07f;
            float bottom = vy - 0.004f;
            float qx = MathF.Abs(x - cx) - (halfW - round);
            float qy = (bottom + round) - y;
            float ax = MathF.Max(qx, 0f), ay = MathF.Max(qy, 0f);
            return MathF.Sqrt(ax * ax + ay * ay) + MathF.Min(MathF.Max(qx, qy), 0f) - round;
        }

        private static float Ellipse(float x, float y, float cx, float cy, float rx, float ry, float turn)
        {
            float c = MathF.Cos(turn), s = MathF.Sin(turn);
            float px = (x - cx) * c + (y - cy) * s, py = -(x - cx) * s + (y - cy) * c;
            float k = MathF.Sqrt(px * px / (rx * rx) + py * py / (ry * ry));
            return (k - 1f) * MathF.Min(rx, ry);
        }

        private static void Put(byte[] p, int i, int rgb, float a)
        {
            int ia = (int)(a * 256f);
            if (ia <= 0)
            {
                return;
            }
            if (ia > 256)
            {
                ia = 256;
            }
            int r = (rgb >> 16) & 255, g = (rgb >> 8) & 255, b = rgb & 255;
            p[i] = (byte)(p[i] + (((r - p[i]) * ia) >> 8));
            p[i + 1] = (byte)(p[i + 1] + (((g - p[i + 1]) * ia) >> 8));
            p[i + 2] = (byte)(p[i + 2] + (((b - p[i + 2]) * ia) >> 8));
        }

        // What does not move: her flesh, her skin, and the bones and the bladder that show where in her one is looking.
        // "scale" is pixels to a metre; "ox, oy" the pixel of the origin; "vx, vy" where the entrance of her vagina is.
        public static byte[] Background(int w, int h, float scale, float ox, float oy, float vx, float vy)
        {
            byte[] p = new byte[w * h * 4];
            float mm = scale / 1000f;
            float corner = 12f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 4;
                    float fx = (x + 0.5f - ox) / scale, fy = (y + 0.5f - oy) / scale;
                    // The frame: rounded corners, a thin light rim.
                    float ex = MathF.Max(MathF.Abs(x + 0.5f - w * 0.5f) - (w * 0.5f - corner), 0f);
                    float ey = MathF.Max(MathF.Abs(y + 0.5f - h * 0.5f) - (h * 0.5f - corner), 0f);
                    float edge = MathF.Sqrt(ex * ex + ey * ey) + MathF.Min(MathF.Max(MathF.Abs(x + 0.5f - w * 0.5f) - (w * 0.5f - corner), MathF.Abs(y + 0.5f - h * 0.5f) - (h * 0.5f - corner)), 0f) - corner;
                    if (edge > 0.5f)
                    {
                        continue;
                    }
                    float frame = Clamp01(0.5f - edge);
                    // Outside her: dark, a little lighter toward the middle.
                    float vign = 1f - 0.35f * ((x - w * 0.5f) * (x - w * 0.5f) / (w * w * 0.25f) + (y - h * 0.5f) * (y - h * 0.5f) / (h * h * 0.25f));
                    p[i] = (byte)(30f * vign);
                    p[i + 1] = (byte)(22f * vign);
                    p[i + 2] = (byte)(34f * vign);
                    p[i + 3] = (byte)(214f * frame);
                    float d = Body(fx, fy, vx, vy);
                    float inside = Clamp01(0.5f - d * scale);
                    if (inside > 0f)
                    {
                        // Her flesh: deeper in colour the deeper in her, and mottled a little.
                        float deep = Clamp01(-d / 0.09f);
                        float n = 0.6f * Noise(fx * 170f, fy * 170f) + 0.4f * Noise(fx * 520f + 7f, fy * 520f + 3f);
                        int flesh = Mix(Flesh, FleshDeep, deep * 0.55f + (n - 0.5f) * 0.28f);
                        // Under her skin: a paler layer, then her skin itself.
                        float fat = Clamp01(1f - (-d - 0.0025f) / 0.008f);
                        flesh = Mix(flesh, 0xD89684, fat * 0.75f);
                        float skin = Clamp01(1f - (-d) / 0.0026f);
                        flesh = Mix(flesh, Skin, skin);
                        Put(p, i, flesh, inside);
                        p[i + 3] = (byte)(MathF.Max(p[i + 3], 250f * inside) * frame);
                    }
                    if (d < 0f)
                    {
                        // Her pubic bone before her vagina, her bladder above it, and the end of her spine behind her rectum.
                        float bone = Ellipse(fx, fy, vx + 0.049f, vy + 0.043f, 0.0165f, 0.0105f, 0.62f);
                        float tail = Segment(fx, fy, vx - 0.103f, vy + 0.215f, 0.0075f, vx - 0.096f, vy + 0.135f, 0.0062f);
                        tail = MathF.Min(tail, Segment(fx, fy, vx - 0.096f, vy + 0.135f, 0.0062f, vx - 0.082f, vy + 0.088f, 0.0045f));
                        tail = MathF.Min(tail, Segment(fx, fy, vx - 0.082f, vy + 0.088f, 0.0045f, vx - 0.066f, vy + 0.064f, 0.0026f));
                        float b = MathF.Min(bone, tail);
                        float grain = 0.85f + 0.15f * Noise(fx * 900f, fy * 900f);
                        Put(p, i, Mix(BoneEdge, Bone, grain), 0.9f * Clamp01(0.5f - (b + 0.0012f) * scale) + 0f);
                        Put(p, i, BoneEdge, 0.8f * Clamp01(0.5f - b * scale) * Clamp01(0.5f + (b + 0.0012f) * scale));
                        float bladder = Ellipse(fx, fy, vx + 0.030f, vy + 0.090f, 0.021f, 0.0145f, 0.35f);
                        Put(p, i, 0xD9A27E, 0.42f * Clamp01(0.5f - bladder * scale));
                        Put(p, i, 0x8A5A44, 0.55f * Clamp01(1f - MathF.Abs(bladder) * scale / 1.1f));
                    }
                    // The rim of the frame.
                    Put(p, i, 0xFFFFFF, 0.3f * Clamp01(1f - MathF.Abs(edge + 1.2f) / 1.1f));
                    _ = mm;
                }
            }
            return p;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }

        private static float Segment(float x, float y, float ax, float ay, float ra, float bx, float by, float rb)
        {
            float dx = bx - ax, dy = by - ay;
            float l2 = dx * dx + dy * dy;
            float h = l2 > 1e-9f ? ((x - ax) * dx + (y - ay) * dy) / l2 : 0f;
            h = h < 0f ? 0f : h > 1f ? 1f : h;
            float ex = x - ax - dx * h, ey = y - ay - dy * h;
            return MathF.Sqrt(ex * ex + ey * ey) - (ra + (rb - ra) * h);
        }

        // A canal at rest (shut: a tube of its wall with a dark line down its middle), from "skip" metres in from its
        // entrance. Gives back the pixel it begins at.
        private static bool Canal(XRayCanvas c, float[] mx, float[] my, int n, float skip, float scale, float ox, float oy, float wallR, float lumenR, int wall, int edge, int lumen, out float bx, out float by)
        {
            bx = by = 0f;
            if (n < 2)
            {
                return false;
            }
            int m = 0;
            float gone = 0f;
            for (int i = 0; i < n - 1; i++)
            {
                float sx = mx[i + 1] - mx[i], sy = my[i + 1] - my[i];
                float seg = MathF.Sqrt(sx * sx + sy * sy);
                if (gone + seg <= skip)
                {
                    gone += seg;
                    continue;
                }
                if (m == 0)
                {
                    float t = seg > 1e-6f ? Clamp01((skip - gone) / seg) : 0f;
                    s_cx[m] = ox + (mx[i] + sx * t) * scale;
                    s_cy[m] = oy + (my[i] + sy * t) * scale;
                    m++;
                }
                s_cx[m] = ox + mx[i + 1] * scale;
                s_cy[m] = oy + my[i + 1] * scale;
                m++;
                gone += seg;
            }
            if (m < 2)
            {
                return false;
            }
            bx = s_cx[0];
            by = s_cy[0];
            c.Line(s_cx, s_cy, 0, m - 1, wallR + 1.1f, edge, 1f);
            c.Line(s_cx, s_cy, 0, m - 1, wallR, wall, 1f);
            c.Line(s_cx, s_cy, 0, m - 1, lumenR, lumen, 1f);
            return true;
        }

        public static void Paint(XRayCanvas c, byte[] background, XRayScene s, float scale, float ox, float oy)
        {
            c.CopyFrom(background);
            float mm = scale / 1000f;
            float squeeze = Clamp01(s.squeeze);

            // His penis in pixels, and how far along it each point is.
            int n = Math.Min(s.pN, XRayScene.Most);
            float len = 0f;
            for (int i = 0; i < n; i++)
            {
                s_x[i] = ox + s.pX[i] * scale;
                s_y[i] = oy + s.pY[i] * scale;
                s_r[i] = MathF.Max(1.5f, s.pR[i] * scale);
                if (i > 0)
                {
                    float sx = s.pX[i] - s.pX[i - 1], sy = s.pY[i] - s.pY[i - 1];
                    len += MathF.Sqrt(sx * sx + sy * sy);
                }
            }
            bool inHer = n >= 2 && s.inFrom >= 0f && s.inFrom < len;
            float inserted = inHer ? len - s.inFrom : 0f;

            // Her other hole, shut; her womb; then the one he is at, from his tip on in.
            float bx, by;
            if (s.anal)
            {
                Canal(c, s.vagX, s.vagY, s.vagN, 0f, scale, ox, oy, 4.4f * mm, 0.9f * mm, Wall, WallEdge, Lumen, out bx, out by);
            }
            else
            {
                Canal(c, s.anX, s.anY, s.anN, 0f, scale, ox, oy, 5.2f * mm, 1.0f * mm, Gut, GutEdge, GutLumen, out bx, out by);
            }
            if (s.hasUterus)
            {
                float ux = ox + s.cervixX * scale, uy = oy + s.cervixY * scale, tx = ox + s.wombX * scale, ty = oy + s.wombY * scale;
                // (A pear: narrow at its neck, widest near its top.)
                float mx2 = ux + (tx - ux) * 0.72f, my2 = uy + (ty - uy) * 0.72f;
                c.Cone(ux, uy, 9.4f * mm, mx2, my2, 17.5f * mm, WombEdge, 1f);
                c.Cone(mx2, my2, 17.5f * mm, tx, ty, 14.5f * mm, WombEdge, 1f);
                c.Cone(ux, uy, 8.2f * mm, mx2, my2, 16.3f * mm, Womb, 1f);
                c.Cone(mx2, my2, 16.3f * mm, tx, ty, 13.3f * mm, Womb, 1f);
                c.Cone(ux + (tx - ux) * 0.1f, uy + (ty - uy) * 0.1f, 0.7f * mm, mx2, my2, 2.2f * mm, Mix(Womb, Lumen, 0.6f), 1f);
                c.Cone(mx2, my2, 2.2f * mm, ux + (tx - ux) * 0.9f, uy + (ty - uy) * 0.9f, 1.2f * mm, Mix(Womb, Lumen, 0.6f), 1f);
                // His semen in it, as the game has her vagina full of it: its cavity swells white.
                float f = Clamp01(s.semenV);
                if (f > 0.01f)
                {
                    c.Cone(ux + (tx - ux) * 0.1f, uy + (ty - uy) * 0.1f, (0.9f + 1.6f * f) * mm, mx2, my2, (2.6f + 7.5f * f) * mm, Semen, 0.95f);
                    c.Cone(mx2, my2, (2.6f + 7.5f * f) * mm, ux + (tx - ux) * 0.9f, uy + (ty - uy) * 0.9f, (1.4f + 4f * f) * mm, Semen, 0.95f);
                }
            }
            float wallR = (s.anal ? 5.2f : 4.4f) * mm, lumenR = (s.anal ? 1.0f : 0.9f) * mm * (1f - 0.5f * squeeze);
            int wall = Mix(s.anal ? Gut : Wall, 0xF0627C, squeeze * 0.55f), edge = s.anal ? GutEdge : WallEdge, lumen = s.anal ? GutLumen : Lumen;
            bool rest = s.anal
                ? Canal(c, s.anX, s.anY, s.anN, inserted, scale, ox, oy, wallR, lumenR, wall, edge, lumen, out bx, out by)
                : Canal(c, s.vagX, s.vagY, s.vagN, inserted, scale, ox, oy, wallR, lumenR, wall, edge, lumen, out bx, out by);
            if (s.hasUterus)
            {
                // The neck of her womb, at the end of her vagina: a knob with its mouth.
                float ux = ox + s.cervixX * scale, uy = oy + s.cervixY * scale;
                c.Disc(ux, uy, 7.4f * mm, WombEdge, 1f);
                c.Disc(ux, uy, 6.3f * mm, Mix(Womb, 0xE89AA0, 0.5f), 1f);
                c.Disc(ux, uy, 1.3f * mm, s.semenV <= 0.01f ? Lumen : Mix(Lumen, Semen, Clamp01(s.semenV * 2f)), 1f);
            }

            // His semen in her canals, as full as the game has them (from the far end down), and running out of her
            // while the game lets it out. (Up her rectum: only its last centimetres.)
            SemenIn(c, s.vagX, s.vagY, s.vagN, Clamp01(s.semenV), 1f, scale, ox, oy, mm);
            SemenIn(c, s.anX, s.anY, s.anN, Clamp01(s.semenA), 0.06f, scale, ox, oy, mm);
            FlowIn(c, s.vagX, s.vagY, s.vagN, Clamp01(s.flowV), s.flowRun, 1f, scale, ox, oy, mm);
            FlowIn(c, s.anX, s.anY, s.anN, Clamp01(s.flowA), s.flowRun, 0.07f, scale, ox, oy, mm);

            if (n < 2)
            {
                // (He is not in the picture: her entrances shut, with what runs out of them over them.)
                Shut(c, ox, oy, mm);
                if (s.anal)
                {
                    Shut(c, ox + (s.vagN > 0 ? s.vagX[0] : 0.04f) * scale, oy + (s.vagN > 0 ? s.vagY[0] : 0f) * scale, mm);
                }
                else if (s.anN > 0)
                {
                    Shut(c, ox + s.anX[0] * scale, oy + s.anY[0] * scale, mm);
                }
                Drip(c, s.vagX, s.vagY, s.vagN, Clamp01(s.flowV), s.flowRun, scale, ox, oy, mm);
                Drip(c, s.anX, s.anY, s.anN, Clamp01(s.flowA), s.flowRun, scale, ox, oy, mm);
                return;
            }

            // Her wall round him, as far as he is in her: stretched to him, thicker and redder while she squeezes.
            if (inHer)
            {
                float coat = (3.1f + 2.4f * squeeze) * mm;
                float tipX = s_x[n - 1], tipY = s_y[n - 1], tipR = s_r[n - 1];
                if (rest)
                {
                    // (From his tip on, her canal closes again: a little tent before him.)
                    c.Cone(tipX, tipY, tipR + coat + 1.1f, bx, by, wallR + 1.1f, edge, 1f);
                    c.Cone(tipX, tipY, tipR + coat, bx, by, wallR, wall, 1f);
                    c.Cone(tipX, tipY, tipR * 0.5f, bx, by, lumenR, lumen, 1f);
                }
                Along(c, n, s.inFrom, len, scale, coat + 1.1f, 1f, edge, 1f);
                Along(c, n, s.inFrom, len, scale, coat, 1f, wall, 1f);
                // (What she is wet with, between her and him; and his semen, from his tip back down him.)
                Along(c, n, s.inFrom, len, scale, 1.3f * mm, 1f, Fluid, 0.75f * Clamp01(s.wet));
                if (s.semen > 0.01f && !s.condom)
                {
                    Along(c, n, MathF.Max(s.inFrom, len - 0.02f - 0.07f * Clamp01(s.semen)), len, scale, 1.9f * mm, 1f, Semen, 0.95f);
                }
            }

            // Him. (Every stroke of him is painted solid: see-through strokes show at each joint of the line they follow.
            // In a condom he is of its colour, with a rim of it round him and its tip before his.)
            float glansFrom = len - 0.036f;
            int his = s.condom ? Mix(His, s.condomRgb, 0.42f) : His, glans = s.condom ? Mix(Glans, s.condomRgb, 0.42f) : Glans;
            if (s.condom)
            {
                // Its teat before his tip, as round as what he came in it, white with it.
                float teat = MathF.Max(2.8f, s.teatR * 1000f) * mm;
                float dx = s_x[n - 1] - s_x[n - 2], dy = s_y[n - 1] - s_y[n - 2];
                float dl = MathF.Max(1e-4f, MathF.Sqrt(dx * dx + dy * dy));
                float px = s_x[n - 1] + dx / dl * (s_r[n - 1] * 0.6f + teat), py = s_y[n - 1] + dy / dl * (s_r[n - 1] * 0.6f + teat);
                c.Disc(px, py, teat + 0.8f * mm, HisEdge, 1f);
                c.Disc(px, py, teat, Mix(s.condomRgb, 0xFFFFFF, 0.25f), 1f);
                if (s.condomFill > 0.01f)
                {
                    c.Disc(px, py, teat * (0.45f + 0.5f * Clamp01(s.condomFill)), Semen, 1f);
                }
                Along(c, n, 0f, len, scale, 2.0f * mm, 1f, HisEdge, 1f);
                Along(c, n, 0f, len, scale, 1.2f * mm, 1f, Mix(s.condomRgb, 0xFFFFFF, 0.2f), 1f);
            }
            else
            {
                Along(c, n, 0f, len, scale, 1.0f * mm, 1f, HisEdge, 1f);
            }
            Along(c, n, 0f, MathF.Max(0f, glansFrom + 0.004f), scale, 0f, 1f, his, 1f);
            Along(c, n, MathF.Max(0f, glansFrom), len, scale, 0f, 1f, glans, 1f);
            // (A light along one side of him, and a shade along the other, so that he reads as round.)
            Along(c, n, 0.012f, MathF.Max(0.012f, glansFrom), scale, 0f, 0.2f, Mix(his, Light, 0.42f), 1f, -0.48f);
            Along(c, n, MathF.Max(0.012f, glansFrom + 0.006f), len - 0.008f, scale, 0f, 0.2f, Mix(glans, Light, 0.38f), 1f, -0.44f);
            Along(c, n, 0.012f, MathF.Max(0.012f, glansFrom), scale, 0f, 0.13f, Mix(his, HisEdge, 0.22f), 1f, 0.7f);

            // The lips of her entrance: either side of him where he goes into her, or shut.
            Entrance(c, s, n, inHer, ox, oy, mm);
            if (s.anal)
            {
                Shut(c, ox + (s.vagN > 0 ? s.vagX[0] : 0.04f) * scale, oy + (s.vagN > 0 ? s.vagY[0] : 0f) * scale, mm);
            }
            else if (s.anN > 0)
            {
                Shut(c, ox + s.anX[0] * scale, oy + s.anY[0] * scale, mm);
            }

            // His semen in her: let out at his tip, a pool before him.
            if (inHer && s.semen > 0.01f && !s.condom)
            {
                float q = Clamp01(s.semen);
                float dx = s_x[n - 1] - s_x[n - 2], dy = s_y[n - 1] - s_y[n - 2];
                float dl = MathF.Max(1e-4f, MathF.Sqrt(dx * dx + dy * dy));
                float px = s_x[n - 1] + dx / dl * (s_r[n - 1] * 0.9f), py = s_y[n - 1] + dy / dl * (s_r[n - 1] * 0.9f);
                c.Disc(px, py, (2.2f + 5.5f * q) * mm, Semen, 0.92f);
                c.Disc(px - dy / dl * 3.5f * mm * q, py + dx / dl * 3.5f * mm * q, (1.2f + 3.2f * q) * mm, Semen, 0.9f);
                c.Disc(px + dy / dl * 4.2f * mm * q, py - dx / dl * 4.2f * mm * q, (1f + 2.6f * q) * mm, Semen, 0.9f);
            }
            // What runs out of her, over her lips.
            Drip(c, s.vagX, s.vagY, s.vagN, Clamp01(s.flowV), s.flowRun, scale, ox, oy, mm);
            Drip(c, s.anX, s.anY, s.anN, Clamp01(s.flowA), s.flowRun, scale, ox, oy, mm);
        }

        // How long a canal is, in metres (its points run from its entrance in).
        private static float CanalLength(float[] mx, float[] my, int n)
        {
            float len = 0f;
            for (int i = 0; i + 1 < n; i++)
            {
                float sx = mx[i + 1] - mx[i], sy = my[i + 1] - my[i];
                len += MathF.Sqrt(sx * sx + sy * sy);
            }
            return len;
        }

        // Where along a canal a place is, "fromEntrance" metres in from its entrance (pixels).
        private static void CanalAt(float[] mx, float[] my, int n, float fromEntrance, float scale, float ox, float oy, out float x, out float y)
        {
            float gone = 0f;
            for (int i = 0; i + 1 < n; i++)
            {
                float sx = mx[i + 1] - mx[i], sy = my[i + 1] - my[i];
                float seg = MathF.Sqrt(sx * sx + sy * sy);
                if (gone + seg >= fromEntrance || i == n - 2)
                {
                    float t = seg > 1e-6f ? Clamp01((fromEntrance - gone) / seg) : 0f;
                    x = ox + (mx[i] + sx * t) * scale;
                    y = oy + (my[i] + sy * t) * scale;
                    return;
                }
                gone += seg;
            }
            x = ox + (n > 0 ? mx[0] : 0f) * scale;
            y = oy + (n > 0 ? my[0] : 0f) * scale;
        }

        // His semen lying in a canal, from its far end toward its entrance: the fuller, the further down and the thicker.
        private static void SemenIn(XRayCanvas c, float[] mx, float[] my, int n, float fill, float keep, float scale, float ox, float oy, float mm)
        {
            if (fill <= 0.01f || n < 2)
            {
                return;
            }
            float len = CanalLength(mx, my, n);
            float deep = MathF.Min(keep, len);
            float lies = deep * (0.25f + 0.75f * fill);
            float r = (0.7f + 1.3f * fill) * mm;
            const int steps = 12;
            CanalAt(mx, my, n, len, scale, ox, oy, out float px, out float py);
            for (int k = 1; k <= steps; k++)
            {
                CanalAt(mx, my, n, len - lies * k / steps, scale, ox, oy, out float qx, out float qy);
                c.Cone(px, py, r, qx, qy, r * (1f - 0.4f * k / steps), Semen, 0.9f);
                px = qx;
                py = qy;
            }
        }

        // His semen running out of a canal while the game lets it out: a thin stream from the far end to the entrance
        // (of the last "keep" metres), with lumps running along it toward the entrance.
        private static void FlowIn(XRayCanvas c, float[] mx, float[] my, int n, float flow, float run, float keep, float scale, float ox, float oy, float mm)
        {
            if (flow <= 0.02f || n < 2)
            {
                return;
            }
            float len = CanalLength(mx, my, n);
            float runs = MathF.Min(keep, len);
            const int steps = 14;
            float r = (0.4f + 0.5f * flow) * mm;
            CanalAt(mx, my, n, runs, scale, ox, oy, out float px, out float py);
            for (int k = 1; k <= steps; k++)
            {
                CanalAt(mx, my, n, runs * (1f - k / (float)steps), scale, ox, oy, out float qx, out float qy);
                c.Cone(px, py, r, qx, qy, r, Semen, 0.85f);
                px = qx;
                py = qy;
            }
            const float lump = 0.016f;
            float lumpR = (1.0f + 1.4f * flow) * mm;
            for (float d = ((run % lump) + lump) % lump; d < runs; d += lump)
            {
                CanalAt(mx, my, n, runs - d, scale, ox, oy, out float lx, out float ly);
                c.Disc(lx, ly, lumpR, Semen, 0.95f);
            }
        }

        // What has come out of a canal's entrance: drops falling away from it, outward, at the pace it runs.
        private static void Drip(XRayCanvas c, float[] mx, float[] my, int n, float flow, float run, float scale, float ox, float oy, float mm)
        {
            if (flow <= 0.02f || n < 2)
            {
                return;
            }
            float ex = mx[0], ey = my[0];
            float dx = mx[0] - mx[1], dy = my[0] - my[1];
            float dl = MathF.Sqrt(dx * dx + dy * dy);
            if (dl < 1e-5f)
            {
                dx = 0f;
                dy = -1f;
            }
            else
            {
                dx /= dl;
                dy /= dl;
            }
            const float gap = 0.008f;
            float r = (1.0f + 1.6f * flow) * mm;
            for (float d = ((run % gap) + gap) % gap; d < 0.03f; d += gap)
            {
                // (Out along the canal at first, then falling away toward the bottom of the picture.)
                float x = ex + dx * MathF.Min(d, 0.01f);
                float y = ey + dy * MathF.Min(d, 0.01f) - MathF.Max(0f, d - 0.01f);
                c.Disc(ox + x * scale, oy + y * scale, r * (1f - 0.5f * d / 0.03f), Semen, 0.95f * (1f - d / 0.034f));
            }
            c.Disc(ox + ex * scale, oy + ey * scale, r * 1.1f, Semen, 0.9f);
        }

        // A stroke along him between two places on him (metres from his root), "grow" pixels thicker than he is and
        // "part" of his thickness; "aside" shifts it across him by that part of his thickness.
        private static void Along(XRayCanvas c, int n, float from, float to, float scale, float grow, float part, int rgb, float alpha, float aside = 0f)
        {
            if (to <= from || alpha <= 0.003f)
            {
                return;
            }
            float gone = 0f;
            for (int i = 0; i < n - 1; i++)
            {
                float dx = s_x[i + 1] - s_x[i], dy = s_y[i + 1] - s_y[i];
                float segPx = MathF.Sqrt(dx * dx + dy * dy);
                float seg = segPx / scale;
                float a = gone, b = gone + seg;
                gone = b;
                if (b <= from || a >= to || seg < 1e-6f)
                {
                    continue;
                }
                float t0 = Clamp01((from - a) / seg), t1 = Clamp01((to - a) / seg);
                float x0 = s_x[i] + dx * t0, y0 = s_y[i] + dy * t0, x1 = s_x[i] + dx * t1, y1 = s_y[i] + dy * t1;
                float r0 = s_r[i] + (s_r[i + 1] - s_r[i]) * t0, r1 = s_r[i] + (s_r[i + 1] - s_r[i]) * t1;
                if (aside != 0f)
                {
                    float nx = -dy / segPx, ny = dx / segPx;
                    x0 += nx * r0 * aside;
                    y0 += ny * r0 * aside;
                    x1 += nx * r1 * aside;
                    y1 += ny * r1 * aside;
                }
                c.Cone(x0, y0, r0 * part + grow, x1, y1, r1 * part + grow, rgb, alpha);
            }
        }

        private static void Shut(XRayCanvas c, float x, float y, float mm)
        {
            c.Disc(x - 2.6f * mm, y - 0.6f * mm, 3.9f * mm, LipsEdge, 1f);
            c.Disc(x + 2.6f * mm, y - 0.6f * mm, 3.9f * mm, LipsEdge, 1f);
            c.Disc(x - 2.6f * mm, y - 0.6f * mm, 3.0f * mm, Lips, 1f);
            c.Disc(x + 2.6f * mm, y - 0.6f * mm, 3.0f * mm, Lips, 1f);
        }

        private static void Entrance(XRayCanvas c, XRayScene s, int n, bool inHer, float ox, float oy, float mm)
        {
            if (!inHer)
            {
                Shut(c, ox, oy, mm);
                return;
            }
            // Where on him her entrance is, and which way he points there.
            float gone = 0f;
            for (int i = 0; i < n - 1; i++)
            {
                float dx = s_x[i + 1] - s_x[i], dy = s_y[i + 1] - s_y[i];
                float segPx = MathF.Sqrt(dx * dx + dy * dy);
                float seg = MathF.Sqrt((s.pX[i + 1] - s.pX[i]) * (s.pX[i + 1] - s.pX[i]) + (s.pY[i + 1] - s.pY[i]) * (s.pY[i + 1] - s.pY[i]));
                if (gone + seg >= s.inFrom || i == n - 2)
                {
                    float t = seg > 1e-6f ? Clamp01((s.inFrom - gone) / seg) : 0f;
                    float x = s_x[i] + dx * t, y = s_y[i] + dy * t, r = s_r[i] + (s_r[i + 1] - s_r[i]) * t;
                    float nx = segPx > 1e-4f ? -dy / segPx : 1f, ny = segPx > 1e-4f ? dx / segPx : 0f;
                    float off = r + 3.2f * mm;
                    c.Disc(x + nx * off, y + ny * off, 4.0f * mm, LipsEdge, 1f);
                    c.Disc(x - nx * off, y - ny * off, 4.0f * mm, LipsEdge, 1f);
                    c.Disc(x + nx * off, y + ny * off, 3.1f * mm, Lips, 1f);
                    c.Disc(x - nx * off, y - ny * off, 3.1f * mm, Lips, 1f);
                    return;
                }
                gone += seg;
            }
        }
    }
}
