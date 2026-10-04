using Microsoft.Xna.Framework;

namespace GameClient;

public static class ResourceVerifier
{
    public static void Verify(WorldGraph graph)
    {
        foreach(int seed in new[]{1,2,7319,145223,-9001})
        {
            var maze=new WoodedMaze(seed);
            for(int node=0;node<WoodedMaze.Count;node++)
            {
                var a=MazeResources.At(maze,node);var b=MazeResources.At(new WoodedMaze(seed),node);
                if(!a.SequenceEqual(b)||a.Count!=4||a.Any(r=>MathF.Abs(r.Position.X)<2||MathF.Abs(r.Position.Y)<2))
                    throw new InvalidDataException("Resource generation is unstable or blocks the central path.");
            }
            var starter=MazeResources.At(maze,0);
            if(!starter.Any(r=>r.Kind==ResourceKind.Tree)||!starter.Any(r=>r.Kind==ResourceKind.Rock))
                throw new InvalidDataException("Starter wood or stone is missing.");
        }
        var kinds=Enumerable.Range(0,WoodedMaze.Count).SelectMany(n=>MazeResources.At(graph.Maze,n)).ToArray();
        if(!Enum.GetValues<ResourceKind>().All(kind=>kinds.Any(r=>r.Kind==kind)))
            throw new InvalidDataException("A resource or fruit type never appears in this maze.");
        foreach(var (kind,item) in new[]{(ResourceKind.AppleTree,"apple"),(ResourceKind.BerryTree,"berry"),
            (ResourceKind.PlumTree,"plum"),(ResourceKind.OreRock,"ore")})
        {
            var resource=kinds.First(r=>r.Kind==kind);
            var sample=new WorldState(graph,graph.Maze.MapId(resource.Node),resource.Position+new Vector2(0,.6f));
            if(!sample.UpdateHarvest(true,resource.Duration+.01f,out _)||!sample.GroundDrops.Any(d=>d.ItemId==item))
                throw new InvalidDataException($"{kind} did not drop {item}.");
        }
        var state=new WorldState(graph,"maze:0");
        var tree=MazeResources.At(graph.Maze,0).First(r=>r.Kind==ResourceKind.Tree);
        state.MoveTo(tree.Position+new Vector2(0,.6f));
        state.UpdateHarvest(true,tree.Duration*.5f,out _);
        if(state.HarvestRemaining<=0||state.HarvestFraction>=1)throw new InvalidDataException("Harvest bar did not drain.");
        state.UpdateHarvest(false,0,out _);
        if(state.HarvestRemaining!=0||state.HarvestFraction!=1)throw new InvalidDataException("Release did not reset harvesting.");
        state.UpdateHarvest(true,tree.Duration-.01f,out _);
        if(state.DepletedResources.Contains(tree.Id))throw new InvalidDataException("Tree depleted before its duration.");
        if(!state.UpdateHarvest(true,.02f,out _))throw new InvalidDataException("Tree did not deplete at its duration.");
        if(state.GroundDrops.Count!=1||state.Inventory.Bag.Count("wood")!=0)
            throw new InvalidDataException("Harvest did not leave separate visible loot.");
        var drop=state.GroundDrops[0];state.MoveTo(drop.Position);
        if(!state.TryPickupDrop(out _)||state.Inventory.Bag.Count("wood")!=3||state.GroundDrops.Count!=0)
            throw new InvalidDataException("Wood pickup failed.");
        var rock=MazeResources.At(graph.Maze,0).First(r=>r.Kind==ResourceKind.Rock);
        if(rock.Duration<=tree.Duration)throw new InvalidDataException("Mining must take longer than chopping.");
        state.MoveTo(rock.Position+new Vector2(0,.6f));
        state.UpdateHarvest(true,rock.Duration+.01f,out _);
        if(!state.DepletedResources.Contains(rock.Id)||state.GroundDrops.Count!=1)
            throw new InvalidDataException("Rock depletion or stone drop failed.");
        var saved=state.Snapshot();
        var restored=new WorldState(graph,saved.MapId,new Vector2(saved.X,saved.Z),saved);
        if(!restored.DepletedResources.SetEquals(state.DepletedResources)||!restored.GroundDrops.SequenceEqual(state.GroundDrops))
            throw new InvalidDataException("Resources changed on reload.");
        var stoneDrop=restored.GroundDrops[0];restored.MoveTo(stoneDrop.Position);
        foreach(var id in ItemCatalog.All.Where(i=>i.Id!="stone"&&!i.Slot.HasValue).Select(i=>i.Id))
            restored.Inventory.Bag.TryAdd(id,1);
        for(int i=0;restored.Inventory.Bag.Stacks.Count<InventoryState.BagCapacity;i++)
            restored.Inventory.Bag.TryAdd(ItemCatalog.All.Where(item=>item.MaxStack==1).ElementAt(i%8).Id,1);
        if(restored.TryPickupDrop(out _)||restored.GroundDrops.Count!=1)
            throw new InvalidDataException("Full bag consumed a ground drop.");
        var save=restored.Snapshot();
        string path=Path.Combine(Path.GetTempPath(),$"resource-verify-{Guid.NewGuid():N}.json");
        try
        {
            save.Write(path);var loaded=MazeSave.Load(path);
            var again=new WorldState(graph,loaded.MapId,new Vector2(loaded.X,loaded.Z),loaded);
            if(again.GroundDrops.Count!=1||again.DepletedResources.Count!=2)
                throw new InvalidDataException("Drop or depletion lost through disk save.");
            again.MoveTo(new Vector2(-5.15f,0));again.TryInteract(out _);
            if(again.GroundDrops.Count!=1)throw new InvalidDataException("Map change lost the ground drop.");
        }
        finally{if(File.Exists(path))File.Delete(path);}
        Console.WriteLine("Verified seeded resources across five seeds, starter wood and stone, type durations, release reset, depletion, separate drops, full bag, map change and disk reload.");
    }
    static bool SetEquals(this IReadOnlySet<string> set,IReadOnlySet<string> other)=>set.Count==other.Count&&set.All(other.Contains);
}
