using CMAScenes;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length > 0 && (args[0] == "inspect" || args[0] == "inspect-types"))
{
    foreach (string file in Directory.GetFiles(args[1], "*.dll"))
    {
        using var stream = File.OpenRead(file);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata) continue;
        var metadata = pe.GetMetadataReader();
        foreach (var handle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(handle);
            string name = metadata.GetString(type.Name);
            if (args.Length > 2)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(name, args[2], System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
            }
            else if (name != "ClothingItemMap" && name != "ModdingMap" && name != "Language" && name != "Type") continue;
            if (name == "Type" && type.GetDeclaringType().IsNil) continue;
            if (name == "Type" && metadata.GetString(metadata.GetTypeDefinition(type.GetDeclaringType()).Name) != "ClothingItemMap") continue;
            Console.WriteLine(Path.GetFileName(file) + " " + metadata.GetString(type.Namespace) + "." + name);
            if (args[0] == "inspect-types") continue;
            if (type.BaseType.Kind == HandleKind.TypeDefinition) Console.WriteLine("  base " + metadata.GetString(metadata.GetTypeDefinition((TypeDefinitionHandle)type.BaseType).Name));
            if (type.BaseType.Kind == HandleKind.TypeReference) Console.WriteLine("  base " + metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)type.BaseType).Name));
            foreach (var p in type.GetProperties()) { var prop = metadata.GetPropertyDefinition(p); Console.WriteLine("  property " + prop.DecodeSignature(new MetadataNames(), null).ReturnType + " " + metadata.GetString(prop.Name)); }
            foreach (var m in type.GetMethods()) { var method = metadata.GetMethodDefinition(m); var sig = method.DecodeSignature(new MetadataNames(), null); var names = method.GetParameters().Select(p => metadata.GetParameter(p)).Where(p => p.SequenceNumber > 0).ToDictionary(p => (int)p.SequenceNumber, p => metadata.GetString(p.Name)); Console.WriteLine("  method " + sig.ReturnType + " " + metadata.GetString(method.Name) + "(" + string.Join(", ", sig.ParameterTypes.Select((t, i) => t + " " + names.GetValueOrDefault(i + 1))) + ")"); }
            foreach (var f in type.GetFields()) Console.WriteLine("  field " + metadata.GetString(metadata.GetFieldDefinition(f).Name));
        }
    }
    return;
}

int passed = 0;
void Check(string name, Action test)
{
    try { test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception ex) { Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); Environment.ExitCode = 1; }
}
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected '{expected}', got '{actual}'"); }

Check("author credits preserve names and remove duplicates", () => {
    Equal("Alice, Bob", ClothingCardInfo.Authors(new[] { " Alice ", null, "", "alice", "Bob" }));
    Equal("", ClothingCardInfo.Authors(null));
});
Check("preview fitting preserves portrait and landscape proportions", () => {
    var wide = ClothingCardInfo.Fit(400, 200, 80, 80); Equal(80f, wide.Width); Equal(40f, wide.Height); Equal(20f, wide.Y);
    var tall = ClothingCardInfo.Fit(100, 200, 80, 80); Equal(40f, tall.Width); Equal(80f, tall.Height); Equal(20f, tall.X);
});
Check("preview jobs are deduplicated and can refresh", () => {
    var jobs = new PreviewJobs<string>(); jobs.Request("a", "first"); jobs.Request("a", "duplicate"); jobs.Request("b", "second");
    Equal(true, jobs.Take(out var key, out var item)); Equal("a", key); Equal("first", item);
    Equal(true, jobs.Take(out key, out item)); Equal("b", key);
    Equal(false, jobs.Take(out key, out item)); jobs.Request("a", "again"); Equal(false, jobs.Take(out key, out item));
    jobs.Clear(); jobs.Request("a", "new"); Equal(true, jobs.Take(out key, out item)); Equal("new", item);
});
Check("preview cache keys distinguish item versions and addresses", () => {
    var a = ClothingCardInfo.CacheKey("a/b", "address", "1");
    Equal(64, a.Length); Equal(false, a == ClothingCardInfo.CacheKey("a/b", "address", "2"));
    Equal(false, a == ClothingCardInfo.CacheKey("a/b", "other", "1")); Equal(false, a.Contains('/'));
});
Check("evicted previews can be requested again", () => {
    var jobs = new PreviewJobs<string>(); jobs.Request("a", "first"); jobs.Take(out _, out _);
    jobs.Forget("a"); jobs.Request("a", "reloaded");
    Equal(true, jobs.Take(out var key, out var item)); Equal("a", key); Equal("reloaded", item);
});
Check("png preflight rejects oversized or invalid images", () => {
    var data = new byte[24]; byte[] magic = { 137, 80, 78, 71, 13, 10, 26, 10 }; magic.CopyTo(data, 0);
    data[12] = (byte)'I'; data[13] = (byte)'H'; data[14] = (byte)'D'; data[15] = (byte)'R';
    data[18] = 1; data[22] = 1; Equal(true, ClothingCardInfo.SafePng(data));
    data[16] = 1; Equal(false, ClothingCardInfo.SafePng(data)); Equal(false, ClothingCardInfo.SafePng(new byte[24]));
});

