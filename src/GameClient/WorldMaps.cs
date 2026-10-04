using System.Text.Json;
using Microsoft.Xna.Framework;

namespace GameClient;

public sealed class HomesteadLayout
{
    public string Name { get; init; } = "Your Homestead";
    public int Width { get; init; }
    public int Depth { get; init; }
    public int Seed { get; init; }
    public float[] Spawn { get; init; } = [];
    public float[] VillageApproach { get; init; } = [];
    public float[] VillageGate { get; init; } = [];
    public VillageBuilding House { get; init; } = new();
    public VillageRegion Field { get; init; } = new();
    public VillageRoute[] Routes { get; init; } = [];
    public float[][] Plots { get; init; } = [];
    public Vector3 SpawnPosition => new(Spawn[0],0,Spawn[1]);
    public bool IsPath(float x,float z) => Routes.Any(r => r.Contains(x,z));
    public bool CanWalk(float x,float z,float radius=.25f) =>
        x >= -Width/2f+radius && x <= Width/2f-radius &&
        z >= -Depth/2f+radius && z <= Depth/2f-radius && !House.IntersectsDisc(x,z,radius);

    public static HomesteadLayout Load(string path)
    {
        var result=JsonSerializer.Deserialize<HomesteadLayout>(File.ReadAllText(path),new JsonSerializerOptions { PropertyNameCaseInsensitive=true })
            ?? throw new InvalidDataException("Empty homestead layout.");
        result.Validate();
        return result;
    }
    public void Validate()
    {
        if (Width is < 16 or > 128 || Depth is < 16 or > 128 || Width%2!=0 || Depth%2!=0 ||
            Spawn.Length!=2 || VillageApproach.Length!=2 || VillageGate.Length!=2 || House.Approach.Length!=2 ||
            House.Width<=0 || House.Depth<=0 || Field.Width<=0 || Field.Depth<=0 || Plots.Length==0 || Routes.Length==0)
            throw new InvalidDataException("Invalid homestead dimensions, house, field or route data.");
        if (!CanWalk(Spawn[0],Spawn[1]) || !CanWalk(VillageGate[0],VillageGate[1]) ||
            !CanWalk(House.Approach[0],House.Approach[1]) || !IsPath(Spawn[0],Spawn[1]) ||
            !IsPath(VillageGate[0],VillageGate[1]) || !IsPath(House.Approach[0],House.Approach[1]))
            throw new InvalidDataException("Homestead arrival, gate or house approach is not on a walkable path.");
        foreach (var route in Routes)
            if (route.Points.Length<2 || route.Width<=0 || route.Points.Any(p => p.Length!=2 || !CanWalk(p[0],p[1],0)))
                throw new InvalidDataException($"Invalid homestead route: {route.Name}");
        foreach (var plot in Plots)
            if (plot.Length!=2 || !Field.Contains(plot[0],plot[1]) || !CanWalk(plot[0],plot[1]))
                throw new InvalidDataException("Farm plot is outside the walkable field.");
        if (Plots.Select(p => $"{p[0]},{p[1]}").Distinct().Count()!=Plots.Length)
            throw new InvalidDataException("Farm plot positions must be unique.");
    }
    public void VerifyWalkability()
    {
        var seen=new bool[Width,Depth];
        var queue=new Queue<(int X,int Z)>();
        (int X,int Z) Tile(float[] p) => ((int)MathF.Floor(p[0]+Width/2f),(int)MathF.Floor(p[1]+Depth/2f));
        bool Road(int ix,int iz)
        {
            float x=ix-Width/2f+.5f,z=iz-Depth/2f+.5f;
            return IsPath(x,z)&&CanWalk(x,z,0);
        }
        var start=Tile(Spawn); queue.Enqueue(start);seen[start.X,start.Z]=true;
        while(queue.Count>0)
        {
            var (x,z)=queue.Dequeue();
            foreach(var (dx,dz) in new[] {(1,0),(-1,0),(0,1),(0,-1)})
            {
                int nx=x+dx,nz=z+dz;
                if(nx<0||nz<0||nx>=Width||nz>=Depth||seen[nx,nz]||!Road(nx,nz))continue;
                seen[nx,nz]=true;queue.Enqueue((nx,nz));
            }
        }
        foreach(var (label,point) in new[] { ("village gate",VillageGate),("residence",House.Approach) })
        {
            var t=Tile(point);
            if(!seen[t.X,t.Z])throw new InvalidDataException($"Homestead {label} cannot be reached from the arrival path.");
        }
        if(CanWalk(House.X,House.Z)||CanWalk(Width/2f,0))throw new InvalidDataException("House and map edge must block movement.");
        Console.WriteLine($"Verified homestead paths, residence and village gate; {Plots.Length} walkable farm plots.");
    }
}

