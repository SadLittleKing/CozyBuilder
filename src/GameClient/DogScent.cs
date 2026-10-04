using Microsoft.Xna.Framework;
using System.Text.Json;

namespace GameClient;

public enum DogScentMode { Off, Explore, Home, Shrine, Rare, Common }
public sealed record DogScentAdvice(string Message,int? NextNode);

public static class DogScent
{
    static readonly DogScentMode[] Modes=Enum.GetValues<DogScentMode>();
    public static DogScentMode Cycle(WorldState world)
    {
        int index=Array.IndexOf(Modes,world.ScentMode);
        for(int step=1;step<=Modes.Length;step++)
        {
            var mode=Modes[(index+step)%Modes.Length];
            if(Available(world,mode))return mode;
        }
        return DogScentMode.Off;
    }
    public static bool Available(WorldState world,DogScentMode mode)=>mode switch
    {
        DogScentMode.Shrine=>world.Discovered.Contains(world.Graph.Maze.ShrineNode),
        DogScentMode.Rare=>world.Discovered.Contains(world.Graph.Maze.RareNode),
        DogScentMode.Common=>world.Discovered.Contains(world.Graph.Maze.CommonNode),
        _=>true
    };
    public static DogScentAdvice Advise(WorldState world)
    {
        if(!WoodedMaze.TryNode(world.MapId,out int current))return new("DOG RESTING / SCENT WORKS IN MAZE",null);
        if(world.ScentMode==DogScentMode.Off)return new("Q ASK DOG TO FIND A SCENT",null);
        var maze=world.Graph.Maze;
        var distance=Enumerable.Repeat(-1,WoodedMaze.Count).ToArray();
        var first=Enumerable.Repeat(-1,WoodedMaze.Count).ToArray();
        var queue=new Queue<int>();distance[current]=0;first[current]=current;queue.Enqueue(current);
        while(queue.Count>0)
        {
            int at=queue.Dequeue();
            foreach(int next in maze.Neighbors(at))if(distance[next]<0)
            {distance[next]=distance[at]+1;first[next]=at==current?next:first[at];queue.Enqueue(next);}
        }
        int target;
        string scent;
        switch(world.ScentMode)
        {
            case DogScentMode.Explore:
                var closeSeed=new[]{(!world.RareCollected,maze.RareNode,"RARE SEED"),
                    (!world.CommonCollected,maze.CommonNode,"COMMON SEEDS")}
                    .Where(item=>item.Item1&&distance[item.Item2] is >=0 and <=3)
                    .OrderBy(item=>distance[item.Item2]).ThenBy(item=>item.Item2).FirstOrDefault();
                if(closeSeed.Item1){target=closeSeed.Item2;scent=closeSeed.Item3;}
                else
                {
                    var unseen=Enumerable.Range(0,WoodedMaze.Count).Where(n=>!world.Discovered.Contains(n))
                        .OrderBy(n=>distance[n]).ThenBy(n=>n).FirstOrDefault(-1);
                    if(unseen<0)return new("DOG SCENT EXPLORE / ALL CLEARINGS FOUND",null);
                    target=unseen;scent="UNSEEN TRAIL";
                }
                break;
            case DogScentMode.Home:target=0;scent="HOME GATE";break;
            case DogScentMode.Shrine:target=maze.ShrineNode;scent="KNOWN SHRINE";break;
            case DogScentMode.Rare:target=maze.RareNode;scent="KNOWN RARE CLEARING";break;
            case DogScentMode.Common:target=maze.CommonNode;scent="KNOWN COMMON CLEARING";break;
            default:return new("Q ASK DOG TO FIND A SCENT",null);
        }
        if(!Available(world,world.ScentMode))return new("DOG HAS NOT LEARNED THAT TRAIL / Q CHANGE",null);
        if(target==current)
        {
            string action=world.ScentMode switch
            {
                DogScentMode.Home=>"WEST TO VILLAGE",
                DogScentMode.Shrine=>"E USE SHRINE",
                DogScentMode.Rare when !world.RareCollected=>"F COLLECT SEED",
                DogScentMode.Common when !world.CommonCollected=>"F COLLECT SEEDS",
                _=>"HERE"
            };
            return new($"DOG SCENT {scent} / {action}",null);
        }
        if(distance[target]<0)return new("DOG CANNOT FIND A ROUTE / Q CHANGE",null);
        int nextNode=first[target];
        var (dx,dz)=maze.Direction(current,nextNode);
        string direction=dx<0?"WEST":dx>0?"EAST":dz<0?"NORTH":"SOUTH";
        return new($"DOG SCENT {scent} / GO {direction}",nextNode);
    }
}

