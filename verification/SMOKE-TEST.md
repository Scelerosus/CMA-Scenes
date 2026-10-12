# In-game checks still required

The standalone checks do not load Unity or exercise the native wardrobe coroutines. These checks are pending, not passed.

1. In an ordinary clothed scene, open Clothing and check that multiple nearby characters are listed without requiring supported pose bones. Check the 8 m default boundary and change the search radius in Settings.
2. Equip a selected outfit on character A, select character B and equip a different outfit/color. Confirm A stays unchanged. Change selections during the half-second reapply delay; verify both targets keep their intended outfit and color.
3. Type an outfit name containing hotkey letters; confirm no shortcuts activate. Save, close, reopen and select the outfit. Check color and pattern visibility.
4. Select a one-piece, then separate top/bottom. Check no duplicate garment or conflicting whole garment remains. Confirm already-worn selected items survive returning to the original outfit.
5. Browse more than 250 installed items using pages. Search, toggle favorites, switch languages and refresh. Unknown accessory slots should remain browsable without being applied to a body slot.
6. Change brightness repeatedly and inspect allocations. Change scene/remove characters with the menu open. Check borrowed game fonts remain intact and the menu opens normally afterward.
7. Back up only the cosmetic preferences file and test unsupported schema and invalid outfit records. Confirm favorites cannot overwrite that file until explicit reset. Restore the backup afterward.
8. Use the native wardrobe while a temporary outfit is active; inspect whether asynchronous additions and original-outfit restoration preserve manual changes. This path is not certified by the code review.
9. Compare frame time and allocations against the original installed build in the same clothed scene and lighting, with the menu open and closed.
10. Check author credits against clothing metadata and search by author. Confirm long names are readable via tooltip and missing author names are marked.
11. Browse garments, shoes and accessories. Confirm previews show only the selected item, correct geometry and texture, one LOD and no inactive alternatives. Close during asset loading/rendering and reopen; confirm previews recover without leaked cameras, render targets, materials or Addressables handles. Verify missing models/shaders show an icon and log a bounded failure.
12. Reopen the catalog and verify PNG disk cache reuse. Browse more than 96 previews to exercise eviction. Corrupt a cached PNG and confirm regeneration. Confirm the scene lighting and live character outfits are unaffected.

Build is isolated from the original dirty checkout. Before replacing the installed DLL, close the game and preserve the previous DLL as a backup; do not replace the user's original source modifications with this branch.
