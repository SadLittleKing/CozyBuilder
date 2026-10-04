using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient;

public sealed class WoodedMaze
{
    public const int Width=10, Height=10, Count=Width*Height;
    public int Seed { get; }
    public int RareNode { get; }
    public int CommonNode { get; }
    public int ShrineNode { get; }
    readonly int[] depth;
    public int Depth(int node)=>depth[node];
    readonly HashSet<int>[] links=Enumerable.Range(0,Count).Select(_=>new HashSet<int>()).ToArray();
    public string Biome(int node)=>"woodland";
    public string MapId(int node)=>$"maze:{node}";
    public static bool TryNode(string mapId,out int node)
    {
        node=-1;
        return mapId.StartsWith("maze:",StringComparison.Ordinal) && int.TryParse(mapId.AsSpan(5),out node) && node is >=0 and <Count;
    }
    public (int X,int Y) Coordinates(int node)=>(node%Width,node/Width);
    public IEnumerable<int> Neighbors(int node)=>links[node].Order();
    public bool Connected(int a,int b)=>links[a].Contains(b);
    public (int X,int Z) Direction(int from,int to)
    {
        var (x,y)=Coordinates(from);var (nx,ny)=Coordinates(to);
        return (nx-x,ny-y);
    }
    public static Vector2 Gate(int dx,int dz)=>new(dx*5.15f,dz*5.15f);
    public static Vector2 Arrival(int dx,int dz)=>new(-dx*3.5f,-dz*3.5f);

    public WoodedMaze(int seed)
    {
        Seed=seed;
        var random=new StableRandom(seed);
        var seen=new bool[Count];seen[0]=true;
        var stack=new Stack<int>();stack.Push(0);
        while(stack.Count>0)
        {
            int current=stack.Peek();
            var choices=Adjacent(current).Where(n=>!seen[n]).ToArray();
            if(choices.Length==0){stack.Pop();continue;}
            int next=choices[random.Next(choices.Length)];
            Link(current,next);seen[next]=true;stack.Push(next);
        }
        // Sparse shortcuts soften long retraces while the spanning tree keeps every clearing reachable.
        for(int node=0;node<Count;node++)
            foreach(int next in Adjacent(node).Where(n=>n>node))
                if(!Connected(node,next) && random.Next(100)<9)Link(node,next);
        var distance=Distances(0);depth=distance;
        RareNode=Enumerable.Range(1,Count-1).OrderByDescending(n=>distance[n]).ThenBy(n=>n).First();
        ShrineNode=Enumerable.Range(1,Count-1).Where(n=>n!=RareNode)
            .OrderByDescending(n=>distance[n]+Distances(RareNode)[n]).ThenBy(n=>n).First();
        CommonNode=Enumerable.Range(1,Count-1).Where(n=>n!=RareNode&&n!=ShrineNode)
            .OrderByDescending(n=>distance[n]).ThenByDescending(n=>n).First();
        Validate();
    }
    IEnumerable<int> Adjacent(int node)
    {
        int x=node%Width,y=node/Width;
        if(x>0)yield return node-1;
        if(x<Width-1)yield return node+1;
        if(y>0)yield return node-Width;
        if(y<Height-1)yield return node+Width;
    }
    void Link(int a,int b){links[a].Add(b);links[b].Add(a);}
    int[] Distances(int start)
    {
        var result=Enumerable.Repeat(-1,Count).ToArray();result[start]=0;
        var queue=new Queue<int>();queue.Enqueue(start);
        while(queue.Count>0)
        {
            int at=queue.Dequeue();
            foreach(int next in links[at])if(result[next]<0){result[next]=result[at]+1;queue.Enqueue(next);}
        }
        return result;
    }
    public void Validate()
    {
        for(int n=0;n<Count;n++)
        {
            if(links[n].Count is <1 or >4)throw new InvalidDataException($"Maze node {n} has invalid exit count.");
            foreach(int next in links[n])
                if(!Adjacent(n).Contains(next)||!links[next].Contains(n))
                    throw new InvalidDataException($"Maze exit {n}->{next} is non-adjacent or one-way.");
        }
        if(Distances(0).Any(d=>d<0))throw new InvalidDataException("Maze has unreachable nodes.");
        if(new[]{RareNode,CommonNode,ShrineNode}.Distinct().Count()!=3 ||
           new[]{RareNode,CommonNode,ShrineNode}.Any(n=>n<=0||n>=Count))
            throw new InvalidDataException("Maze special nodes must be distinct, reachable and away from entrance.");
    }
    sealed class StableRandom
    {
        uint state;
        public StableRandom(int seed)
        {
            state=unchecked((uint)seed)^0x9E3779B9u;
            if(state==0)state=0xA341316Cu;
        }
        public int Next(int max)
        {
            state^=state<<13;state^=state>>17;state^=state<<5;
            return (int)(state%(uint)max);
        }
    }
}