public sealed class InteriorCatalog
{
    public RoomLayout[] Rooms { get; init; } = [];
    public static InteriorCatalog Load(string path)
    {
        var result=JsonSerializer.Deserialize<InteriorCatalog>(File.ReadAllText(path),new JsonSerializerOptions { PropertyNameCaseInsensitive=true })
            ?? throw new InvalidDataException("Empty interiors catalog.");
        if(result.Rooms.Length==0 || result.Rooms.Select(r=>r.Id).Distinct(StringComparer.Ordinal).Count()!=result.Rooms.Length)
            throw new InvalidDataException("Interior template IDs must be present and unique.");
        foreach(var room in result.Rooms)room.Validate();
        return result;
    }
    public RoomLayout Template(string id) => Rooms.FirstOrDefault(r=>r.Id==id)
        ?? throw new InvalidDataException($"Missing interior template: {id}");
}

public sealed class RoomLayout
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Floor { get; init; } = "warm";
    public float Width { get; init; }
    public float Depth { get; init; }
    public RoomFurniture[] Furniture { get; init; } = [];
    public bool CanWalk(float x,float z,float radius=.25f) =>
        MathF.Abs(x)<=Width/2-.55f-radius && MathF.Abs(z)<=Depth/2-.55f-radius &&
        !Furniture.Any(f=>f.IntersectsDisc(x,z,radius));
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Id)||string.IsNullOrWhiteSpace(Name)||Width<8||Depth<8||
            !CanWalk(0,1.8f)||!CanWalk(0,3.7f))
            throw new InvalidDataException($"Interior {Id} has invalid size, spawn or exit.");
        foreach(var item in Furniture)
            if(string.IsNullOrWhiteSpace(item.Label)||item.Width<=0||item.Depth<=0||item.Height<=0||
                MathF.Abs(item.X)+item.Width/2>Width/2-.45f||MathF.Abs(item.Z)+item.Depth/2>Depth/2-.45f)
                throw new InvalidDataException($"Invalid furniture in {Id}.");
    }
}

public sealed class RoomFurniture
{
    public string Label { get; init; } = "";
    public float X { get; init; }
    public float Z { get; init; }
    public float Width { get; init; }
    public float Depth { get; init; }
    public float Height { get; init; }
    public bool IntersectsDisc(float x,float z,float radius)
    {
        float nx=MathHelper.Clamp(x,X-Width/2,X+Width/2),nz=MathHelper.Clamp(z,Z-Depth/2,Z+Depth/2);
        return (x-nx)*(x-nx)+(z-nz)*(z-nz)<=radius*radius;
    }
}

public sealed record MapPortal(string Id,string From,Vector2 Trigger,string To,Vector2 Arrival,string Label);

public sealed class WorldGraph
{
    public VillageLayout Village { get; }
    public HomesteadLayout Homestead { get; }
    public InteriorCatalog Interiors { get; }
    public WoodedMaze Maze { get; }
    public IReadOnlyList<MapPortal> Portals => portals;
    readonly List<MapPortal> portals=[];
    readonly Dictionary<string,RoomLayout> roomByMap=new(StringComparer.Ordinal);
    readonly Dictionary<string,string> titleByMap=new(StringComparer.Ordinal);
    public IEnumerable<string> MapIds => new[] { "village","homestead" }.Concat(roomByMap.Keys).Concat(Enumerable.Range(0,WoodedMaze.Count).Select(Maze.MapId));
    public Vector2 DungeonGate { get; }