public sealed class DogCompanion
{
    public Vector2 Position { get; private set; }
    public float Heading { get; private set; }
    public DogCompanion(WorldState world)=>Snap(world);
    public void Snap(WorldState world)
    {
        var candidates=new[]{world.Position+new Vector2(1.15f,1.0f),world.Position+new Vector2(-1.15f,1.0f),world.Position};
        Position=candidates.First(p=>world.Graph.CanWalk(world.MapId,p,.15f));
    }
    public void Follow(WorldState world,float delta,DogScentAdvice advice)
    {
        var candidates=new List<Vector2>();
        if(advice.NextNode is int leadNode&&WoodedMaze.TryNode(world.MapId,out int at))
        {
            var (leadX,leadZ)=world.Graph.Maze.Direction(at,leadNode);
            var direction=new Vector2(leadX,leadZ);
            var lead=world.Position+direction*1.8f+new Vector2(-leadZ,leadX)*.35f;
            candidates.Add(new Vector2(MathHelper.Clamp(lead.X,-4.65f,4.65f),MathHelper.Clamp(lead.Y,-4.65f,4.65f)));
        }
        candidates.Add(world.Position+new Vector2(1.15f,1.0f));
        candidates.Add(world.Position+new Vector2(-1.15f,1.0f));
        candidates.Add(world.Position);
        var desired=candidates.First(p=>world.Graph.CanWalk(world.MapId,p,.15f));
        var difference=desired-Position;
        if(difference.Length()>3.5f){Snap(world);return;}
        if(difference.LengthSquared()>.04f)
        {
            var step=Vector2.Normalize(difference)*MathF.Min(difference.Length(),delta*4.0f);
            var x=new Vector2(Position.X+step.X,Position.Y);
            var z=new Vector2(Position.X,Position.Y+step.Y);
            if(world.Graph.CanWalk(world.MapId,x,.15f))Position=x;
            if(world.Graph.CanWalk(world.MapId,z,.15f))Position=z;
            Heading=MathF.Atan2(step.X,step.Y);
        }
        if(advice.NextNode is int next&&WoodedMaze.TryNode(world.MapId,out int current))
        {
            var (dx,dz)=world.Graph.Maze.Direction(current,next);
            Heading=MathF.Atan2(dx,dz);
        }
    }
}

public static class DogScentVerifier
{
    public static void Verify(WorldGraph graph)
    {
        var maze=graph.Maze;
        var state=new WorldState(graph,maze.MapId(0));
        state.CycleDogScent();
        if(state.ScentMode!=DogScentMode.Explore||DogScent.Advise(state).NextNode is not int first||
           !maze.Connected(0,first))throw new InvalidDataException("Dog exploration scent lacks a reachable first step.");
        var (dx,dz)=maze.Direction(0,first);
        state.MoveTo(WoodedMaze.Gate(dx,dz));
        if(!state.TryInteract(out _)||state.MapId!=maze.MapId(first)||!state.Discovered.Contains(first))
            throw new InvalidDataException("Dog exploration step did not reach and reveal a node.");
        state.CycleDogScent();
        if(state.ScentMode!=DogScentMode.Home||DogScent.Advise(state).NextNode is not int homeStep||
           !maze.Connected(first,homeStep))throw new InvalidDataException("Dog did not recall the home route.");
        var dog=new DogCompanion(state);
        if(!state.MoveTo(Vector2.Zero))throw new InvalidDataException("Dog follow check cannot move player.");
        for(int i=0;i<90;i++)dog.Follow(state,1f/60f,DogScent.Advise(state));
        if(Vector2.Distance(dog.Position,state.Position)>2.5f||!graph.CanWalk(state.MapId,dog.Position,.15f))
            throw new InvalidDataException("Dog follow moved outside walkable clearing.");
        var (homeX,homeZ)=maze.Direction(first,homeStep);
        if(Vector2.Dot(dog.Position-state.Position,new Vector2(homeX,homeZ))<.5f)
            throw new InvalidDataException("Dog did not lead ahead toward the scented exit.");
        var learned=new MazeSave {Seed=maze.Seed,MapId=maze.MapId(0),X=-3.5f,Z=0,
            Discovered=[0,maze.RareNode,maze.CommonNode,maze.ShrineNode],DogScent="Rare"};
        var known=new WorldState(graph,learned.MapId,new Vector2(learned.X,learned.Z),learned);
        if(known.ScentMode!=DogScentMode.Rare||DogScent.Advise(known).NextNode is not int next||!maze.Connected(0,next))
            throw new InvalidDataException("Dog did not recall a learned rare clearing.");
        string temp=Path.Combine(Path.GetTempPath(),$"game-dog-scent-{Guid.NewGuid():N}.json");
        try
        {
            known.Snapshot().Write(temp);
            var reloaded=MazeSave.Load(temp);
            var restored=new WorldState(graph,reloaded.MapId,new Vector2(reloaded.X,reloaded.Z),reloaded);
            if(restored.ScentMode!=DogScentMode.Rare||DogScent.Advise(restored).NextNode!=next)
                throw new InvalidDataException("Dog scent mode or route changed after save/reload.");
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
        int nearRare=maze.Neighbors(maze.RareNode).First();
        var smelling=new WorldState(graph,maze.MapId(nearRare));smelling.CycleDogScent();
        if(!DogScent.Advise(smelling).Message.Contains("SEED",StringComparison.Ordinal))
            throw new InvalidDataException("Nearby uncollected seed scent was not detected.");
        string oldPath=Path.Combine(Path.GetTempPath(),$"game-old-maze-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(oldPath,JsonSerializer.Serialize(new {Version=1,Seed=maze.Seed,MapId="village",
                X=0f,Z=3f,Discovered=new[]{0},RareCollected=false,CommonCollected=false,ShrineUnlocked=false}));
            var compatible=new WorldState(graph,"village",null,MazeSave.Load(oldPath));
            if(compatible.ScentMode!=DogScentMode.Off)throw new InvalidDataException("Old maze save has incompatible scent default.");
        }
        finally{if(File.Exists(oldPath))File.Delete(oldPath);}
        Console.WriteLine("Verified dog exploration hint, known-route recall, follow movement, scent save/reload and old-save compatibility.");
    }
}
