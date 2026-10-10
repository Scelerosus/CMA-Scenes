using System;

namespace CMAScenes
{
    // A small painter for the X-ray inset: round-ended, tapering strokes and discs with soft edges, blended into a
    // picture of bytes (red, green, blue, alpha; the bottom row first, as textures have it).
    //
    // Nothing of Unity's is used here or in XRayPaint.cs: the picture can be painted outside the game too, which is how
    // it was looked at while it was made.
    internal sealed class XRayCanvas
    {
        public readonly int W, H;
        public readonly byte[] px;

        public XRayCanvas(int w, int h)
        {
            W = w;
            H = h;
            px = new byte[w * h * 4];
        }

        public void CopyFrom(byte[] src)
        {
            Buffer.BlockCopy(src, 0, px, 0, px.Length);
        }

        // A stroke from a to b, round at both ends, as thick as ra at a and rb at b (all in pixels). Colour 0xRRGGBB.
        public void Cone(float ax, float ay, float ra, float bx, float by, float rb, int rgb, float alpha)
        {
            if (alpha <= 0.003f || (ra <= 0f && rb <= 0f))
            {
                return;
            }
            float rmax = MathF.Max(ra, rb) + 1.5f;
            int x0 = Math.Max(0, (int)MathF.Floor(MathF.Min(ax, bx) - rmax));
            int x1 = Math.Min(W - 1, (int)MathF.Ceiling(MathF.Max(ax, bx) + rmax));
            int y0 = Math.Max(0, (int)MathF.Floor(MathF.Min(ay, by) - rmax));
            int y1 = Math.Min(H - 1, (int)MathF.Ceiling(MathF.Max(ay, by) + rmax));
            if (x0 > x1 || y0 > y1)
            {
                return;
            }
            float dx = bx - ax, dy = by - ay;
            float len2 = dx * dx + dy * dy;
            float inv = len2 > 1e-6f ? 1f / len2 : 0f;
            int cr = (rgb >> 16) & 255, cg = (rgb >> 8) & 255, cb = rgb & 255;
            byte[] p = px;
            for (int y = y0; y <= y1; y++)
            {
                float qy = y + 0.5f - ay;
                int i = (y * W + x0) * 4;
                for (int x = x0; x <= x1; x++, i += 4)
                {
                    float qx = x + 0.5f - ax;
                    float h = (qx * dx + qy * dy) * inv;
                    h = h < 0f ? 0f : h > 1f ? 1f : h;
                    float ex = qx - dx * h, ey = qy - dy * h;
                    float r = ra + (rb - ra) * h;
                    float d2 = ex * ex + ey * ey;
                    float ro = r + 0.5f;
                    if (ro <= 0f || d2 >= ro * ro)
                    {
                        continue;
                    }
                    float ri = r - 0.5f;
                    float cov = ri > 0f && d2 <= ri * ri ? 1f : ro - MathF.Sqrt(d2);
                    if (cov > 1f)
                    {
                        cov = 1f;
                    }
                    int a = (int)(cov * alpha * 256f);
                    if (a <= 0)
                    {
                        continue;
                    }
                    if (a > 256)
                    {
                        a = 256;
                    }
                    p[i] = (byte)(p[i] + (((cr - p[i]) * a) >> 8));
                    p[i + 1] = (byte)(p[i + 1] + (((cg - p[i + 1]) * a) >> 8));
                    p[i + 2] = (byte)(p[i + 2] + (((cb - p[i + 2]) * a) >> 8));
                    p[i + 3] = (byte)(p[i + 3] + (((255 - p[i + 3]) * a) >> 8));
                }
            }
        }

        public void Disc(float cx, float cy, float r, int rgb, float alpha)
        {
            Cone(cx, cy, r, cx, cy, r, rgb, alpha);
        }

        // A line through points, round at its joints: as thick as "r" all along.
        public void Line(float[] xs, float[] ys, int from, int to, float r, int rgb, float alpha)
        {
            for (int i = from; i < to; i++)
            {
                Cone(xs[i], ys[i], r, xs[i + 1], ys[i + 1], r, rgb, alpha);
            }
        }
    }
}
