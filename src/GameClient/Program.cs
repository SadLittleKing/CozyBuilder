using Game.Assets;
using GameClient;

try
{
    string? modelPath = null, configPath = null, screenshotPath = null, terrainPath = null;
    string? dogModelPath=null;
    string dogConfigPath="dog.json";
    string? dogScent=null;
    string? homesteadPath = null, interiorsPath = null;
    string savePath = "maze-save.json";
    string startMap = "village";
    string? previewFruit=null;
    int screenshotAfter=0;
    bool previewInventory=false,previewChest=false,previewEquipped=false,previewIronGear=false,previewIconItems=false;
    int mazeSeed=7319;
    bool inspect = false, verifyVillage = false, verifyMaps = false, verifyMaze=false, verifyDog=false, verifyInventory=false, verifyResources=false, overview = false, mazeMap=false,newMaze=false,explicitMap=false,previewHarvest=false,previewDrops=false;
    bool dogHelmet=false,dogArmor=false,dogBackpack=false,noDogCollar=false;
    for (int i = 0; i < args.Length; i++)
        switch (args[i])
        {
            case "--model": modelPath = args[++i]; break;
            case "--config": configPath = args[++i]; break;
            case "--dog-model": dogModelPath=args[++i]; break;
            case "--dog-config": dogConfigPath=args[++i]; break;
            case "--dog-scent": dogScent=args[++i]; break;
            case "--dog-helmet": dogHelmet=true; break;
            case "--dog-armor": dogArmor=true; break;
            case "--dog-backpack": dogBackpack=true; break;
            case "--no-dog-collar": noDogCollar=true; break;
            case "--verify-dog": verifyDog=true; inspect=true; break;
            case "--verify-inventory": verifyInventory=true; inspect=true; break;
            case "--verify-resources": verifyResources=true; inspect=true; break;
            case "--preview-harvest": previewHarvest=true; break;
            case "--preview-drops": previewDrops=true; break;
            case "--preview-fruit": previewFruit=args[++i]; break;
            case "--screenshot": screenshotPath = args[++i]; break;
            case "--screenshot-after": screenshotAfter=int.Parse(args[++i]); break;
            case "--preview-inventory":previewInventory=true;break;
            case "--preview-chest":previewChest=true;break;
            case "--preview-equipped":previewEquipped=true;break;
            case "--preview-iron-gear":previewIronGear=true;break;
            case "--preview-icon-items":previewIconItems=true;break;
            case "--inspect": inspect = true; break;
            case "--terrain": terrainPath = args[++i]; break;
            case "--homestead": homesteadPath = args[++i]; break;
            case "--interiors": interiorsPath = args[++i]; break;
            case "--map": startMap = args[++i]; explicitMap=true; break;
            case "--seed": mazeSeed=int.Parse(args[++i]); break;
            case "--save": savePath=args[++i]; break;
            case "--new-maze": newMaze=true; break;
            case "--maze-map": mazeMap=true; break;
            case "--verify-maze": verifyMaze=true; inspect=true; break;
            case "--verify-village": verifyVillage = true; inspect = true; break;
            case "--verify-maps": verifyMaps = true; inspect = true; break;
            case "--overview": overview = true; break;
            default: throw new ArgumentException("Usage: dotnet run --project src/GameClient -- [--config character.json] [--map village|homestead|interior:id|maze:n] [--seed number] [--save path] [--new-maze] [--dog-scent explore|home|shrine|rare|common] [--verify-maps|--verify-maze|--verify-dog|--verify-inventory] [--preview-inventory|--preview-chest|--preview-equipped|--preview-iron-gear] [--screenshot-after frames] [--screenshot file.png]");
        }

    modelPath ??= new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
        .Select(folder => Path.Combine(folder,"assets","villager","villager_modular.glb"))
        .FirstOrDefault(File.Exists);
    if (modelPath == null) throw new FileNotFoundException("No modular villager GLB found. Supply --model path/to/villager_modular.glb.");
    dogModelPath ??= new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
        .Select(folder=>Path.Combine(folder,"assets","dog","dog_modular.glb")).FirstOrDefault(File.Exists);
    if(dogModelPath==null)throw new FileNotFoundException("No modular dog GLB found. Run assets/dog/generate_dog.py with Blender.");
    terrainPath ??= new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
        .Select(folder => Path.Combine(folder,"assets","village","settlement.json"))
        .FirstOrDefault(File.Exists);
    if (terrainPath == null) throw new FileNotFoundException("No village layout found. Supply --terrain path/to/settlement.json.");
    string? FindMap(string file) => new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
        .Select(folder => Path.Combine(folder,"assets","village",file)).FirstOrDefault(File.Exists);
    homesteadPath ??= FindMap("homestead.json");
    interiorsPath ??= FindMap("interiors.json");
    if (homesteadPath == null || interiorsPath == null) throw new FileNotFoundException("Missing homestead or interiors JSON in assets/village.");
    if(screenshotAfter<0||screenshotAfter>3600)throw new ArgumentOutOfRangeException(nameof(screenshotAfter));
    configPath ??= File.Exists("character.json") ? "character.json" : null;
    var config = configPath == null ? new CharacterConfiguration() : CharacterConfiguration.Load(configPath);
    var library = GlbScene.Load(modelPath);
    var character = new CharacterAssembly(library,config);
    var dogLibrary=GlbScene.Load(dogModelPath);
    var dogConfig=File.Exists(dogConfigPath)?DogConfiguration.Load(dogConfigPath):new DogConfiguration();
    dogConfig=dogConfig with {Collar=noDogCollar?false:dogConfig.Collar,Helmet=dogHelmet||dogConfig.Helmet,
        Armor=dogArmor||dogConfig.Armor,Backpack=dogBackpack||dogConfig.Backpack};
    var dog=new DogAssembly(dogLibrary,dogConfig);
    MazeSave? save = !newMaze && !inspect && File.Exists(savePath) ? MazeSave.Load(savePath) : null;
    if(save!=null)mazeSeed=save.Seed;
    var graph = WorldGraph.Load(terrainPath,homesteadPath,interiorsPath,mazeSeed);
    if(previewFruit!=null)
    {
        var kind=previewFruit.ToLowerInvariant() switch
        {"apple"=>ResourceKind.AppleTree,"berry"=>ResourceKind.BerryTree,"plum"=>ResourceKind.PlumTree,_=>throw new ArgumentException("Use apple, berry or plum with --preview-fruit.")};
        int fruitNode=Enumerable.Range(0,WoodedMaze.Count).First(n=>MazeResources.At(graph.Maze,n).Any(r=>r.Kind==kind));
        startMap=graph.Maze.MapId(fruitNode);explicitMap=true;
    }
    if(save!=null&&!explicitMap){startMap=save.MapId;}
    var world = new WorldState(graph,startMap,save!=null&&!explicitMap?new Microsoft.Xna.Framework.Vector2(save.X,save.Z):null,save);
    if(previewFruit!=null)
    {
        var resource=MazeResources.At(graph.Maze,int.Parse(world.MapId.AsSpan(5))).First(r=>r.Kind.ToString().StartsWith(previewFruit,StringComparison.OrdinalIgnoreCase));
        world.MoveTo(resource.Position+new Microsoft.Xna.Framework.Vector2(0,.7f));
    }
    if(previewHarvest||previewDrops)
    {
        var resource=world.NearbyResource(100)??throw new ArgumentException("Preview requires a maze map with resources.");
        world.MoveTo(resource.Position+new Microsoft.Xna.Framework.Vector2(0,.7f));
        world.UpdateHarvest(true,previewHarvest?resource.Duration*.52f:resource.Duration+.01f,out _);
    }
    if(previewChest)
    {
        if(world.MapId!="interior:residence"||!world.MoveTo(new Microsoft.Xna.Framework.Vector2(-2.35f,1.8f)))
            throw new ArgumentException("--preview-chest requires --map interior:residence.");
    }
    if(previewEquipped)
        foreach(string id in new[]{"padded_hood","padded_vest","leather_gloves","trail_boots"})
            if(!world.Inventory.TryEquip(id,out _))throw new InvalidOperationException("Starter equipment unavailable for preview.");
    if(previewIronGear)
    {
        if(world.MapId!="interior:residence")throw new ArgumentException("--preview-iron-gear requires --map interior:residence.");
        const string residenceChest="interior:residence:chest:2";
        foreach(string id in new[]{"iron_helm","iron_cuirass","iron_gauntlets","iron_boots"})
            if(!world.Inventory.TryTransfer(residenceChest,false,id,1,out _)||!world.Inventory.TryEquip(id,out _))
                throw new InvalidOperationException("Iron equipment unavailable for preview.");
    }
    if(previewIconItems)
        foreach(string id in new[]{"rare_seed","crop","wood","stone","ore","apple","berry","plum"})
            if(!world.Inventory.Bag.TryAdd(id,1))throw new InvalidOperationException("Preview bag is full.");
    character.SelectGear(world.Inventory.Visuals);
    if(dogScent!=null)world.SetDogScent(Enum.Parse<DogScentMode>(dogScent,true));
    Console.WriteLine($"GameClient loaded {Path.GetFullPath(modelPath)}: {library.Parts.Count} library parts, {character.VisibleParts.Count} equipped parts; {config}");
    Console.WriteLine($"Trail dog loaded {Path.GetFullPath(dogModelPath)}: {dogLibrary.Parts.Count} named parts; {dogConfig}");
    Console.WriteLine($"Starting in {graph.Title(world.MapId)}. World has {graph.MapIds.Count()} maps and {graph.Portals.Count} directed portals.");
    if (verifyVillage) graph.Village.VerifyWalkability();
    if (verifyMaps) graph.Verify();
    if (verifyMaze) MazeVerifier.Verify(graph);
    if (verifyDog) { dog.VerifyModules(); DogScentVerifier.Verify(graph); }
    var itemModels=new ItemModels();
    if (verifyInventory) InventoryVerifier.Verify(graph,library);
    if (verifyResources) ResourceVerifier.Verify(graph);
    if (!inspect)
    {
        using var game = new ClientGame(character,dog,world,itemModels,terrainPath,homesteadPath,interiorsPath,screenshotPath,overview,
            screenshotPath==null?savePath:null,mazeMap,screenshotPath==null?dogConfigPath:null,screenshotAfter,previewInventory,previewChest,previewHarvest);
        game.Run();
    }
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}