Check("outfit names suppress shortcuts", () => {
    Equal(true, UiInput.IsTextControl("outfit")); Equal(true, UiInput.IsTextControl("search"));
    Equal(false, UiInput.IsTextControl("")); Equal(false, UiInput.IsTextControl(null));
});

Check("selection remains stable when distances reorder", () => {
    var s = new CharacterSelection(); s.Reconcile(new long[] { 1, 2 }); Equal(true, s.Select(2));
    s.Reconcile(new long[] { 1, 2 }); Equal<long?>(2, s.SelectedKey);
    Equal(false, s.Select(99)); Equal<long?>(2, s.SelectedKey);
});
Check("selection follows removal and empty scene", () => {
    var s = new CharacterSelection(); s.Reconcile(new long[] { 1, 2 });
    s.Reconcile(new long[] { 2 }); Equal<long?>(2, s.SelectedKey);
    s.Reconcile(Array.Empty<long>()); Equal<long?>(null, s.SelectedKey);
});
Check("selection has no hidden character limit", () => {
    var s = new CharacterSelection(); s.Reconcile(Enumerable.Range(1, 200).Select(i => (long)i).ToArray());
    Equal(true, s.Select(200)); Equal<long?>(200, s.SelectedKey);
});
Check("delayed wardrobe changes retain each target and snapshot", () => {
    var q = new PendingClothing<object>(); var a = new object(); var b = new object();
    q.Schedule(a, 1f, new ClothingChoice("topA", "bottomA", "#FF0000", true));
    q.Schedule(b, 2f, new ClothingChoice("topB", "bottomB", "#0000FF", false));
    Equal(0, q.TakeReady(0.5f).Count);
    var first = q.TakeReady(1.5f); Equal(1, first.Count); Equal(a, first[0].Target); Equal("topA", first[0].Choice.Top);
    var second = q.TakeReady(2f); Equal(b, second[0].Target); Equal(false, second[0].Choice.Solid);
});
Check("canceling one wardrobe change preserves other targets", () => {
    var q = new PendingClothing<object>(); var a = new object(); var b = new object();
    q.Schedule(a, 1f, new ClothingChoice("a", "", "#FF0000", true));
    q.Schedule(b, 1f, new ClothingChoice("b", "", "#0000FF", true));
    q.Cancel(a); Equal(false, q.Has(a)); Equal(true, q.Has(b));
    var ready = q.TakeReady(2f); Equal(1, ready.Count); Equal(b, ready[0].Target);
    Equal(0, q.TakeReady(3f).Count);
});
Check("queued color update affects selected target only", () => {
    var q = new PendingClothing<object>(); var a = new object(); var b = new object();
    q.Schedule(a, 1f, new ClothingChoice("a", "", "#FF0000", true));
    q.Schedule(b, 1f, new ClothingChoice("b", "", "#0000FF", false));
    q.UpdateColor(a, "#00FF00");
    var ready = q.TakeReady(1f);
    Equal("#00FF00", ready.Single(c => c.Target == a).Choice.Color);
    Equal("#0000FF", ready.Single(c => c.Target == b).Choice.Color);
});
Check("item selection toggles without restricting clothing combinations", () => {
    var choice = new ClothingChoice(new[]{"dress", "bra", "glasses"}, "#FF0000", true);
    var selected = choice.Pick("hoodie"); Equal(4, selected.Items.Count);
    Equal(false, selected.Pick("dress").Items.Contains("dress")); Equal(3, choice.Items.Count);
});
Check("material choices are independent snapshots", () => {
    var source = new Dictionary<string, Dictionary<int,string>> { ["top"] = new() { [0] = "cotton" } };
    var choice = new ClothingChoice(new[]{"top"}, "#FFFFFF", true, source);
    source["top"][0] = "silk"; Equal("cotton", choice.Materials["top"][0]);
    var next = choice.WithColor("#000000"); next.Materials["top"][0] = "silk";
    Equal("cotton", choice.Materials["top"][0]);
});
Check("exposed dress variants have distinct readable labels", () => {
    Equal("Leopard Dress", ClothingCatalog.VariantName("Leopard Dress", "leoparddress", false));
    Equal("Leopard Dress · open bust, open crotch", ClothingCatalog.VariantName("Leopard Dress", "leoparddressexposebreastexposecrotch", false));
});