public sealed class MazeSave
{
    public int Version { get; set; }=1;
    public int Seed { get; set; }
    public string MapId { get; set; }="village";
    public float X { get; set; }
    public float Z { get; set; }=3;
    public int[] Discovered { get; set; }=[];
    public bool RareCollected { get; set; }
    public bool CommonCollected { get; set; }
    public bool ShrineUnlocked { get; set; }
    public string DogScent { get; set; }="Off";
    public List<ItemStack>? Inventory { get; set; }
    public Dictionary<string,string?>? Equipment { get; set; }
    public Dictionary<string,List<ItemStack>>? Chests { get; set; }
    public Dictionary<int,FarmStage>? FarmPlots { get; set; }
    public int HarvestCount { get; set; }
    public string[] DepletedResources { get; set; }=[];
    public List<GroundDrop> GroundDrops { get; set; }=[];
    public static MazeSave Load(string path)
    {
        var save=JsonSerializer.Deserialize<MazeSave>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})
            ?? throw new InvalidDataException("Empty maze save.");
        if(save.Version!=1||save.Discovered==null||save.Discovered.Any(n=>n<0||n>=WoodedMaze.Count)||
           !float.IsFinite(save.X)||!float.IsFinite(save.Z)||save.DepletedResources==null||save.GroundDrops==null)
            throw new InvalidDataException("Invalid maze save.");
        return save;
    }
    public void Write(string path)
    {
        string full=Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string pending=full+".tmp";
        File.WriteAllText(pending,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(pending,full,true);
    }
}

public static class MazeTerrain
{
    public static VertexPositionColor[] Build(WoodedMaze maze,int node,bool rareCollected,bool commonCollected)
    {
        var mesh=new List<VertexPositionColor>(5000);
        var forest=new Color(109,148,104);
        for(int z=-7;z<7;z++)for(int x=-7;x<7;x++)
        {
            int shade=(x*13+z*17+node*7+maze.Seed)&7;
            var floor=MathF.Abs(x)<2 && MathF.Abs(z)<2 ? new Color(167+shade,158+shade,119+shade) : new Color(129+shade,165+shade,112+shade);
            VillageTerrain.Quad(mesh,x,-.04f,z,x+1,z+1,floor);
        }
        for(int i=0;i<4;i++)
        {
            (int dx,int dz)=i switch {0=>(0,-1),1=>(1,0),2=>(0,1),_=>(-1,0)};
            bool open=maze.Neighbors(node).Any(n=>maze.Direction(node,n)==(dx,dz)) || node==0&&dx==-1;
            if(open)
            {
                var gate=WoodedMaze.Gate(dx,dz);
                VillageTerrain.Box(mesh,gate.X,.01f,gate.Y,dx==0?1.8f:2.5f,.06f,dz==0?1.8f:2.5f,
                    new Color(205,185,129),new Color(165,143,105));
                foreach(float side in new[]{-1.55f,1.55f})
                {
                    float x=dx==0?side:dx*6.2f,z=dz==0?side:dz*6.2f;
                    Tree(mesh,x,z,forest);
                }
            }
            else
            {
                float x=dx*6.25f,z=dz*6.25f;
                VillageTerrain.Box(mesh,x,.48f,z,dx==0?4.1f:.65f,.95f,dz==0?4.1f:.65f,
                    new Color(83,124,83),new Color(70,104,70));
            }
        }
        for(int i=0;i<16;i++)
        {
            float a=i*MathHelper.TwoPi/16f;
            float x=MathF.Cos(a)*6.7f,z=MathF.Sin(a)*6.7f;
            if(MathF.Abs(x)<2.1f||MathF.Abs(z)<2.1f)continue;
            Tree(mesh,x,z,Color.Lerp(forest,new Color(77,118,82),(i%3)*.17f));
        }
        if(node==maze.RareNode&&!rareCollected)
        {
            VillageTerrain.Box(mesh,0,.18f,0,1.2f,.35f,1.2f,new Color(105,96,133),new Color(87,81,113));
            VillageTerrain.Pyramid(mesh,0,.8f,0,.58f,1.15f,new Color(215,187,100));
        }
        if(node==maze.CommonNode&&!commonCollected)
        {
            VillageTerrain.Box(mesh,0,.12f,0,1.35f,.24f,1.25f,new Color(119,94,66),new Color(105,81,59));
            for(int i=-1;i<=1;i++)VillageTerrain.Pyramid(mesh,i*.32f,.38f,0,.25f,.48f,new Color(151,178,85));
        }
        if(node==maze.ShrineNode)
        {
            VillageTerrain.Box(mesh,0,.16f,0,2.6f,.32f,2.6f,new Color(170,171,153),new Color(132,137,125));
            for(int i=-1;i<=1;i+=2)VillageTerrain.Box(mesh,i*.85f,.75f,0,.35f,1.18f,.35f,new Color(184,184,165),new Color(139,143,129));
            VillageTerrain.Box(mesh,0,1.45f,0,2.2f,.25f,.4f,new Color(187,193,174),new Color(137,148,131));
        }
        return mesh.ToArray();
    }
    static void Tree(List<VertexPositionColor> mesh,float x,float z,Color leaf)
    {
        VillageTerrain.Box(mesh,x,.47f,z,.22f,.95f,.22f,new Color(108,84,61),new Color(87,69,51));
        VillageTerrain.Pyramid(mesh,x,1.05f,z,1.1f,1.35f,leaf);
    }
}

