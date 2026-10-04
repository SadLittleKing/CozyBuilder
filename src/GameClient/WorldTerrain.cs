using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient;

public static class WorldTerrain
{
    public static VertexPositionColor[] Build(WorldGraph graph,string mapId) => mapId switch
    {
        "village" => Village(graph),
        "homestead" => Homestead(graph.Homestead),
        _ when WoodedMaze.TryNode(mapId,out int node) => MazeTerrain.Build(graph.Maze,node,false,false),
        _ => Interior(graph.Room(mapId))
    };
    public static VertexPositionColor[] Build(WorldState world) => WoodedMaze.TryNode(world.MapId,out int node)
        ? MazeTerrain.Build(world.Graph.Maze,node,world.RareCollected,world.CommonCollected)
        : Build(world.Graph,world.MapId);

    static VertexPositionColor[] Village(WorldGraph graph)
    {
        var output=VillageTerrain.Build(graph.Village).ToList();
        var gate=graph.Homestead.VillageApproach;
        float x=gate[0],z=gate[1];
        // The west lane sign is outside the walkable road surface.
        VillageTerrain.Box(output,x,.7f,z-1.9f,.16f,1.4f,.16f,new Color(118,95,69),new Color(110,85,61));
        VillageTerrain.Box(output,x,.99f,z-1.9f,1.65f,.52f,.16f,new Color(161,129,91),new Color(130,99,70));
        var dungeon=graph.DungeonGate;
        VillageTerrain.Box(output,dungeon.X,1.23f,dungeon.Y-2.4f,1.65f,.52f,.16f,
            new Color(104,126,99),new Color(78,102,78));
        return output.ToArray();
    }

    static VertexPositionColor[] Homestead(HomesteadLayout map)
    {
        var output=new List<VertexPositionColor>(map.Width*map.Depth*6+3000);
        for(int iz=0;iz<map.Depth;iz++)
        for(int ix=0;ix<map.Width;ix++)
        {
            float x=ix-map.Width/2f,z=iz-map.Depth/2f;
            float px=x+.5f,pz=z+.5f;
            int variation=(ix*17+iz*31+map.Seed)%7;
            var color=map.IsPath(px,pz) ? new Color(204+variation,186+variation,146+variation) :
                map.Field.Contains(px,pz) ? new Color(166+variation,180+variation,125+variation) :
                new Color(176+variation,194+variation,150+variation);
            VillageTerrain.Quad(output,x,-.035f,z,x+1,z+1,color);
        }
        var house=map.House;
        VillageTerrain.Box(output,house.X,.06f,house.Z,house.Width+.35f,.12f,house.Depth+.35f,new Color(213,197,164),new Color(176,158,132));
        VillageTerrain.Box(output,house.X,house.Height/2+.12f,house.Z,house.Width,house.Height,house.Depth,new Color(150,106,89),new Color(122,86,72));
        VillageTerrain.Box(output,house.Approach[0],.04f,house.Approach[1],1.6f,.08f,1.1f,new Color(228,209,166),new Color(194,172,136));
        // Orchard and perimeter are just scenery; the field plots hold state.
        for(int i=0;i<7;i++)
        {
            float x=-13+i*4.0f,z=10.7f;
            if(map.Field.Contains(x,z))continue;
            VillageTerrain.Box(output,x,.38f,z,.19f,.76f,.19f,new Color(119,102,75),new Color(102,89,66));
            VillageTerrain.Pyramid(output,x,.75f,z,1.15f,1.25f,new Color(100,147,103));
        }
        for(float x=map.Field.X-map.Field.Width/2+.5f;x<map.Field.X+map.Field.Width/2;x+=1.5f)
        {
            VillageTerrain.Box(output,x,.26f,map.Field.Z-map.Field.Depth/2,.11f,.52f,.11f,new Color(175,153,110),new Color(149,129,96));
            VillageTerrain.Box(output,x,.26f,map.Field.Z+map.Field.Depth/2,.11f,.52f,.11f,new Color(175,153,110),new Color(149,129,96));
        }
        var gate=map.VillageGate;
        foreach(float offset in new[]{-1.45f,1.45f})
            VillageTerrain.Box(output,gate[0],.64f,gate[1]+offset,.22f,1.28f,.22f,new Color(151,122,85),new Color(126,99,70));
        VillageTerrain.Box(output,gate[0],1.24f,gate[1],.22f,.18f,3.14f,new Color(171,142,97),new Color(142,114,78));
        return output.ToArray();
    }

