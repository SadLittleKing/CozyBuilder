using CharacterEditor;
using Game.Assets;

try
{
    if(args.Contains("--dog",StringComparer.Ordinal))
    {
        DogViewerProgram.Run(args);
        return;
    }
    string? model = null;
    string? screenshot = null;
    bool inspect = false;
    bool verify = false, backView = false;
    string? saveConfig = null;
    var config = new CharacterConfiguration();
    var gear = new GearVisualConfiguration();
    bool verifyGear=false;
    for (int i = 0; i < args.Length; i++)
        switch (args[i])
        {
            case "--model": model = args[++i]; break;
            case "--screenshot": screenshot = args[++i]; break;
            case "--inspect": inspect = true; break;
            case "--verify-swaps": verify = true; inspect = true; break;
            case "--verify-gear": verifyGear=true;inspect=true;break;
            case "--head":gear=gear with {Head=args[++i]};break;
            case "--body-gear":gear=gear with {Body=args[++i]};break;
            case "--gloves":gear=gear with {Gloves=args[++i]};break;
            case "--feet":gear=gear with {Feet=args[++i]};break;
            case "--config": config = CharacterConfiguration.Load(args[++i]); break;
            case "--save-config": saveConfig = args[++i]; break;
            case "--hair": config = config with { Hair = args[++i] }; break;
            case "--outfit": config = config with { Outfit = args[++i] }; break;
            case "--sword": config = config with { Hand = "sword" }; break;
            case "--backpack": config = config with { Back = "backpack" }; break;
            case "--back-view": backView = true; break;
            default: throw new ArgumentException("Usage: dotnet run --project src/CharacterEditor -- [--model file.glb] [--inspect|--verify-swaps|--verify-gear] [--screenshot file.png] [--hair swept|bun] [--outfit sage|rust] [--head none|padded_hood|iron_helm] [--body-gear none|padded_vest|iron_cuirass] [--gloves none|leather_gloves|iron_gauntlets] [--feet none|trail_boots|iron_boots] [--sword] [--backpack] [--dog ...]");
        }
    model ??= new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
        .Select(folder => Path.Combine(folder, "assets", "villager", "villager_modular.glb"))
        .FirstOrDefault(File.Exists);
    if (model == null) throw new FileNotFoundException("No villager GLB found. Supply --model path/to/villager.glb.");
    var scene = GlbScene.Load(model);
    var character = new CharacterAssembly(scene,config);
    character.SelectGear(gear);
    Console.WriteLine($"Loaded {Path.GetFullPath(model)}: {scene.Parts.Count} mesh primitives, {scene.Parts.Sum(p => p.Vertices.Length / 3):N0} triangles. Bounds {scene.Min} to {scene.Max}");
    Console.WriteLine($"Selected {character.VisibleParts.Count} parts: {config}");
    if (saveConfig != null) { config.Save(saveConfig); Console.WriteLine($"Saved character: {Path.GetFullPath(saveConfig)}"); }
    if (verify)
    {
        character.VerifyAllCombinations();
        foreach (var slot in Enum.GetValues<CharacterSlot>())
            if (CharacterHud.Hit(CharacterHud.Row((int)slot).Center) != slot) throw new InvalidDataException("HUD row targets wrong slot.");
        if (CharacterHud.Hit(new Microsoft.Xna.Framework.Point(600,450)) != null) throw new InvalidDataException("Scene click must not swap parts.");
        Console.WriteLine("Verified all four clickable HUD row targets and scene-click exclusion.");
    }
    if(verifyGear)character.VerifyGearVariants();
    if (!inspect)
    {
        using var game = new EditorGame(scene, model, screenshot,config,backView,gear);
        game.Run();
    }
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}
