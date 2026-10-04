using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient;

public static class VillageTerrain
{
    public static VertexPositionColor[] Build(VillageLayout map)
    {
        var vertices = new List<VertexPositionColor>(map.Width*map.Depth*6+10000);
        var plaza = map.Regions.Single(r => r.Kind == "plaza");
        var farm = map.Regions.Single(r => r.Kind == "farm");
        for (int iz=0;iz<map.Depth;iz++)
        for (int ix=0;ix<map.Width;ix++)
        {
            float x=ix-map.Width/2f,z=iz-map.Depth/2f;
            float cx=x+.5f,cz=z+.5f;
            float noise=(Hash(ix,iz,map.Seed)%9)/300f;
            Color color;
            if (map.IsPond(cx,cz)) color = Color.Lerp(new Color(103,158,166),new Color(142,186,184),noise*5);
            else if (plaza.Contains(cx,cz)) color = Color.Lerp(new Color(208,198,166),new Color(230,218,184),noise*4);
            else if (map.IsPath(cx,cz)) color = Color.Lerp(new Color(193,176,136),new Color(222,204,158),noise*4);
            else if (farm.Contains(cx,cz)) color = Color.Lerp(new Color(159,175,119),new Color(189,193,141),noise*4);
            else color = Color.Lerp(new Color(170,191,146),new Color(199,207,164),noise*5);
            Quad(vertices,x,-.035f,z,x+1,z+1,color);
        }

        // Repeated rectangular crop beds make the farm legible from overhead.
        for (float z=farm.Z-.2f;z<=farm.Z+farm.Depth/2-1;z+=1.2f)
        for (float x=farm.X-farm.Width/2+.8f;x<=farm.X+farm.Width/2-1;x+=1.8f)
        {
            if (map.IsPath(x,z) || map.Buildings.Any(b => MathF.Abs(x-b.X)<b.Width/2+.7f && MathF.Abs(z-b.Z)<b.Depth/2+.7f)) continue;
            Box(vertices,x,.01f,z,1.2f,.055f,.62f,new Color(119,109,76),new Color(132,116,77));
            for (int n=0;n<3;n++)
                Box(vertices,x-.38f+n*.38f,.07f,z,.17f,.08f,.25f,new Color(112,147,84),new Color(98,133,76));
        }

        // Square plinth, water and low rim: a landmark at the gathering area.
        float wx=plaza.X,wz=plaza.Z;
        Box(vertices,wx,.14f,wz,1.55f,.28f,1.55f,new Color(183,181,158),new Color(165,162,143));
        Box(vertices,wx,.305f,wz,1.10f,.04f,1.10f,new Color(92,150,160),new Color(92,150,160));
        var stone=new Color(205,202,176);
        Box(vertices,wx,.36f,wz-.68f,1.58f,.18f,.25f,stone,new Color(168,165,144));
        Box(vertices,wx,.36f,wz+.68f,1.58f,.18f,.25f,stone,new Color(168,165,144));
        Box(vertices,wx-.68f,.36f,wz,.25f,.18f,1.33f,stone,new Color(168,165,144));
        Box(vertices,wx+.68f,.36f,wz,.25f,.18f,1.33f,stone,new Color(168,165,144));

        // Zone colors identify intended use; these blocks are footprints, not finished architecture.
        foreach (var b in map.Buildings)
        {
            var roof = b.Zone switch
            {
                "square" => new Color(105,123,113),
                "market" => new Color(168,110,85),
                "homes" => new Color(150,128,104),
                "farm" => new Color(137,151,102),
                _ => new Color(151,130,111)
            };
            Box(vertices,b.X,.055f,b.Z,b.Width+.35f,.11f,b.Depth+.35f,new Color(211,197,169),new Color(178,165,144));
            Box(vertices,b.X,b.Height/2+.11f,b.Z,b.Width,b.Height,b.Depth,roof,Color.Lerp(roof,Color.Black,.22f));
            // Small front threshold points toward each authored approach.
            float facing = MathF.Sign(b.Approach[1]-b.Z);
            Box(vertices,b.X,.045f,b.Z+facing*(b.Depth/2+.43f),1.0f,.09f,.7f,new Color(223,209,171),new Color(190,177,147));
        }

        // A gate marks the road leaving toward the dungeon.
        var dungeonEnd=map.Routes.Single(r => r.Name.Contains("dungeon",StringComparison.OrdinalIgnoreCase)).Points[^1];
        float gx=dungeonEnd[0],gz=dungeonEnd[1]-1.7f;
        foreach (float x in new[] {gx-2.25f,gx+2.25f})
            Box(vertices,x,.9f,gz,.58f,1.8f,.72f,new Color(151,156,146),new Color(119,128,122));
        Box(vertices,gx,1.7f,gz,5.05f,.42f,.72f,new Color(176,177,160),new Color(128,135,127));

        // Deterministic, sparse groves keep later JSON edits reproducible.
        for (int z=-map.Depth/2+1;z<map.Depth/2-1;z+=2)
        for (int x=-map.Width/2+1;x<map.Width/2-1;x+=2)
        {
            uint random=Hash(x,z,map.Seed);
            if (random%7!=0) continue;
            float px=x+((int)((random>>5)%7)-3)*.12f;
            float pz=z+((int)((random>>9)%7)-3)*.12f;
            if (map.IsPath(px,pz) || map.IsPond(px,pz) || farm.Contains(px,pz) || plaza.Contains(px,pz) ||
                map.Buildings.Any(b => MathF.Abs(px-b.X)<b.Width/2+1.3f && MathF.Abs(pz-b.Z)<b.Depth/2+1.3f)) continue;
            float height=1.0f+(random%5)*.11f;
            Box(vertices,px,.32f,pz,.17f,.64f,.17f,new Color(117,101,73),new Color(103,91,70));
            Pyramid(vertices,px,.65f,pz,.85f,height,new Color(93+(byte)(random%18),139+(byte)(random%20),98+(byte)(random%12)));
        }
        return vertices.ToArray();
    }

