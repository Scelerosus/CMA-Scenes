"""Static Outfit regressions; runtime layout and native clothing still need game checks."""
from pathlib import Path
import re

repo = Path(__file__).resolve().parents[1]
source = (repo / "UI" / "Menu.Bikini.cs").read_text(encoding="utf-8-sig")
code = re.sub(r"/\*.*?\*/|//[^\n]*", "", source, flags=re.S)
# Keep character offsets while ignoring braces in C# strings.
masked = re.sub(r'"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'', lambda m: " " * len(m[0]), code)


def method(name):
    match = re.search(r"private static [^\n]+\b" + name + r"\(", masked)
    assert match, f"Missing UI method: {name}"
    begin = masked.index("{", match.end())
    depth = 1
    end = begin + 1
    while depth:
        depth += (masked[end] == "{") - (masked[end] == "}")
        end += 1
    return code[begin:end]


checks = 0


def check(condition, label):
    global checks
    assert condition, label
    checks += 1


check("SelectionGrid(" not in code and "GUIContent.Temp(" not in code, "Use supported IL2CPP widgets")
check("Loc." not in code and not re.search(r"\bT\(", code), "Outfit UI remains in English")
check(code.count("host.ApplyOutfit()") == 1, "One explicit Dress action")
check("host.ApplyOutfit()" in method("DrawOutfitCharacter"), "Dress belongs to character actions")
check("host.RestoreOutfit()" in method("DrawOutfitCharacter") and "host.ToggleBikini()" not in code, "Restore cannot accidentally dress the character")
for name in ("TogglePiece", "StoreDraft", "DrawClothingBrowser", "DrawOutfitEditor", "DrawPieceColor", "DrawMaterialChoices", "DrawOutfits"):
    body = method(name)
    check(not re.search(r"\bhost\.|Bikini\.(?:Set|Retint)\(", body), name + " only edits drafts")

character = method("DrawOutfitCharacter")
check("host.OutfitChanging" in character and "!stalled && !loading" in character, "Prevent duplicate dress requests including queued changes")
check("host.CanRestoreOutfit" in character and "host.RestoreOutfit()" in character, "Restore remains available during queued changes")
controller = (repo / "Core" / "Controller.cs").read_text(encoding="utf-8-sig")
check("internal bool OutfitChanging => Bikini.LoadingSelected() || m_clothingChanges.Has(WardrobeCharacters.Selected);" in controller, "Loading includes the selected character's controller queue")
check("internal bool CanRestoreOutfit => Bikini.CanRestoreSelected() || m_clothingChanges.Has(WardrobeCharacters.Selected);" in controller, "Restore includes the selected character's controller queue")
check("Bikini.ReadWorn(out var wornIds)" in method("DrawBikini"), "Current worn status comes from the native wardrobe")
check("choice.Items.Contains(entry.id)" in method("DrawClothingBrowser") and "wornIds.Contains(entry.id)" in method("DrawClothingBrowser"), "Draft and worn card status stay distinct")
check("wornVariants" in character and "Copy worn clothes into draft" in character, "Copy worn preserves its material choices")
check("selected[group.materialIndex] = variants[pick]" in method("DrawMaterialChoices"), "Material choices preserve native slot indices")
check("ClothingPreviews.Key(entry.map, variants, tint, solid)" in method("DrawClothingPreview"), "Preview cache key includes the selected style")
check("entry.map, variants, tint, solid)" in method("DrawClothingPreview"), "Preview request includes the selected style")
check("96f, selected && choice.Solid)" in method("DrawClothingBrowser"), "Only selected browser pieces use the draft's solid style")
check("128f, choice.Solid)" in method("DrawMaterialChoices"), "Editor preview follows the draft's solid style")
check("StoreDraft(new ClothingChoice(outfit.Items, outfit.Color, outfit.Solid, outfit.Materials, outfit.Colors))" in method("DrawOutfits"), "Saved drafts restore materials and per-piece colours")
check("DrawPieceColor(st, choice, id)" in method("DrawOutfitEditor"), "Colour picker belongs to a selected garment")
check("choice.ColorFor(entry.id)" in method("DrawClothingBrowser") and "choice.ColorFor(entry.id)" in method("DrawMaterialChoices"), "Both previews use their own garment colour")
check("WithPieceColor(id, s_hex)" in method("SetColor") and "Plugin.BikiniColor.Value =" not in method("SetColor"), "Picking a colour cannot change the global default")
check("s_outfitView = 1" in method("DrawClothingBrowser") and "if (!selected) TogglePiece(entry.id)" in method("DrawClothingBrowser"), "Clicking an existing draft piece opens editing without removing it")
check("s_hexItem != id" in method("DrawPieceColor"), "Changing garments resets the hex input")
check("s_deleteOutfit == outfit.Name" in method("DrawOutfits"), "Deleting saved outfits requires a second click")
check("if (s_resetPreferences)" in method("DrawOutfits"), "Resetting saved preferences requires a second click")

catalog = (repo / "Core" / "ClothingCatalog.cs").read_text(encoding="utf-8-sig")
categories = set(re.search(r"enum ClothingCategory\s*\{([^}]+)\}", catalog)[1].replace(" ", "").split(","))
choices = re.search(r"ClothingCategories = \{([^}]+)\}", code)[1]
check(set(re.findall(r"ClothingCategory\.(\w+)", choices)) == categories, "Every clothing category has a browser filter")
check("s_clothingPage = 0" in method("RebuildShown"), "Changing filters resets pagination")
print(f"{checks} static Outfit UI checks passed. Runtime appearance was not checked.")
