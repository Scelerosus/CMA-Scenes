using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CMAScenes
{
    internal sealed class ClothingChoice
    {
        internal readonly List<string> Items;
        internal readonly Dictionary<string, Dictionary<int, string>> Materials;
        internal readonly Dictionary<string, string> Colors;
        internal readonly string Color;
        internal readonly bool Solid;
        internal string Top => Items.Count > 0 ? Items[0] : "";
        internal string Bottom => Items.Count > 1 ? Items[1] : "";

        internal ClothingChoice(IEnumerable<string> items, string color, bool solid, Dictionary<string, Dictionary<int, string>> materials = null, Dictionary<string, string> colors = null)
        {
            Items = new List<string>();
            if (items != null) foreach (string item in items)
                if (!string.IsNullOrWhiteSpace(item) && !Items.Contains(item)) Items.Add(item);
            Color = color;
            Solid = solid;
            Colors = new Dictionary<string, string>(StringComparer.Ordinal);
            if (colors != null) foreach (var piece in colors)
                if (Items.Contains(piece.Key) && ValidColor(piece.Value)) Colors[piece.Key] = piece.Value;
            Materials = new Dictionary<string, Dictionary<int, string>>(StringComparer.Ordinal);
            if (materials != null) foreach (var piece in materials)
            {
                if (string.IsNullOrWhiteSpace(piece.Key) || piece.Value == null) continue;
                var slots = new Dictionary<int, string>();
                foreach (var slot in piece.Value)
                    if (slot.Key >= 0 && !string.IsNullOrWhiteSpace(slot.Value)) slots[slot.Key] = slot.Value;
                Materials[piece.Key] = slots;
            }
        }

        internal ClothingChoice(string top, string bottom, string color, bool solid)
            : this(new[] { top, bottom }, color, solid) { }

        internal ClothingChoice Pick(string id)
        {
            var items = new List<string>(Items);
            if (items.Contains(id)) items.Remove(id); else if (!string.IsNullOrWhiteSpace(id)) items.Add(id);
            return new ClothingChoice(items, Color, Solid, Materials, Colors);
        }

        internal static bool ValidColor(string color) => color?.Length == 7 && System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$");
        internal string ColorFor(string id) => Colors.TryGetValue(id, out var color) ? color : Color;
        internal ClothingChoice WithPieceColor(string id, string color)
        {
            if (!Items.Contains(id) || !ValidColor(color)) throw new ArgumentException("Choose a selected garment and a valid #RRGGBB colour.");
            var colors = new Dictionary<string, string>(Colors, StringComparer.Ordinal) { [id] = color };
            return new ClothingChoice(Items, Color, Solid, Materials, colors);
        }
        internal ClothingChoice WithColor(string color) => new(Items, color, Solid, Materials, Colors);
        internal ClothingChoice WithSolid(bool solid) => new(Items, Color, solid, Materials, Colors);
        internal ClothingChoice WithMaterials(Dictionary<string, Dictionary<int, string>> materials) => new(Items, Color, Solid, materials, Colors);
        internal static ClothingChoice FromConfig(string items, string legacyTop, string legacyBottom, string color, bool solid, string materials, string colors = null)
        {
            var selected = new List<string>();
            if (!string.IsNullOrWhiteSpace(items)) selected.AddRange(items.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            else
            {
                selected.AddRange((legacyTop ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                selected.AddRange((legacyBottom ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
            Dictionary<string, Dictionary<int, string>> variants = null;
            try { variants = JsonSerializer.Deserialize<Dictionary<string, Dictionary<int, string>>>(materials ?? "{}"); }
            catch { }
            Dictionary<string, string> pieceColors = null;
            try { pieceColors = JsonSerializer.Deserialize<Dictionary<string, string>>(colors ?? "{}"); }
            catch { }
            return new ClothingChoice(selected, color, solid, variants, pieceColors);
        }
        internal string ToConfigItems() => string.Join("|", Items);
        internal string ToConfigMaterials() => JsonSerializer.Serialize(Materials);
        internal string ToConfigColors() => JsonSerializer.Serialize(Colors);
    }

    internal sealed class PendingClothing<T> where T : class
    {
        internal sealed class Change { internal T Target; internal float At; internal ClothingChoice Choice; }
        private readonly Dictionary<T, Change> pending = new();
        internal bool Has(T target) => target != null && pending.ContainsKey(target);
        internal void Schedule(T target, float at, ClothingChoice choice)
        { if (target != null) pending[target] = new Change { Target = target, At = at, Choice = choice }; }
        internal void Cancel(T target) { if (target != null) pending.Remove(target); }
        internal void UpdateColor(T target, string color)
        { if (target != null && pending.TryGetValue(target, out var change)) change.Choice = change.Choice.WithColor(color); }
        internal void Clear() => pending.Clear();
        internal IReadOnlyList<Change> TakeReady(float now)
        {
            if (pending.Count == 0) return Array.Empty<Change>();
            var ready = new List<Change>();
            foreach (var change in pending.Values) if (now >= change.At) ready.Add(change);
            foreach (var change in ready) pending.Remove(change.Target);
            return ready;
        }
    }
}