    WorldGraph(VillageLayout village,HomesteadLayout homestead,InteriorCatalog interiors,int mazeSeed)
    {
        Village=village;Homestead=homestead;Interiors=interiors;Maze=new WoodedMaze(mazeSeed);
        var road=village.Routes.Single(r=>r.Name.Contains("dungeon",StringComparison.OrdinalIgnoreCase));
        DungeonGate=P(road.Points[^1]);
        titleByMap["village"]=village.Name;
        titleByMap["homestead"]=homestead.Name;
        foreach(var building in village.Buildings)
        {
            var mapId="interior:"+building.Id;
            roomByMap.Add(mapId,interiors.Template(building.Interior));
            titleByMap.Add(mapId,building.Label+" / "+interiors.Template(building.Interior).Name);
            AddPair("door:"+building.Id,"village",P(building.Approach),mapId,new Vector2(0,1.8f),new Vector2(0,3.7f),
                "ENTER "+building.Label,"EXIT TO VILLAGE");
        }
        roomByMap.Add("interior:residence",interiors.Template("residence"));
        titleByMap.Add("interior:residence","YOUR RESIDENCE");
        AddPair("west-gate","village",P(homestead.VillageApproach),"homestead",P(homestead.Spawn),P(homestead.VillageGate),
            "GO TO HOMESTEAD","RETURN TO VILLAGE");
        AddPair("residence","homestead",P(homestead.House.Approach),"interior:residence",new Vector2(0,1.8f),new Vector2(0,3.7f),
            "ENTER RESIDENCE","EXIT TO HOMESTEAD");
        portals.Add(new("maze:enter","village",DungeonGate,Maze.MapId(0),new Vector2(-3.5f,0),"ENTER WOODED MAZE"));
        portals.Add(new("maze:leave",Maze.MapId(0),WoodedMaze.Gate(-1,0),"village",DungeonGate,"RETURN TO VILLAGE"));
        for(int node=0;node<WoodedMaze.Count;node++)
            foreach(int next in Maze.Neighbors(node))
            {
                var (dx,dz)=Maze.Direction(node,next);
                portals.Add(new($"maze:{node}:{next}",Maze.MapId(node),WoodedMaze.Gate(dx,dz),Maze.MapId(next),
                    WoodedMaze.Arrival(dx,dz),$"GO {DirectionName(dx,dz)}"));
            }
        foreach(var portal in portals)
            if(!CanWalk(portal.From,portal.Trigger,0)||!CanWalk(portal.To,portal.Arrival,.25f))
                throw new InvalidDataException($"Portal {portal.Id} has a blocked trigger or arrival.");
    }
    void AddPair(string id,string exterior,Vector2 outside,string inside,Vector2 interiorSpawn,Vector2 insideExit,string enter,string leave)
    {
        portals.Add(new(id+":enter",exterior,outside,inside,interiorSpawn,enter));
        portals.Add(new(id+":exit",inside,insideExit,exterior,outside,leave));
    }
    static string DirectionName(int dx,int dz)=>dx switch {-1=>"WEST",1=>"EAST",_=>dz<0?"NORTH":"SOUTH"};
    public static WorldGraph Load(string villagePath,string homesteadPath,string interiorsPath,int mazeSeed=7319) =>
        new(VillageLayout.Load(villagePath),HomesteadLayout.Load(homesteadPath),InteriorCatalog.Load(interiorsPath),mazeSeed);
    public RoomLayout Room(string mapId) => roomByMap.TryGetValue(mapId,out var room) ? room : throw new ArgumentException($"No interior map: {mapId}");
    public string Title(string mapId) => WoodedMaze.TryNode(mapId,out int node) ? $"WOODED MAZE / CLEARING {node+1}" :
        titleByMap.TryGetValue(mapId,out var title) ? title : throw new ArgumentException($"Unknown map: {mapId}");
    public bool HasMap(string mapId) => titleByMap.ContainsKey(mapId)||WoodedMaze.TryNode(mapId,out _);
    public float Extent(string mapId) => mapId switch
    {
        "village" => Math.Max(Village.Width,Village.Depth),
        "homestead" => Math.Max(Homestead.Width,Homestead.Depth),
        _ when WoodedMaze.TryNode(mapId,out _) => 14,
        _ => Math.Max(Room(mapId).Width,Room(mapId).Depth)
    };
    public Vector3 Spawn(string mapId) => mapId switch
    {
        "village" => Village.SpawnPosition,
        "homestead" => Homestead.SpawnPosition,
        _ when WoodedMaze.TryNode(mapId,out _) => new Vector3(-3.5f,0,0),
        _ => new Vector3(0,0,1.8f)
    };
    public bool CanWalk(string mapId,Vector2 p,float radius=.25f) => mapId switch
    {
        "village" => Village.CanWalk(p.X,p.Y,radius),
        "homestead" => Homestead.CanWalk(p.X,p.Y,radius),
        _ when WoodedMaze.TryNode(mapId,out _) => MathF.Abs(p.X)<=5.6f-radius && MathF.Abs(p.Y)<=5.6f-radius,
        _ => Room(mapId).CanWalk(p.X,p.Y,radius)
    };
    public IEnumerable<MapPortal> From(string mapId) => portals.Where(p=>p.From==mapId);
    static Vector2 P(float[] p) => new(p[0],p[1]);