public static class MazeVerifier
{
    public static void Verify(WorldGraph graph)
    {
        var maze=graph.Maze;
        maze.Validate();
        var second=new WoodedMaze(maze.Seed);
        if(maze.RareNode!=second.RareNode||maze.CommonNode!=second.CommonNode||maze.ShrineNode!=second.ShrineNode||
            Enumerable.Range(0,WoodedMaze.Count).Any(n=>!maze.Neighbors(n).SequenceEqual(second.Neighbors(n))))
            throw new InvalidDataException("Maze generation changed for the same seed.");
        var world=new WorldState(graph,"village",graph.DungeonGate);
        if(!world.TryInteract(out _)||world.MapId!=maze.MapId(0))throw new InvalidDataException("Village maze gate failed.");
        if(world.NearbyPortal()!=null)throw new InvalidDataException("Maze arrival is too close to an exit.");
        if(!world.MoveTo(WoodedMaze.Gate(-1,0))||!world.TryInteract(out _)||world.MapId!="village"||
            Vector2.Distance(world.Position,graph.DungeonGate)>.001f)
            throw new InvalidDataException("Maze entrance return failed.");
        if(!world.TryInteract(out _)||world.MapId!=maze.MapId(0))throw new InvalidDataException("Maze reentry failed.");
        Travel(world,maze.CommonNode);
        world.MoveTo(Vector2.Zero);
        if(!world.TryCollect(out _)||!world.CommonCollected||world.Inventory.Bag.Count("common_seed")!=7||world.TryCollect(out _))
            throw new InvalidDataException("Common seeds were not collectible exactly once.");
        Travel(world,maze.RareNode);
        world.MoveTo(Vector2.Zero);
        if(!world.TryCollect(out _)||!world.RareCollected||world.Inventory.Bag.Count("rare_seed")!=1||world.TryCollect(out _))
            throw new InvalidDataException("Rare seed was not collectible exactly once.");
        string temp=Path.Combine(Path.GetTempPath(),$"game-maze-verify-{Guid.NewGuid():N}.json");
        try
        {
            world.Snapshot().Write(temp);
            var save=MazeSave.Load(temp);
            var restored=new WorldState(graph,save.MapId,new Vector2(save.X,save.Z),save);
            if(restored.Discovered.Count!=world.Discovered.Count||!restored.CommonCollected||!restored.RareCollected||
                restored.TryCollect(out _))throw new InvalidDataException("Maze save/reload lost exploration or pickup state.");
            Travel(restored,maze.ShrineNode);
            restored.MoveTo(Vector2.Zero);
            if(!restored.TryInteract(out _)||restored.MapId!="village"||!restored.ShrineUnlocked)
                throw new InvalidDataException("Shrine return to village failed.");
            if(!restored.TryJumpShrine(out _)||restored.MapId!=maze.MapId(maze.ShrineNode))
                throw new InvalidDataException("Village shrine jump failed.");
            restored.Snapshot().Write(temp);
            var final=MazeSave.Load(temp);
            if(!final.ShrineUnlocked||final.MapId!=maze.MapId(maze.ShrineNode)||!final.Discovered.Contains(maze.ShrineNode))
                throw new InvalidDataException("Shrine state did not persist.");
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
        Console.WriteLine($"Verified deterministic {WoodedMaze.Width}x{WoodedMaze.Height} wooded maze, reciprocal exits, traversal, one-time seeds, shrine round trip and save/reload. Rare {maze.RareNode}, common {maze.CommonNode}, shrine {maze.ShrineNode}.");
    }
    static void Travel(WorldState state,int target)
    {
        var maze=state.Graph.Maze;
        if(!WoodedMaze.TryNode(state.MapId,out int current))throw new InvalidDataException("Travel began outside maze.");
        var previous=Enumerable.Repeat(-1,WoodedMaze.Count).ToArray();previous[current]=current;
        var queue=new Queue<int>();queue.Enqueue(current);
        while(queue.Count>0)
        {
            int at=queue.Dequeue();
            foreach(int next in maze.Neighbors(at))if(previous[next]<0){previous[next]=at;queue.Enqueue(next);}
        }
        if(previous[target]<0)throw new InvalidDataException("Target is unreachable.");
        var path=new Stack<int>();
        for(int at=target;at!=current;at=previous[at])path.Push(at);
        while(path.Count>0)
        {
            int next=path.Pop();var (dx,dz)=maze.Direction(current,next);
            if(!state.MoveTo(WoodedMaze.Gate(dx,dz))||!state.TryInteract(out _)||state.MapId!=maze.MapId(next))
                throw new InvalidDataException($"Maze travel {current}->{next} failed.");
            if(state.NearbyPortal()!=null)throw new InvalidDataException($"Maze arrival {next} is too close to a return exit.");
            current=next;
        }
    }
}
