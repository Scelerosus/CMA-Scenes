using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CMAScenes
{
    internal sealed class ClothingPreferences
    {
        internal sealed class Outfit
        {
            public string Name { get; set; }
            public List<string> Items { get; set; } = new();
            public Dictionary<string, Dictionary<int, string>> Materials { get; set; } = new();
            public Dictionary<string, string> Colors { get; set; } = new();
            public string Top { get; set; }
            public string Bottom { get; set; }
            public string Color { get; set; }
            public bool Solid { get; set; } = true;
        }
        public sealed class Document { public int Version { get; set; } public List<string> Favorites { get; set; } public List<Outfit> Sets { get; set; } }
        private readonly string path;
        private readonly HashSet<string> favorites = new(StringComparer.Ordinal);
        private readonly List<Outfit> sets = new();
        internal bool CanSave { get; private set; } = true;
        internal IReadOnlyList<Outfit> Sets => sets;
        internal int FavoriteCount => favorites.Count;
        internal ClothingPreferences(string path) { this.path = path; }
        internal bool IsFavorite(string id) => id != null && favorites.Contains(id);
        internal void ToggleFavorite(string id) { if (!string.IsNullOrWhiteSpace(id) && !favorites.Remove(id)) favorites.Add(id); }
        internal void SaveSet(string name, ClothingChoice choice)
        {
            var outfit = Validated(new Outfit { Name = name, Items = new List<string>(choice.Items), Materials = choice.Materials,
                Top = choice.Top, Bottom = choice.Bottom, Color = choice.Color, Solid = choice.Solid, Colors = choice.Colors });
            int at = sets.FindIndex(s => string.Equals(s.Name, outfit.Name, StringComparison.OrdinalIgnoreCase));
            if (at < 0) sets.Add(outfit); else sets[at] = outfit;
        }
        private static Outfit Validated(Outfit value)
        {
            if (value == null) throw new InvalidDataException("Incomplete outfit record.");
            value.Items ??= new List<string>();
            value.Materials ??= new Dictionary<string, Dictionary<int, string>>();
            value.Colors ??= new Dictionary<string, string>();
            if (value.Items.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(value.Top)) value.Items.Add(value.Top);
                if (!string.IsNullOrWhiteSpace(value.Bottom) && !value.Items.Contains(value.Bottom)) value.Items.Add(value.Bottom);
            }
            string name = (value.Name ?? "").Trim();
            if (name.Length == 0 || name.Length > 60 || value.Items.Count == 0) throw new ArgumentException("Choose an outfit name and at least one item.");
            if (!ClothingChoice.ValidColor(value.Color)) throw new ArgumentException("Invalid outfit color.");
            foreach (string id in value.Items) if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("Invalid clothing item ID.");
            var colors = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var piece in value.Colors)
            {
                if (string.IsNullOrWhiteSpace(piece.Key) || !ClothingChoice.ValidColor(piece.Value)) throw new InvalidDataException("Invalid garment colour.");
                if (value.Items.Contains(piece.Key)) colors.Add(piece.Key, piece.Value);
            }
            var materials = new Dictionary<string, Dictionary<int, string>>(StringComparer.Ordinal);
            foreach (var piece in value.Materials)
            {
                if (string.IsNullOrWhiteSpace(piece.Key) || piece.Value == null) throw new InvalidDataException("Invalid material selection.");
                var slots = new Dictionary<int, string>();
                foreach (var slot in piece.Value)
                {
                    if (slot.Key < 0 || string.IsNullOrWhiteSpace(slot.Value)) throw new InvalidDataException("Invalid material slot.");
                    slots.Add(slot.Key, slot.Value);
                }
                materials.Add(piece.Key, slots);
            }
            return new Outfit { Name = name, Items = new List<string>(value.Items), Materials = materials, Colors = colors, Top = value.Top ?? "", Bottom = value.Bottom ?? "", Color = value.Color, Solid = value.Solid };
        }
        internal void DeleteSet(string name) => sets.RemoveAll(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        internal void Reset() { favorites.Clear(); sets.Clear(); CanSave = true; }
        internal void Load()
        {
            favorites.Clear(); sets.Clear(); CanSave = false;
            if (!File.Exists(path)) { CanSave = true; return; }
            var doc = JsonSerializer.Deserialize<Document>(File.ReadAllText(path));
            if (doc == null || (doc.Version != 1 && doc.Version != 2 && doc.Version != 3) || doc.Favorites == null || doc.Sets == null) return;
            var validSets = new List<Outfit>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Outfit set in doc.Sets) { var valid = Validated(set); if (!names.Add(valid.Name)) throw new InvalidDataException("Duplicate outfit names."); validSets.Add(valid); }
            foreach (string id in doc.Favorites) if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("Invalid favorite ID.");
            foreach (string id in doc.Favorites) favorites.Add(id);
            sets.AddRange(validSets); CanSave = true;
        }
        internal void Save()
        {
            if (!CanSave) throw new InvalidOperationException("Unsupported or damaged clothing preferences; reset explicitly to replace them.");
            string directory = Path.GetDirectoryName(Path.GetFullPath(path)); Directory.CreateDirectory(directory);
            string temp = Path.Combine(directory, ".clothing-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                var ids = new List<string>(favorites); ids.Sort(StringComparer.Ordinal);
                File.WriteAllText(temp, JsonSerializer.Serialize(new Document { Version = 3, Favorites = ids, Sets = sets }, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temp, path, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
