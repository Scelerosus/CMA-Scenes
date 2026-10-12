using System;
using UnityEngine;

namespace CMAScenes
{
    internal static partial class Menu
    {
        internal static void Release(ShakeController host)
        {
            if (s_open) { s_open = false; host.SetMenuCursor(false); }
            ClothingPreviews.Release();
            SaveIfDirty();
            s_st = null;
            if (s_skin != null) { UnityEngine.Object.Destroy(s_skin); s_skin = null; }
            try { s_ownedTextures.Clear(); } catch (Exception ex) { Plugin.ModLog.LogWarning("Menu texture cleanup: " + ex.Message); }
            tPanel = null;
            try { Icons.Release(); } catch (Exception ex) { Plugin.ModLog.LogWarning("Icon cleanup: " + ex.Message); }
            if (s_osFont != null) UnityEngine.Object.Destroy(s_osFont);
            s_osFont = null;
            s_font = null;
            s_fontTried = false;
            s_gameFont = false;
            s_entries.Clear(); s_shown.Clear(); s_shownKey = "";
            s_tips.Clear();
            s_capture = null; s_typing = false; s_drag = false; s_resize = false;
            s_clothingPreferences = null;
            WardrobeCharacters.Clear();
        }
    }
}
