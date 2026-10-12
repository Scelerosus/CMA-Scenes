using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CMAScenes
{
    internal static class ClothingCardInfo
    {
        internal static string Authors(IEnumerable<string> authors)
        {
            var names = new List<string>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (authors != null)
                foreach (string value in authors)
                {
                    string name = Regex.Replace(value ?? "", "\\s+", " ").Trim();
                    if (name.Length > 0 && seen.Add(name)) names.Add(name);
                }
            return string.Join(", ", names);
        }
        internal readonly struct FitBox
        {
            internal readonly float X, Y, Width, Height;
            internal FitBox(float x, float y, float width, float height) { X = x; Y = y; Width = width; Height = height; }
        }
        internal static FitBox Fit(float imageWidth, float imageHeight, float width, float height)
        {
            if (imageWidth <= 0 || imageHeight <= 0 || width <= 0 || height <= 0) return new FitBox(0, 0, 0, 0);
            float scale = Math.Min(width / imageWidth, height / imageHeight);
            float w = imageWidth * scale, h = imageHeight * scale;
            return new FitBox((width - w) * 0.5f, (height - h) * 0.5f, w, h);
        }
        internal static string CacheKey(string id, string address, string version) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { id, address, version, "preview-v2" }))));

        internal static bool SafePng(byte[] data)
        {
            if (data == null || data.Length < 24 || data.Length > 8 * 1024 * 1024) return false;
            byte[] magic = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int i = 0; i < magic.Length; i++) if (data[i] != magic[i]) return false;
            if (data[12] != 'I' || data[13] != 'H' || data[14] != 'D' || data[15] != 'R') return false;
            uint width = ((uint)data[16] << 24) | ((uint)data[17] << 16) | ((uint)data[18] << 8) | data[19];
            uint height = ((uint)data[20] << 24) | ((uint)data[21] << 16) | ((uint)data[22] << 8) | data[23];
            return width > 0 && height > 0 && width <= 4096 && height <= 4096;
        }
    }
    internal sealed class PreviewJobs<T>
    {
        private readonly Queue<(string key, T item)> jobs = new();
        private readonly HashSet<string> requested = new(StringComparer.Ordinal);
        internal void Request(string key, T item) { if (requested.Add(key)) jobs.Enqueue((key, item)); }
        internal bool Take(out string key, out T item)
        {
            if (jobs.Count == 0) { key = null; item = default; return false; }
            var job = jobs.Dequeue(); key = job.key; item = job.item; return true;
        }
        internal void Clear() { jobs.Clear(); requested.Clear(); }
        internal void Forget(string key) { requested.Remove(key); }
    }
}