    static uint Hash(int x,int z,int seed)
    {
        unchecked
        {
            uint n=(uint)(x*73856093 ^ z*19349663 ^ seed*83492791);
            n ^= n>>16; n*=0x7feb352d; n^=n>>15; n*=0x846ca68b; n^=n>>16;
            return n;
        }
    }
    static VertexPositionColor V(float x,float y,float z,Color color) => new(new Vector3(x,y,z),color);
    static void Tri(List<VertexPositionColor> output,Vector3 a,Vector3 b,Vector3 c,Color color)
    { output.Add(new(a,color)); output.Add(new(b,color)); output.Add(new(c,color)); }
    internal static void Quad(List<VertexPositionColor> output,float x1,float y,float z1,float x2,float z2,Color color)
    {
        output.Add(V(x1,y,z1,color));output.Add(V(x2,y,z1,color));output.Add(V(x2,y,z2,color));
        output.Add(V(x1,y,z1,color));output.Add(V(x2,y,z2,color));output.Add(V(x1,y,z2,color));
    }
    internal static void Box(List<VertexPositionColor> output,float x,float y,float z,float width,float height,float depth,Color top,Color side)
    {
        float l=x-width/2,r=x+width/2,b=y-height/2,t=y+height/2,n=z-depth/2,f=z+depth/2;
        Quad(output,l,t,n,r,f,top);
        Face(output,new(l,b,n),new(r,b,n),new(r,t,n),new(l,t,n),side);
        Face(output,new(r,b,f),new(l,b,f),new(l,t,f),new(r,t,f),side);
        Face(output,new(l,b,f),new(l,b,n),new(l,t,n),new(l,t,f),side);
        Face(output,new(r,b,n),new(r,b,f),new(r,t,f),new(r,t,n),side);
    }
    static void Face(List<VertexPositionColor> output,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
    { Tri(output,a,b,c,color);Tri(output,a,c,d,color); }
    internal static void Pyramid(List<VertexPositionColor> output,float x,float baseY,float z,float width,float height,Color color)
    {
        float r=width/2;
        var a=new Vector3(x-r,baseY,z-r); var b=new Vector3(x+r,baseY,z-r);
        var c=new Vector3(x+r,baseY,z+r); var d=new Vector3(x-r,baseY,z+r);
        var tip=new Vector3(x,baseY+height,z);
        Tri(output,a,b,tip,color);Tri(output,b,c,tip,Color.Lerp(color,Color.Black,.07f));
        Tri(output,c,d,tip,Color.Lerp(color,Color.Black,.15f));Tri(output,d,a,tip,Color.Lerp(color,Color.Black,.10f));
    }
}