    public void Verify()
    {
        Village.VerifyWalkability();
        Homestead.VerifyWalkability();
        foreach(var mapId in MapIds)
        {
            var mesh=WorldTerrain.Build(this,mapId);
            if(mesh.Length==0 || mesh.Length%3!=0)throw new InvalidDataException($"Map {mapId} has invalid terrain geometry.");
        }
        if(portals.Count<(Village.Buildings.Length+3)*2)throw new InvalidDataException("A building or world link is missing.");
        foreach(var building in Village.Buildings)
        {
            var state=new WorldState(this,"village",P(building.Approach));
            if(!state.TryInteract(out _)||state.MapId!="interior:"+building.Id)
                throw new InvalidDataException($"Cannot enter {building.Id}.");
            if(!state.MoveTo(new Vector2(0,3.7f))||!state.TryInteract(out _)||state.MapId!="village"||
                Vector2.Distance(state.Position,P(building.Approach))>.001f)
                throw new InvalidDataException($"Incorrect return point from {building.Id}.");
        }
        var trip=new WorldState(this,"village",P(Homestead.VillageApproach));
        if(!trip.TryInteract(out _)||trip.MapId!="homestead")throw new InvalidDataException("Homestead gate failed.");
        if(!trip.MoveTo(P(Homestead.House.Approach))||!trip.TryInteract(out _)||trip.MapId!="interior:residence")
            throw new InvalidDataException("Residence entry failed.");
        if(!trip.MoveTo(new Vector2(0,3.7f))||!trip.TryInteract(out _)||trip.MapId!="homestead"||
            Vector2.Distance(trip.Position,P(Homestead.House.Approach))>.001f)
            throw new InvalidDataException("Residence return point failed.");
        int plot=0;
        if(!trip.MoveTo(P(Homestead.Plots[plot])))throw new InvalidDataException("Farm plot cannot be reached.");
        for(int i=0;i<4;i++)if(!trip.TryFarm(out _))throw new InvalidDataException("Farm action failed.");
        if(trip.HarvestCount!=1 || trip.PlotStage(plot)!=FarmStage.Tilled)
            throw new InvalidDataException("Till/plant/water/harvest cycle failed.");
        if(!trip.MoveTo(P(Homestead.VillageGate))||!trip.TryInteract(out _)||trip.MapId!="village"||
            Vector2.Distance(trip.Position,P(Homestead.VillageApproach))>.001f)
            throw new InvalidDataException("Village return from homestead failed.");
        if(!trip.MoveTo(P(Homestead.VillageApproach))||!trip.TryInteract(out _)||trip.MapId!="homestead"||
            trip.HarvestCount!=1 || trip.PlotStage(plot)!=FarmStage.Tilled)
            throw new InvalidDataException("Farm state did not survive map transitions.");
        Console.WriteLine($"Verified {MapIds.Count()} renderable maps, {Village.Buildings.Length} enterable village interiors, homestead/residence round trip, exact return positions, collision and persistent-session farm cycle.");
    }
}

