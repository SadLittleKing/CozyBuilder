using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient;

public enum ResourceKind { Tree, AppleTree, BerryTree, PlumTree, Rock, OreRock }
public sealed record ResourceNode(int Node,int Slot,ResourceKind Kind,Vector2 Position)
{
    public string Id=>$"{Node}:{Slot}";
    public float Duration=>Kind switch { ResourceKind.Rock=>2.2f,ResourceKind.OreRock=>3.4f,_=>1.35f };
    public string Name=>Kind switch { ResourceKind.AppleTree=>"APPLE TREE",ResourceKind.BerryTree=>"BERRY TREE",ResourceKind.PlumTree=>"PLUM TREE",ResourceKind.OreRock=>"ORE ROCK",ResourceKind.Rock=>"ROCK",_=>"TREE" };
    public (string Id,int Quantity)[] Loot=>Kind switch
    {
        ResourceKind.Rock=>[("stone",3)], ResourceKind.OreRock=>[("stone",2),("ore",2)],
        ResourceKind.AppleTree=>[("wood",3),("apple",2)],
        ResourceKind.BerryTree=>[("wood",3),("berry",3)],
        ResourceKind.PlumTree=>[("wood",3),("plum",2)],
        _=>[("wood",3)]
    };
}
public sealed record GroundDrop(string Id,string ItemId,int Quantity,int Node,float X,float Z)
{
    public Vector2 Position=>new(X,Z);
}

public static class MazeResources
{
    static readonly Vector2[] Spots=[new(-2.8f,-2.8f),new(2.8f,-2.8f),new(-2.8f,2.8f),new(2.8f,2.8f)];
    public static IReadOnlyList<ResourceNode> At(WoodedMaze maze,int node)
    {
        var result=new List<ResourceNode>(4);
        for(int slot=0;slot<4;slot++)
        {
            uint h=Hash(maze.Seed,node,slot);
            ResourceKind kind;
            if(node==0)kind=slot switch {0=>ResourceKind.Tree,1=>ResourceKind.Rock,2=>ResourceKind.AppleTree,_=>ResourceKind.Rock};
            else
            {
                int roll=(int)(h%100);
                kind=roll switch
                {
                    <30=>ResourceKind.Tree,<54=>ResourceKind.Rock,
                    <67=>ResourceKind.AppleTree,<79=>ResourceKind.BerryTree,
                    <90=>ResourceKind.PlumTree,_=>ResourceKind.OreRock
                };
                // The nearest clearings favor basics; fruit and ore broaden along the actual maze route.
                if(maze.Depth(node)<4 && kind is ResourceKind.BerryTree or ResourceKind.PlumTree)kind=ResourceKind.Tree;
                if(maze.Depth(node)<6 && kind==ResourceKind.OreRock)kind=ResourceKind.Rock;
            }
            var spot=Spots[slot]+new Vector2(((int)((h>>8)%5)-2)*.08f,((int)((h>>12)%5)-2)*.08f);
            result.Add(new ResourceNode(node,slot,kind,spot));
        }
        return result;
    }
    static uint Hash(int seed,int node,int slot)
    {
        unchecked
        {
            uint h=(uint)seed ^ ((uint)node*0x9E3779B9u) ^ ((uint)(slot+1)*0x85EBCA6Bu);
            h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;return h^(h>>16);
        }
    }
    public static VertexPositionColor[] Build(WorldState world)
    {
        if(!WoodedMaze.TryNode(world.MapId,out int node))return [];
        var mesh=new List<VertexPositionColor>();
        foreach(var resource in At(world.Graph.Maze,node))
        {
            if(world.DepletedResources.Contains(resource.Id))continue;
            float x=resource.Position.X,z=resource.Position.Y;
            if(resource.Kind is ResourceKind.Rock or ResourceKind.OreRock)
            {
                bool ore=resource.Kind==ResourceKind.OreRock;
                var top=ore?new Color(98,113,126):new Color(137,143,139);
                var side=ore?new Color(68,81,94):new Color(102,110,107);
                Rock(mesh,x,z,top,side);
                if(ore)VillageTerrain.Box(mesh,x,.65f,z,.25f,.12f,.35f,new Color(194,139,78),new Color(150,98,57));
            }
            else
            {
                VillageTerrain.Box(mesh,x,.55f,z,.27f,1.1f,.27f,new Color(112,82,56),new Color(82,62,45));
                VillageTerrain.Pyramid(mesh,x,1.15f,z,1.25f,1.55f,new Color(79,130,82));
                if(resource.Kind!=ResourceKind.Tree)
                {
                    Color fruit=resource.Kind switch
                    {
                        ResourceKind.AppleTree=>new Color(222,76,61),
                        ResourceKind.BerryTree=>new Color(65,89,211),
                        _=>new Color(170,79,180)
                    };
                    float size=resource.Kind==ResourceKind.BerryTree?.22f:.29f;
                    foreach(var (ox,oz,y) in new[]{(.62f,.24f,1.38f),(-.62f,.2f,1.36f),(.12f,.64f,1.28f),
                        (.43f,.48f,1.69f),(-.34f,.51f,1.71f),(.08f,-.65f,1.31f)})
                        VillageTerrain.Box(mesh,x+ox,y,z+oz,size,size,size,fruit,Color.Lerp(fruit,Color.Black,.2f));
                }
            }
        }
        return mesh.ToArray();
    }
    // The faceted rock is exported from assets/maze/rock.json by generate_rock.py.
    static void Rock(List<VertexPositionColor> mesh,float x,float z,Color top,Color side)
    {
        VillageTerrain.Box(mesh,x,.225f,z,1.16f,.45f,1.16f,side,Color.Lerp(side,Color.Black,.16f));
        VillageTerrain.Pyramid(mesh,x,.45f,z,1.16f,.65f,top);
    }
}
