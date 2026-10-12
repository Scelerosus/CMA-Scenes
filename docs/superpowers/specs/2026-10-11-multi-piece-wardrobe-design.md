# Multi-piece wardrobe, material variants, and type sorting

Date: 2026-10-11
Project: CMAScenes 0.3.37
Status: Implemented in 0.3.37; live in-game verification pending

## Goal

Extend the Clothing/Outfit page so users can select and wear multiple installed garments, including items currently classified as `Other`. The existing flow stores only Top and Bottom and refuses `Other`, even though the game exposes additional clothing types. Keep the existing wardrobe restoration behavior and old saved outfits usable.

## Agreed behavior

- Present selectable clothing by the actual type supplied by `ClothingItemMap.Type` (including upper/lower body, underwear accessories, shoes, jackets, swimsuits, accessories, glasses, socks, gloves, and hats). Keep inferred One-piece and Set groupings where useful for the catalog.
- Let users add and remove multiple garment IDs independently, including several items that share a type when the game wardrobe allows them together.
- Apply the selected set through the game's wardrobe manager. Determine conflicts from the game's coverage flags and item type, preserve original material selections before removing original garments, and restore those originals when the selected set is taken off or changed.
- Add a Random colour action that chooses a visible, usable tint and applies it to the selected garments, respecting the existing plain-colour/pattern option.
- Add a Random material action that chooses from each garment's game-provided material variants per material slot. Store the chosen IDs so reapply and saved outfits remain stable.
- Saved outfits contain all selected garment IDs, their material selections, tint, and current plain-colour setting. Load existing Top/Bottom outfit records and legacy config values without losing them.
- Improve sorting: retain A→Z and Z→A; make By type use a curated, stable order of game types, then sort names A→Z within each type. Do not sort types by raw enum spelling.

## UI

- Replace the two fixed Top/Bottom pick rows with a selected-garment list. Each selected garment has a clear/remove action; the catalog remains searchable and filterable by type.
- Use actual type names for filters and item labels. Keep One-piece and Set as useful derived group filters; an item can still be selected from its actual type.
- Keep favorite filtering and pagination. The By type option groups items by the curated type order and then name.
- Put Random colour beside the existing colour controls and Random material beside the garment/material controls. Either action applies immediately to the selected character when the set is worn.

## Persistence and compatibility

- Extend the clothing preferences JSON schema to store generalized outfit items and material IDs. Read version 1 records by translating Top and Bottom into the new item list; preserve favorites and user data.
- Keep legacy `Bikini/Top` and `Bikini/Bottom` keys readable and map them to the corresponding selected items on startup. Do not silently overwrite the preferences file if it cannot be parsed or migrated.

## Implementation boundaries

- Generalize `ClothingChoice`, delayed reapply, `Bikini.PutOn`, and `Bikini.TakeOff` to handle a list of selected garments and material slots.
- Keep character targeting, wardrobe selection, original-garment snapshots, and async add/remove callbacks scoped to the selected character.
- Derive random material options from the selected `ClothingItemMap` material data, not from renderer runtime state.
- Do not change the X-Ray, pose, or scene systems as part of this feature.

## Acceptance criteria

1. Users can select and wear multiple garments, including every current `Other` game type.
2. Taking the set off restores the character's pre-existing clothes and material variants.
3. Changing a selected garment while worn reapplies the set without leaving duplicate or missing original garments.
4. Random colour changes the selected pieces; random material chooses valid item variants per material slot.
5. Saved outfits persist all selections and random material choices; v1 outfits still load.
6. Type sorting is deterministic, readable, and alphabetic within each type; A→Z and Z→A continue to work.
7. Existing wardrobe selection, search, favorites, and pagination continue to work.
