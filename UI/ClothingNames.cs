using System;
using Assets.TValle.Tools.Runtime.Moddding.Clothing.Maps;

namespace CMAScenes
{
    internal static class ClothingNames
    {
        // The installed game exposes Language.en only. Russian uses the known-item dictionary and cleaned fallback.
        internal static string Name(ClothingItemMap map)
        {
            string localized = null;
            try
            {
                if (!Loc.Ru) localized = map.GetIngameName(Assets.TValle.Tools.Runtime.Language.en);
            }
            catch { /* Optional localized game names: the catalog still works if a mod map lacks translations. */ }
            if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, map.id, StringComparison.Ordinal)
                && !string.Equals(localized, map.displayId, StringComparison.Ordinal)) return ClothingCatalog.VariantName(localized, map.id, Loc.Ru);
            return ClothingCatalog.VariantName(ClothingCatalog.DisplayName(map.id, map.displayId, map.name, Loc.Ru), map.id, Loc.Ru);
        }
    }
}
