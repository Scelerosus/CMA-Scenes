using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CMAScenes
{
    internal enum ClothingCategory { All, Top, Bottom, UpperUnderwear, LowerUnderwear, OnePiece, Set, UpperBody, LowerBody, Shoes, Jacket, Accessories, Glasses, Socks, Gloves, Hat, UpperUnderwearAccessories, LowerUnderwearAccessories, Other }

    // Pure catalog logic. IDs are never rewritten; cleaned names are presentation only.
    internal static class ClothingCatalog
    {
        private static readonly Dictionary<string, (string en, string ru)> Known = new(StringComparer.OrdinalIgnoreCase)
        {
            ["bikinitop"] = ("Bikini top", "Верх бикини"),
            ["bikinibot"] = ("Bikini bottom", "Низ бикини"),
            ["bikinibottom"] = ("Bikini bottom", "Низ бикини"),
            ["microbikinitop"] = ("Micro bikini top", "Верх мини-бикини"),
            ["microbikinibot"] = ("Micro bikini bottom", "Низ мини-бикини"),
            ["tanga"] = ("Tanga", "Танга"), ["thong"] = ("Thong", "Стринги"),
            ["gstring"] = ("String briefs", "Стринги"), ["bra"] = ("Bra", "Бюстгальтер"),
            ["bralette"] = ("Bralette", "Бралетт"), ["bandeau"] = ("Bandeau", "Бандо"),
            ["panties"] = ("Briefs", "Трусы"), ["swimsuit"] = ("One-piece swimsuit", "Цельный купальник"),
            ["monokini"] = ("Monokini", "Монокини"), ["bikiniset"] = ("Bikini set", "Комплект бикини")
        };
        private static readonly Regex Camel = new("([a-zа-я0-9])([A-ZА-Я])", RegexOptions.Compiled);
        private static readonly Regex Space = new("\\s+", RegexOptions.Compiled);
        private static readonly Regex Prefix = new("^(?:clothes?|clothing|item|mod)[_ .:/-]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static string Key(string value) => Regex.Replace(Prefix.Replace(value ?? "", ""), "[^\\p{L}\\p{N}]", "").ToLowerInvariant();

        internal static string DisplayName(string id, string display, string assetName, bool ru)
        {
            // A readable display name supplied by the game wins over the fallback dictionary.
            if (!string.IsNullOrWhiteSpace(display) && display.IndexOf(' ') > 0 && !display.Contains('_') && !display.Contains('/') && !display.Contains('.'))
                return display.Trim();
            foreach (string candidate in new[] { display, id, assetName })
                if (Known.TryGetValue(Key(candidate), out var name)) return ru ? name.ru : name.en;
            string raw = !string.IsNullOrWhiteSpace(assetName) ? assetName : !string.IsNullOrWhiteSpace(display) ? display : id;
            string clean = Prefix.Replace((raw ?? "").Replace("(Clone)", ""), "");
            clean = Space.Replace(Camel.Replace(clean, "$1 $2").Replace('_', ' ').Replace('-', ' '), " ").Trim();
            if (clean.Length == 0) return ru ? "Одежда" : "Clothing";
            return char.ToUpperInvariant(clean[0]) + clean.Substring(1);
        }

        internal static string Escape(string text) => (text ?? "").Replace('<', '‹').Replace('>', '›');

        internal static string VariantName(string name, string id, bool ru)
        {
            var parts = new List<string>();
            string key = (id ?? "").ToLowerInvariant();
            if (key.Contains("exposebreast")) parts.Add(ru ? "открытая грудь" : "open bust");
            if (key.Contains("exposeass")) parts.Add(ru ? "открытые ягодицы" : "open back below waist");
            if (key.Contains("exposecrotch")) parts.Add(ru ? "открытый пах" : "open crotch");
            return parts.Count == 0 ? name : name + " · " + string.Join(", ", parts);
        }

        internal static ClothingCategory Category(string type, string rawNames)
        {
            // Explicit game categories override incidental words such as "bra" in "bracelet".
            switch ((type ?? "").ToLowerInvariant())
            {
                case "accessories": return ClothingCategory.Accessories;
                case "glasses": return ClothingCategory.Glasses;
                case "socks": return ClothingCategory.Socks;
                case "shoes": return ClothingCategory.Shoes;
                case "gloves": return ClothingCategory.Gloves;
                case "hat": return ClothingCategory.Hat;
                case "jacket": return ClothingCategory.Jacket;
                case "upperbodyunderwearaccessories": return ClothingCategory.UpperUnderwearAccessories;
                case "lowerbodyunderwearaccessories": return ClothingCategory.LowerUnderwearAccessories;
                case "upperbody": return ClothingCategory.UpperBody;
                case "lowerbody": return ClothingCategory.LowerBody;
                case "upperbodyunderwear": return ClothingCategory.UpperUnderwear;
                case "lowerbodyunderwear": return ClothingCategory.LowerUnderwear;
            }
            string key = Key(rawNames);
            string words = Camel.Replace(rawNames ?? "", "$1 $2").ToLowerInvariant();
            if (Regex.IsMatch(words, "(?:^|[^\\p{L}\\p{N}])(?:set|комплект)(?:$|[^\\p{L}\\p{N}])") || key == "bikiniset") return ClothingCategory.Set;
            if (key.Contains("bottom") || key.Contains("bikinibot") || key.Contains("panty") || key.Contains("panties") || key.Contains("thong") || key.Contains("tanga") || key.Contains("gstring")) return ClothingCategory.Bottom;
            if (key.Contains("top") || key.Contains("bra") || key.Contains("bandeau")) return ClothingCategory.Top;
            if (string.Equals(type, "swimsuit", StringComparison.OrdinalIgnoreCase) || key.Contains("monokini") || key.Contains("swimsuit")) return ClothingCategory.OnePiece;
            return ClothingCategory.Other;
        }
    }
}