public enum FarmStage { Wild, Tilled, Planted, Watered }

public sealed class WorldState
{
    public WorldGraph Graph { get; private set; }
    public string MapId { get; private set; }
    public Vector2 Position { get; private set; }
    public int HarvestCount { get; private set; }
    public bool RareCollected { get; private set; }
    public bool CommonCollected { get; private set; }
    public bool ShrineUnlocked { get; private set; }
    public DogScentMode ScentMode { get; private set; }
    public InventoryState Inventory { get; }
    readonly HashSet<int> discovered=[];
    public IReadOnlyCollection<int> Discovered=>discovered;
    readonly Dictionary<int,FarmStage> plots=[];
    readonly HashSet<string> depletedResources=new(StringComparer.Ordinal);
    readonly List<GroundDrop> groundDrops=[];
    string? harvestTarget;
    float harvestElapsed;
    public IReadOnlySet<string> DepletedResources=>depletedResources;
    public IReadOnlyList<GroundDrop> GroundDrops=>groundDrops;
    public float HarvestRemaining=>NearbyResource() is { } target && harvestTarget==target.Id ? Math.Max(0,target.Duration-harvestElapsed) : 0;
    public float HarvestFraction=>NearbyResource() is { } target && harvestTarget==target.Id ? MathHelper.Clamp(1-harvestElapsed/target.Duration,0,1) : 1;
    public WorldState(WorldGraph graph,string mapId="village",Vector2? start=null,MazeSave? save=null)
    {
        if(!graph.HasMap(mapId))throw new ArgumentException($"Unknown starting map: {mapId}");
        Graph=graph;MapId=mapId;
        Inventory=new InventoryState(save);
        Position=start??new Vector2(graph.Spawn(mapId).X,graph.Spawn(mapId).Z);
        if(!graph.CanWalk(mapId,Position))throw new InvalidDataException($"Blocked starting point in {mapId}.");
        if(save!=null)
        {
            RareCollected=save.RareCollected;CommonCollected=save.CommonCollected;ShrineUnlocked=save.ShrineUnlocked;
            foreach(int node in save.Discovered)discovered.Add(node);
            if(Enum.TryParse<DogScentMode>(save.DogScent,true,out var scent))ScentMode=scent;
            HarvestCount=save.HarvestCount;
            foreach(string id in save.DepletedResources)depletedResources.Add(id);
            groundDrops.AddRange(save.GroundDrops);
            if(save.FarmPlots!=null)foreach(var (index,stage) in save.FarmPlots)
            {
                if(index<0||index>=graph.Homestead.Plots.Length||!Enum.IsDefined(stage))
                    throw new InvalidDataException("Invalid saved farm plot.");
                plots[index]=stage;
            }
        }
        Discover();
        if(!DogScent.Available(this,ScentMode))ScentMode=DogScentMode.Off;
    }
    void Discover(){if(WoodedMaze.TryNode(MapId,out int node))discovered.Add(node);}
    public MazeSave Snapshot()=>new()
    {
        Seed=Graph.Maze.Seed,MapId=MapId,X=Position.X,Z=Position.Y,
        Discovered=discovered.Order().ToArray(),RareCollected=RareCollected,
        CommonCollected=CommonCollected,ShrineUnlocked=ShrineUnlocked,DogScent=ScentMode.ToString(),
        Inventory=Inventory.Bag.Snapshot(),Equipment=Inventory.EquipmentSnapshot(),Chests=Inventory.ChestSnapshot(),
        FarmPlots=new Dictionary<int,FarmStage>(plots),HarvestCount=HarvestCount,
        DepletedResources=depletedResources.Order(StringComparer.Ordinal).ToArray(),GroundDrops=groundDrops.ToList()
    };
    public void CycleDogScent()=>ScentMode=DogScent.Cycle(this);
    public void SetDogScent(DogScentMode mode)
    {
        if(!DogScent.Available(this,mode))throw new InvalidOperationException($"Dog has not learned {mode} scent.");
        ScentMode=mode;
    }
    public FarmStage PlotStage(int index) => plots.GetValueOrDefault(index,FarmStage.Wild);
    public ResourceNode? NearbyResource(float reach=1.45f)
    {
        if(!WoodedMaze.TryNode(MapId,out int node))return null;
        return MazeResources.At(Graph.Maze,node).Where(r=>!depletedResources.Contains(r.Id)&&Vector2.Distance(Position,r.Position)<=reach)
            .OrderBy(r=>Vector2.DistanceSquared(Position,r.Position)).FirstOrDefault();
    }
    public GroundDrop? NearbyDrop(float reach=1.2f)
    {
        if(!WoodedMaze.TryNode(MapId,out int node))return null;
        return groundDrops.Where(d=>d.Node==node&&Vector2.Distance(Position,d.Position)<=reach)
            .OrderBy(d=>Vector2.DistanceSquared(Position,d.Position)).FirstOrDefault();
    }
    public bool TryPickupDrop(out string message)
    {
        var drop=NearbyDrop();
        if(drop==null){message="NO DROP NEARBY";return false;}
        if(!Inventory.Bag.TryAdd(drop.ItemId,drop.Quantity))
        {message="BAG FULL / DROP LEFT ON GROUND";return false;}
        groundDrops.Remove(drop);message=$"PICKED UP {drop.Quantity} {ItemCatalog.Get(drop.ItemId).Name}";return true;
    }
    public bool UpdateHarvest(bool held,float seconds,out string message)
    {
        message="";
        var target=held?NearbyResource():null;
        if(target==null){harvestTarget=null;harvestElapsed=0;return false;}
        if(harvestTarget!=target.Id){harvestTarget=target.Id;harvestElapsed=0;}
        harvestElapsed+=Math.Max(0,seconds);
        if(harvestElapsed<target.Duration)return false;
        depletedResources.Add(target.Id);
        int index=0;
        foreach(var (item,quantity) in target.Loot)
        {
            var offset=index++==0?new Vector2(-.38f,.18f):new Vector2(.38f,-.18f);
            var at=target.Position+offset;
            groundDrops.Add(new GroundDrop($"{target.Id}:{index}",item,quantity,target.Node,at.X,at.Y));
        }
        harvestTarget=null;harvestElapsed=0;
        message=$"{target.Name} CLEARED / PICK UP DROPS WITH F";
        return true;
    }
    public MapPortal? NearbyPortal(float reach=1.35f) => Graph.From(MapId)
        .Where(p=>Vector2.Distance(Position,p.Trigger)<=reach)
        .OrderBy(p=>Vector2.DistanceSquared(Position,p.Trigger)).FirstOrDefault();
    public bool MoveTo(Vector2 destination)
    {
        if(!Graph.CanWalk(MapId,destination))return false;
        Position=destination;return true;
    }
    public void Move(Vector2 step)
    {
        MoveTo(new Vector2(Position.X+step.X,Position.Y));
        MoveTo(new Vector2(Position.X,Position.Y+step.Y));
    }
    public bool TryInteract(out string message)
    {
        if(WoodedMaze.TryNode(MapId,out int node)&&node==Graph.Maze.ShrineNode&&Vector2.Distance(Position,Vector2.Zero)<1.7f)
        {
            ShrineUnlocked=true;MapId="village";Position=Graph.DungeonGate;
            message="SHRINE AWAKENED / RETURNED TO VILLAGE";return true;
        }
        var portal=NearbyPortal();
        if(portal==null){message="NO DOOR OR GATE NEARBY";return false;}
        MapId=portal.To;Position=portal.Arrival;
        Discover();
        message=Graph.Title(MapId);
        return true;
    }
    public bool TryJumpShrine(out string message)
    {
        if(MapId!="village"||!ShrineUnlocked||Vector2.Distance(Position,Graph.DungeonGate)>1.6f)
        {message="UNLOCK THE SHRINE FIRST / STAND AT DUNGEON GATE";return false;}
        MapId=Graph.Maze.MapId(Graph.Maze.ShrineNode);Position=new Vector2(0,2.8f);Discover();
        message="RETURNED TO WOODLAND SHRINE";return true;
    }
    public bool TryCollect(out string message)
    {
        if(!WoodedMaze.TryNode(MapId,out int node)||Vector2.Distance(Position,Vector2.Zero)>1.7f)
        {message="NO SEEDS NEARBY";return false;}
        if(node==Graph.Maze.RareNode&&!RareCollected)
        {
            if(!Inventory.Bag.TryAdd("rare_seed",1)){message="BAG FULL / RARE SEED LEFT HERE";return false;}
            RareCollected=true;message="RARE SEED FOUND / 1";return true;
        }
        if(node==Graph.Maze.CommonNode&&!CommonCollected)
        {
            if(!Inventory.Bag.TryAdd("common_seed",3)){message="BAG FULL / COMMON SEEDS LEFT HERE";return false;}
            CommonCollected=true;message="COMMON SEEDS FOUND / 3";return true;
        }
        message="NOTHING LEFT TO COLLECT";return false;
    }
    public int? NearbyPlot(float reach=1.35f)
    {
        if(MapId!="homestead")return null;
        return Enumerable.Range(0,Graph.Homestead.Plots.Length)
            .Where(i=>Vector2.Distance(Position,new(Graph.Homestead.Plots[i][0],Graph.Homestead.Plots[i][1]))<=reach)
            .OrderBy(i=>Vector2.DistanceSquared(Position,new(Graph.Homestead.Plots[i][0],Graph.Homestead.Plots[i][1]))).Cast<int?>().FirstOrDefault();
    }
    public string? NearbyChest(float reach=2.1f)
    {
        if(!MapId.StartsWith("interior:",StringComparison.Ordinal))return null;
        return Graph.Room(MapId).Furniture.Select((f,i)=>(f,i))
            .Where(entry=>entry.f.Label=="CHEST"&&Vector2.Distance(Position,new Vector2(entry.f.X,entry.f.Z))<=reach)
            .OrderBy(entry=>Vector2.DistanceSquared(Position,new Vector2(entry.f.X,entry.f.Z)))
            .Select(entry=>MapId+":chest:"+entry.i).FirstOrDefault();
    }
    public bool TryFarm(out string message)
    {
        var index=NearbyPlot();
        if(index==null){message="MOVE NEAR A FARM PLOT";return false;}
        var current=PlotStage(index.Value);
        if(current==FarmStage.Tilled&&Inventory.Bag.Count("common_seed")<1)
        {message="NEED A COMMON SEED TO PLANT";return false;}
        if(current==FarmStage.Watered&&!Inventory.Bag.CanAddBoth("common_seed",1,"crop",1))
        {message="BAG FULL / HARVEST WAITING";return false;}
        var next=PlotStage(index.Value) switch
        {
            FarmStage.Wild => FarmStage.Tilled,
            FarmStage.Tilled => FarmStage.Planted,
            FarmStage.Planted => FarmStage.Watered,
            FarmStage.Watered => FarmStage.Tilled,
            _ => FarmStage.Wild
        };
        if(current==FarmStage.Tilled)Inventory.Bag.TryRemove("common_seed",1);
        if(current==FarmStage.Watered)
        {
            Inventory.Bag.TryAdd("common_seed",1);Inventory.Bag.TryAdd("crop",1);
            HarvestCount++;message=$"HARVESTED CROP / TOTAL {HarvestCount}";
        }
        else message=next switch {FarmStage.Tilled=>"SOIL TILLED",FarmStage.Planted=>"SEED PLANTED",FarmStage.Watered=>"CROP WATERED",_=>"PLOT"};
        plots[index.Value]=next;
        return true;
    }
    public void ReplaceGraph(WorldGraph graph)
    {
        Graph=graph;
        if(!graph.HasMap(MapId)||!graph.CanWalk(MapId,Position))
        { MapId="village";var p=graph.Spawn(MapId);Position=new(p.X,p.Z); }
        foreach(int index in plots.Keys.Where(i=>i>=graph.Homestead.Plots.Length).ToArray())plots.Remove(index);
    }
}