Check("known clothing names in both languages", () => {
    Equal("Bikini top", ClothingCatalog.DisplayName("bikinitop", "clothes_bikinitop", "bikinitop", false));
    Equal("Верх бикини", ClothingCatalog.DisplayName("bikinitop", "clothes_bikinitop", "bikinitop", true));
});
Check("readable game name takes priority", () => Equal("Ocean Blue", ClothingCatalog.DisplayName("bikinitop", "Ocean Blue", "technical", true)));
Check("unknown name cleanup preserves digits", () => Equal("Ocean Blue 02", ClothingCatalog.DisplayName("other", "", "OceanBlue_02(Clone)", false)));
Check("empty name fallback", () => Equal("Clothing", ClothingCatalog.DisplayName("", null, null, false)));
Check("literal rich text escaped", () => Equal("‹b›Ocean‹/b›", ClothingCatalog.Escape("<b>Ocean</b>")));
Check("categories keep one pieces out of separates", () => {
    Equal(ClothingCategory.OnePiece, ClothingCatalog.Category("swimsuit", "swimsuit"));
    Equal(ClothingCategory.Top, ClothingCatalog.Category("swimsuit", "bikinitop"));
    Equal(ClothingCategory.LowerUnderwear, ClothingCatalog.Category("lowerBodyUnderwear", "tanga"));
    Equal(ClothingCategory.Set, ClothingCatalog.Category("swimsuit", "bikini_set"));
});
Check("unknown items remain discoverable", () => Equal(ClothingCategory.Other, ClothingCatalog.Category("modCustom", "mystery")));
Check("category does not mistake sunset or offset for a set", () => {
    Equal(ClothingCategory.OnePiece, ClothingCatalog.Category("swimsuit", "sunset_swimsuit"));
    Equal(ClothingCategory.Top, ClothingCatalog.Category("swimsuit", "offset_bikinitop"));
});
Check("preferences round trip and reset", () => {
    string dir = Path.Combine(Path.GetTempPath(), "cma-check-" + Guid.NewGuid());
    Directory.CreateDirectory(dir);
    try {
        string file = Path.Combine(dir, "clothing.json");
        var p = new ClothingPreferences(file);
        p.ToggleFavorite("A|Б,2"); p.SaveSet("Ocean", new ClothingChoice("top", "bottom", "#00AAFF", true));
        p.Save();
        var again = new ClothingPreferences(file); again.Load();
        Equal(true, again.IsFavorite("A|Б,2")); Equal(1, again.Sets.Count);
        Equal("#00AAFF", again.Sets[0].Color);
        again.Reset(); again.Save();
        var empty = new ClothingPreferences(file); empty.Load();
        Equal(false, empty.IsFavorite("A|Б,2")); Equal(0, empty.Sets.Count);
    } finally { Directory.Delete(dir, true); }
});
Check("future schema is preserved", () => {
    string file = Path.Combine(Path.GetTempPath(), "cma-check-" + Guid.NewGuid() + ".json");
    try {
        File.WriteAllText(file, "{\"Version\":99,\"Favorites\":[\"future\"]}");
        var p = new ClothingPreferences(file); p.Load(); Equal(false, p.CanSave);
        bool refused = false; try { p.Save(); } catch (InvalidOperationException) { refused = true; }
        Equal(true, refused); Equal(true, File.ReadAllText(file).Contains("99"));
    } finally { File.Delete(file); }
});
Check("malformed preferences fail without replacement", () => {
    string file = Path.Combine(Path.GetTempPath(), "cma-check-" + Guid.NewGuid() + ".json");
    try {
        File.WriteAllText(file, "broken"); var p = new ClothingPreferences(file);
        bool failed = false; try { p.Load(); } catch { failed = true; }
        Equal(true, failed); Equal(false, p.CanSave);
        Equal("broken", File.ReadAllText(file));
    } finally { File.Delete(file); }
});
Check("structurally invalid preference records remain protected", () => {
    string file = Path.Combine(Path.GetTempPath(), "cma-check-" + Guid.NewGuid() + ".json");
    try {
        foreach (string content in new[] {
            "{\"Favorites\":[],\"Sets\":[]}",
            "{\"Version\":1,\"Sets\":[]}",
            "{\"Version\":1,\"Favorites\":[],\"Sets\":[{\"Name\":\"A\",\"Top\":\"x\",\"Bottom\":\"\",\"Color\":\"invalid\"}]}",
            "{\"Version\":1,\"Favorites\":[],\"Sets\":[null]}"
        }) {
            File.WriteAllText(file, content); var p = new ClothingPreferences(file);
            try { p.Load(); } catch (ArgumentException) { } catch (InvalidDataException) { }
            Equal(false, p.CanSave);
            try { p.Save(); } catch (InvalidOperationException) { }
            Equal(content, File.ReadAllText(file));
        }
    } finally { File.Delete(file); }
});
Check("outfits remember pattern visibility", () => {
    string file = Path.Combine(Path.GetTempPath(), "cma-check-" + Guid.NewGuid() + ".json");
    try {
        var p = new ClothingPreferences(file); p.SaveSet("Pattern", new ClothingChoice("top", "", "#FF0000", false)); p.Save();
        var restored = new ClothingPreferences(file); restored.Load(); Equal(false, restored.Sets[0].Solid);
    } finally { File.Delete(file); }
});
Check("explicit accessory types are not outfit tops", () => Equal(ClothingCategory.Accessories, ClothingCatalog.Category("accessories", "bracelet")));
Check("owned resources released exactly once", () => {
    var released = new List<object>(); var pool = new OwnedResources<object>(released.Add);
    object a = new(), b = new(); pool.Add(a); pool.Add(a); pool.Add(b);
    pool.Clear(); pool.Clear(); Equal(2, released.Count); Equal(0, pool.Count);
    pool.Add(a); pool.Clear(); Equal(3, released.Count);
});
Check("release failure does not leak subsequent resources", () => {
    int released = 0; var pool = new OwnedResources<object>(o => { released++; if (released == 1) throw new Exception("failure"); });
    pool.Add(new object()); pool.Add(new object());
    try { pool.Clear(); } catch (AggregateException) { }
    Equal(2, released); Equal(0, pool.Count);
});
Check("saved outfits retain materials independently of selection edits", () => {
    var choice = new ClothingChoice(new[]{"top"}, "#FFFFFF", false,
        new() { ["top"] = new() { [0] = "cotton" } });
    var prefs = new ClothingPreferences("unused.json"); prefs.SaveSet("Cotton", choice);
    choice.Materials["top"][0] = "silk"; Equal("cotton", prefs.Sets[0].Materials["top"][0]);
});
Check("invalid config material slots are ignored", () => {
    var choice = ClothingChoice.FromConfig("top", "", "", "#FFFFFF", false,
        "{\"top\":{\"-1\":\"bad\",\"0\":\"cotton\"},\"broken\":null}");
    Equal(1, choice.Materials["top"].Count); Equal(false, choice.Materials.ContainsKey("broken"));
});
Check("Outfit material controls avoid stripped IL2CPP SelectionGrid", () => {
    string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../UI/Menu.Bikini.cs"));
    string source = File.ReadAllText(path);
    Equal(false, source.Contains("GUILayout.SelectionGrid("));
    Equal(true, source.Contains("GUILayout.Button(names[i]"));
});
Check("solid previews cannot reuse patterned cache entries", () => {
    string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Core/ClothingPreviews.cs"));
    string source = File.ReadAllText(path);
    Equal(true, source.Contains("solid ? \"solid\" : \"pattern\""));
    Equal(true, source.Contains("if (!currentSolid) foreach"));
    Equal(true, source.Contains("currentSolid = request.Solid"));
});
Check("preview cache and loading resolve the same material defaults", () => {
    string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Core/ClothingPreviews.cs"));
    string source = File.ReadAllText(path);
    Equal(2, source.Split("DefaultVariants(map, variants)").Length - 1);
    Equal(true, source.Contains("if (!result.ContainsKey(group.materialIndex))"));
    Equal(true, source.Contains("if (id == requested)"));
});
Check("Outfit loads initialized material slots and reports missing callbacks", () => {
    string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Clothing/Bikini.cs"));
    string source = File.ReadAllText(path);
    Equal(true, source.Contains("ClothingMaterials.Create(id, group.materialIndex, color)"));
    Equal(true, source.Contains("state.loading.Count != 0 || state.restoring.Count != 0"));
    Equal(true, source.Contains("!state.loadStalled && Time.unscaledTime >= state.loadDeadline"));
    Equal(true, source.Contains("state.additions.Clear();"));
});
Check("garment colours are independent and preserve legacy defaults", () => {
    var draft = new ClothingChoice(new[] { "shirt", "skirt" }, "#FFFFFF", false);
    var red = draft.WithPieceColor("shirt", "#FF0000");
    var blue = red.WithPieceColor("skirt", "#0000FF");
    Equal("#FF0000", blue.ColorFor("shirt")); Equal("#0000FF", blue.ColorFor("skirt"));
    Equal("#FFFFFF", draft.ColorFor("shirt")); Equal("#FFFFFF", red.ColorFor("skirt"));
});
Check("style and material edits preserve per-piece colours", () => {
    var draft = new ClothingChoice(new[] { "shirt", "skirt" }, "#FFFFFF", false).WithPieceColor("shirt", "#FF0000");
    var edited = draft.WithSolid(true).WithMaterials(new() { ["shirt"] = new() { [0] = "cotton" } }).Pick("glasses");
    Equal("#FF0000", edited.ColorFor("shirt"));
    edited.Colors["shirt"] = "#0000FF"; Equal("#FF0000", draft.ColorFor("shirt"));
    Equal(false, draft.Pick("shirt").Colors.ContainsKey("shirt"));
});
Check("per-piece config round trip filters invalid colours and stale items", () => {
    var draft = new ClothingChoice(new[] { "shirt", "skirt" }, "#FFFFFF", false).WithPieceColor("shirt", "#FF0000");
    var restored = ClothingChoice.FromConfig(draft.ToConfigItems(), "", "", draft.Color, false, draft.ToConfigMaterials(), draft.ToConfigColors());
    Equal("#FF0000", restored.ColorFor("shirt")); Equal("#FFFFFF", restored.ColorFor("skirt"));
    var invalid = ClothingChoice.FromConfig("shirt", "", "", "#FFFFFF", false, "{}", "{\"shirt\":\"bad\",\"missing\":\"#FF0000\"}");
    Equal(0, invalid.Colors.Count);
    Equal(0, ClothingChoice.FromConfig("shirt", "", "", "#FFFFFF", false, "{}", "broken").Colors.Count);
});
Check("invalid garment colour cannot replace a valid selection", () => {
    var draft = new ClothingChoice(new[] { "shirt" }, "#FFFFFF", false);
    foreach (var invalid in new[] { "bad", "#12345", "#GGGGGG", "#FFFFFFFF", "#FF0000\n" }) {
        bool rejected = false; try { draft.WithPieceColor("shirt", invalid); } catch (ArgumentException) { rejected = true; }
        Equal(true, rejected);
    }
    bool absent = false; try { draft.WithPieceColor("missing", "#FF0000"); } catch (ArgumentException) { absent = true; }
    Equal(true, absent); Equal(0, draft.Colors.Count);
});
Check("saved outfits round trip independent garment colours", () => {
    string file = Path.Combine(Path.GetTempPath(), "cma-check-" + Guid.NewGuid() + ".json");
    try {
        var draft = new ClothingChoice(new[] { "shirt", "skirt" }, "#FFFFFF", false).WithPieceColor("shirt", "#FF0000").WithPieceColor("skirt", "#0000FF");
        var prefs = new ClothingPreferences(file); prefs.SaveSet("Two colours", draft);
        draft.Colors["shirt"] = "#00FF00"; prefs.Save();
        var restored = new ClothingPreferences(file); restored.Load();
        Equal(true, restored.CanSave); Equal("#FF0000", restored.Sets[0].Colors["shirt"]); Equal("#0000FF", restored.Sets[0].Colors["skirt"]);
    } finally { File.Delete(file); }
});
Check("legacy saved outfits retain their original shared colour", () => {
    string file = Path.Combine(Path.GetTempPath(), "cma-check-" + Guid.NewGuid() + ".json");
    try {
        foreach (int version in new[] { 1, 2 }) {
            File.WriteAllText(file, "{\"Version\":" + version + ",\"Favorites\":[],\"Sets\":[{\"Name\":\"Legacy\",\"Top\":\"shirt\",\"Bottom\":\"skirt\",\"Color\":\"#123456\"}]}");
            var prefs = new ClothingPreferences(file); prefs.Load(); Equal(true, prefs.CanSave);
            var outfit = prefs.Sets[0]; var choice = new ClothingChoice(outfit.Items, outfit.Color, outfit.Solid, outfit.Materials, outfit.Colors);
            Equal("#123456", choice.ColorFor("shirt")); Equal("#123456", choice.ColorFor("skirt"));
        }
    } finally { File.Delete(file); }
});
Check("damaged saved garment colours remain write-protected", () => {
    string file = Path.Combine(Path.GetTempPath(), "cma-check-" + Guid.NewGuid() + ".json");
    try {
        string original = "{\"Version\":3,\"Favorites\":[],\"Sets\":[{\"Name\":\"Broken\",\"Items\":[\"shirt\"],\"Color\":\"#FFFFFF\",\"Colors\":{\"shirt\":\"bad\"}}]}";
        File.WriteAllText(file, original); var prefs = new ClothingPreferences(file);
        bool rejected = false; try { prefs.Load(); } catch (InvalidDataException) { rejected = true; }
        Equal(true, rejected); Equal(false, prefs.CanSave); Equal(original, File.ReadAllText(file));
    } finally { File.Delete(file); }
});
Console.WriteLine($"{passed} checks passed; exit code {Environment.ExitCode}");