    public static VertexPositionColor[] BuildPlots(WorldState world)
    {
        if(world.MapId!="homestead")return [];
        var output=new List<VertexPositionColor>();
        for(int i=0;i<world.Graph.Homestead.Plots.Length;i++)
        {
            var point=world.Graph.Homestead.Plots[i];
            float x=point[0],z=point[1];
            var stage=world.PlotStage(i);
            var dirt=stage switch
            {
                FarmStage.Wild => new Color(163,169,111),
                FarmStage.Tilled => new Color(114,91,65),
                FarmStage.Planted => new Color(124,95,65),
                FarmStage.Watered => new Color(91,91,72),
                _ => new Color(114,91,65)
            };
            VillageTerrain.Box(output,x,.015f,z,1.35f,.09f,1.30f,dirt,new Color(131,110,76));
            if(stage is FarmStage.Planted or FarmStage.Watered)
            {
                float height=stage==FarmStage.Watered ? .30f : .16f;
                for(int row=-1;row<=1;row++)
                    VillageTerrain.Box(output,x+row*.34f,.08f+height/2,z,.18f,height,.26f,
                        stage==FarmStage.Watered?new Color(115,163,81):new Color(137,170,96),new Color(102,137,74));
            }
        }
        return output.ToArray();
    }

    static VertexPositionColor[] Interior(RoomLayout room)
    {
        var output=new List<VertexPositionColor>();
        var floor=room.Floor switch
        {
            "stone" => new Color(184,181,165),
            "earth" => new Color(162,143,112),
            _ => new Color(205,179,140)
        };
        for(int z=-(int)room.Depth/2;z<(int)room.Depth/2;z++)
        for(int x=-(int)room.Width/2;x<(int)room.Width/2;x++)
        {
            var tile=Color.Lerp(floor,Color.White,((x+z)&1)==0?.025f:.07f);
            VillageTerrain.Quad(output,x,-.025f,z,x+1,z+1,tile);
        }
        // Roofless cutaway room: three full walls, south wall with a visible door gap.
        var wall=new Color(223,207,174);var side=new Color(181,165,138);
        VillageTerrain.Box(output,0,.7f,-room.Depth/2+.17f,room.Width,1.4f,.34f,wall,side);
        VillageTerrain.Box(output,-room.Width/2+.17f,.7f,0,.34f,1.4f,room.Depth,wall,side);
        VillageTerrain.Box(output,room.Width/2-.17f,.7f,0,.34f,1.4f,room.Depth,wall,side);
        float segment=(room.Width-2.2f)/2;
        VillageTerrain.Box(output,-room.Width/2+segment/2,.7f,room.Depth/2-.17f,segment,1.4f,.34f,wall,side);
        VillageTerrain.Box(output,room.Width/2-segment/2,.7f,room.Depth/2-.17f,segment,1.4f,.34f,wall,side);
        VillageTerrain.Box(output,0,.018f,room.Depth/2-.9f,1.65f,.06f,1.3f,new Color(149,117,83),new Color(130,101,70));
        // Rugs and furniture colors distinguish roles while they remain blockout shapes.
        if(room.Floor=="warm")VillageTerrain.Box(output,0,-.006f,0,3.0f,.035f,2.0f,new Color(152,104,86),new Color(152,104,86));
        foreach(var f in room.Furniture)
        {
            var top=f.Label switch
            {
                "BED" => new Color(143,169,147),
                "HAY" => new Color(198,176,104),
                "FURNACE" => new Color(142,95,82),
                "ANVIL" => new Color(109,125,132),
                "SEEDS" => new Color(152,169,104),
                "HEARTH" => new Color(189,118,87),
                _ => new Color(153,118,88)
            };
            VillageTerrain.Box(output,f.X,f.Height/2,f.Z,f.Width,f.Height,f.Depth,top,Color.Lerp(top,Color.Black,.18f));
        }
        return output.ToArray();
    }
}
