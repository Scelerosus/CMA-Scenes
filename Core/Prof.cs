using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace CMAScenes
{
    // For debugging: how many milliseconds each part of the plugin takes in a frame, written to the log every 20 seconds
    // (the average per frame, and the worst one), beside the frame time itself, so that a drop in the frame rate can be
    // told apart: the plugin's, or the game's.
    internal static class Prof
    {
        private sealed class Part
        {
            public long started;
            public double total, worst;
            public double thisFrame;
            public int frames;
            // Measured inside another part (listed, but not added to the plugin's total twice).
            public bool nested;
        }

        private static readonly Dictionary<string, Part> s_parts = new Dictionary<string, Part>();
        private static readonly double s_toMs = 1000.0 / Stopwatch.Frequency;
        private static float s_reportAt = -1f;
        private static int s_frames;
        private static double s_frameTotal, s_frameWorst, s_all, s_allWorst;
        private static int s_lastFrame = -1;
        internal static bool Enabled = true;

        internal static void B(string name)
        {
            if (!Enabled)
            {
                return;
            }
            if (!s_parts.TryGetValue(name, out Part p))
            {
                p = new Part { nested = s_inside.Contains(name) };
                s_parts[name] = p;
            }
            p.started = Stopwatch.GetTimestamp();
        }

        internal static void E(string name)
        {
            if (!Enabled || !s_parts.TryGetValue(name, out Part p) || p.started == 0)
            {
                return;
            }
            p.thisFrame += (Stopwatch.GetTimestamp() - p.started) * s_toMs;
            p.started = 0;
        }

        // Once a frame, at its end.
        internal static void Tick()
        {
            if (!Enabled || Time.frameCount == s_lastFrame)
            {
                return;
            }
            s_lastFrame = Time.frameCount;
            float now = Time.unscaledTime;
            if (s_reportAt < 0f)
            {
                s_reportAt = now + 20f;
            }
            double all = 0;
            foreach (Part p in s_parts.Values)
            {
                if (p.thisFrame > 0)
                {
                    p.total += p.thisFrame;
                    p.frames++;
                    if (p.thisFrame > p.worst)
                    {
                        p.worst = p.thisFrame;
                    }
                    // (Only the parts that are not inside another one add up to the plugin's own total.)
                    if (!p.nested)
                    {
                        all += p.thisFrame;
                    }
                    p.thisFrame = 0;
                }
            }
            double frame = Time.unscaledDeltaTime * 1000.0;
            s_frames++;
            s_frameTotal += frame;
            if (frame > s_frameWorst)
            {
                s_frameWorst = frame;
            }
            s_all += all;
            if (all > s_allWorst)
            {
                s_allWorst = all;
            }
            if (now >= s_reportAt)
            {
                s_reportAt = now + 20f;
                Report();
            }
        }

        // Parts measured inside others (they are listed, but not added twice).
        private static readonly HashSet<string> s_inside = new HashSet<string> { "DrawMasks", "FluidPass", "StrandsDraw" };

        private static void Report()
        {
            if (s_frames == 0)
            {
                return;
            }
            var rows = new List<KeyValuePair<string, Part>>(s_parts);
            rows.Sort((a, b) => b.Value.total.CompareTo(a.Value.total));
            var sb = new StringBuilder();
            foreach (var row in rows)
            {
                Part p = row.Value;
                if (p.frames == 0)
                {
                    continue;
                }
                if (sb.Length > 0)
                {
                    sb.Append("; ");
                }
                sb.Append(row.Key).Append(' ').Append((p.total / s_frames).ToString("F2")).Append(" ms (worst ").Append(p.worst.ToString("F1")).Append(')');
                p.total = 0;
                p.worst = 0;
                p.frames = 0;
            }
            double avg = s_frameTotal / s_frames;
            Plugin.ModLog.LogInfo("Perf: over " + s_frames + " frames: the frame " + avg.ToString("F1") + " ms (" + (1000.0 / Math.Max(0.1, avg)).ToString("F0") + " fps, worst " + s_frameWorst.ToString("F0") + " ms); the plugin altogether " + (s_all / s_frames).ToString("F2") + " ms a frame (worst " + s_allWorst.ToString("F1") + "); by part: " + (sb.Length > 0 ? sb.ToString() : "nothing ran") + ".");
            s_frames = 0;
            s_frameTotal = s_frameWorst = s_all = s_allWorst = 0;
        }
    }
}
