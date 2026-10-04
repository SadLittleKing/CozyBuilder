using System.Text.Json;
using Microsoft.Xna.Framework;

namespace GameClient;

// Metre-scale source data. Geometry and walkability are derived from the same
// rectangles and routes, so editing the JSON does not desync visuals/collision.
public sealed class VillageLayout
{
    public string Name { get; init; } = "Village";
    public int Width { get; init; }
    public int Depth { get; init; }
    public int Seed { get; init; }
    public float[] Spawn { get; init; } = [];
    public VillageRegion[] Regions { get; init; } = [];
    public VillageRoute[] Routes { get; init; } = [];
    public VillageBuilding[] Buildings { get; init; } = [];
    public float[][] Checkpoints { get; init; } = [];
    public Vector3 SpawnPosition => new(Spawn[0],0,Spawn[1]);

    public static VillageLayout Load(string path)
    {
        var village = JsonSerializer.Deserialize<VillageLayout>(File.ReadAllText(path),new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Empty village layout.");
        village.Validate();
        return village;
    }

    public bool InBounds(float x,float z,float margin = 0) =>
        x >= -Width/2f+margin && x <= Width/2f-margin && z >= -Depth/2f+margin && z <= Depth/2f-margin;

    public bool IsPath(float x,float z) =>
        Regions.Any(r => r.Kind == "plaza" && r.Contains(x,z)) ||
        Routes.Any(route => route.Contains(x,z));

    public bool IsPond(float x,float z) => Regions.Any(r => r.Kind == "pond" && r.ContainsEllipse(x,z));

    public bool CanWalk(float x,float z,float radius = .22f)
    {
        if (!InBounds(x,z,radius) || IsPond(x,z)) return false;
        if (Regions.Any(r => r.Kind == "pond" && r.IntersectsDisc(x,z,radius))) return false;
        if (Buildings.Any(b => b.IntersectsDisc(x,z,radius))) return false;
        var square=Regions.FirstOrDefault(r => r.Kind == "plaza");
        if (square != null && (x-square.X)*(x-square.X)+(z-square.Z)*(z-square.Z) <= MathF.Pow(.72f+radius,2)) return false; // square well
        return true;
    }

    public string NearbyPlace(float x,float z)
    {
        var nearest = Buildings.OrderBy(b => (b.X-x)*(b.X-x)+(b.Z-z)*(b.Z-z)).FirstOrDefault();
        if (nearest != null && Vector2.Distance(new(x,z),new(nearest.X,nearest.Z)) < 5) return nearest.Label;
        if (Regions.Any(r => r.Kind == "farm" && r.Contains(x,z))) return "FARM EDGE";
        var pond=Regions.FirstOrDefault(r => r.Kind == "pond");
        if (pond != null && (IsPond(x,z) || Vector2.Distance(new(x,z),new(pond.X,pond.Z)) < Math.Max(pond.Width,pond.Depth))) return "POND";
        if (Regions.Any(r => r.Kind == "plaza" && r.Contains(x,z))) return "VILLAGE SQUARE";
        if (Routes.Any(r => r.Name.Contains("dungeon",StringComparison.OrdinalIgnoreCase) && r.Contains(x,z))) return "DUNGEON ROAD";
        return "VILLAGE";
    }

    public void Validate()
    {
        if (Width is < 16 or > 128 || Depth is < 16 or > 128 || Width%2 != 0 || Depth%2 != 0)
            throw new InvalidDataException("Village dimensions must be even numbers between 16 and 128 metres.");
        if (Spawn.Length != 2 || Routes.Length == 0 || Buildings.Length == 0 || Checkpoints.Length == 0)
            throw new InvalidDataException("Village needs a spawn, routes, buildings and checkpoints.");
        if (!CanWalk(Spawn[0],Spawn[1]) || !IsPath(Spawn[0],Spawn[1])) throw new InvalidDataException("Spawn is not on a walkable path.");
        if (Regions.Count(r => r.Kind == "plaza") != 1 || Regions.Count(r => r.Kind == "farm") != 1 || Regions.Count(r => r.Kind == "pond") != 1)
            throw new InvalidDataException("Expected one plaza, farm and pond region.");
        foreach (var region in Regions)
            if (region.Width <= 0 || region.Depth <= 0 || !InBounds(region.X-region.Width/2,region.Z-region.Depth/2) || !InBounds(region.X+region.Width/2,region.Z+region.Depth/2))
                throw new InvalidDataException($"Invalid or out-of-bounds region: {region.Kind}");
        if (Routes.Count(r => r.Name.Contains("dungeon",StringComparison.OrdinalIgnoreCase)) != 1)
            throw new InvalidDataException("Expected one dungeon road route for the gate marker.");
        foreach (var route in Routes)
        {
            if (route.Points.Length < 2 || route.Width <= 0 || route.Points.Any(p => p.Length != 2 || !InBounds(p[0],p[1])))
                throw new InvalidDataException($"Invalid route: {route.Name}");
        }
        foreach (var building in Buildings)
        {
            if (string.IsNullOrWhiteSpace(building.Id) || string.IsNullOrWhiteSpace(building.Label) || string.IsNullOrWhiteSpace(building.Interior) || building.Width <= 0 || building.Depth <= 0 || building.Height <= 0 || building.Approach.Length != 2)
                throw new InvalidDataException("Invalid building footprint or label.");
            if (!InBounds(building.X-building.Width/2,building.Z-building.Depth/2) || !InBounds(building.X+building.Width/2,building.Z+building.Depth/2))
                throw new InvalidDataException($"Building {building.Id} crosses the map boundary.");
            if (IsPath(building.X,building.Z)) throw new InvalidDataException($"Building {building.Id} sits on a path.");
            if (!IsPath(building.Approach[0],building.Approach[1]) || !CanWalk(building.Approach[0],building.Approach[1]))
                throw new InvalidDataException($"Building {building.Id} has no walkable path at its approach point.");
        }
        if (Buildings.Select(b => b.Id).Distinct(StringComparer.Ordinal).Count() != Buildings.Length)
            throw new InvalidDataException("Building IDs must be unique.");
    }

    // Check the authored path graph on the one-metre grid. Reachability is more
    // useful than a geometry snapshot: it catches severed roads after edits.
    public void VerifyWalkability()
    {
        var square=Regions.Single(r => r.Kind == "plaza");
        var pond=Regions.Single(r => r.Kind == "pond");
        if (CanWalk(square.X,square.Z) || CanWalk(pond.X,pond.Z) || CanWalk(Width/2f,0))
            throw new InvalidDataException("Well, pond and map edge must block movement.");
        foreach (var building in Buildings)
            if (CanWalk(building.X,building.Z)) throw new InvalidDataException($"Building {building.Id} does not block movement.");
        var seen = new bool[Width,Depth];
        var queue = new Queue<(int X,int Z)>();
        (int X,int Z) Tile(float x,float z) => (Math.Clamp((int)MathF.Floor(x+Width/2f),0,Width-1),Math.Clamp((int)MathF.Floor(z+Depth/2f),0,Depth-1));
        bool Road(int ix,int iz)
        {
            float x=ix-Width/2f+.5f,z=iz-Depth/2f+.5f;
            return IsPath(x,z) && CanWalk(x,z,0);
        }
        var start = Tile(Spawn[0],Spawn[1]);
        if (!Road(start.X,start.Z)) throw new InvalidDataException("Spawn tile is not on the path grid.");
        queue.Enqueue(start); seen[start.X,start.Z]=true;
        while (queue.Count > 0)
        {
            var (x,z)=queue.Dequeue();
            foreach (var (dx,dz) in new[] { (1,0),(-1,0),(0,1),(0,-1) })
            {
                int nx=x+dx,nz=z+dz;
                if (nx<0 || nz<0 || nx>=Width || nz>=Depth || seen[nx,nz] || !Road(nx,nz)) continue;
                seen[nx,nz]=true; queue.Enqueue((nx,nz));
            }
        }
        void Reach(float[] point,string label)
        {
            if (point.Length != 2) throw new InvalidDataException($"Invalid checkpoint {label}");
            var t=Tile(point[0],point[1]);
            if (!seen[t.X,t.Z]) throw new InvalidDataException($"Unreachable village checkpoint: {label} ({point[0]},{point[1]}).");
        }
        foreach (var point in Checkpoints) Reach(point,"route");
        foreach (var building in Buildings) Reach(building.Approach,building.Id);
        Console.WriteLine($"Verified {seen.Cast<bool>().Count(v=>v)} connected walkable path tiles, {Checkpoints.Length} route checkpoints, and {Buildings.Length} building approaches.");
    }
}

public sealed class VillageRegion
{
    public string Kind { get; init; } = "";
    public float X { get; init; }
    public float Z { get; init; }
    public float Width { get; init; }
    public float Depth { get; init; }
    public bool Contains(float x,float z) => MathF.Abs(x-X)<=Width/2 && MathF.Abs(z-Z)<=Depth/2;
    public bool ContainsEllipse(float x,float z)
    {
        float dx=(x-X)/(Width/2), dz=(z-Z)/(Depth/2);
        return dx*dx+dz*dz<=1;
    }
    public bool IntersectsDisc(float x,float z,float radius)
    {
        float dx=(x-X)/(Width/2+radius), dz=(z-Z)/(Depth/2+radius);
        return dx*dx+dz*dz<=1;
    }
}

public sealed class VillageRoute
{
    public string Name { get; init; } = "";
    public float Width { get; init; }
    public float[][] Points { get; init; } = [];
    public bool Contains(float x,float z)
    {
        for (int i=0;i<Points.Length-1;i++)
        {
            var a=new Vector2(Points[i][0],Points[i][1]);
            var b=new Vector2(Points[i+1][0],Points[i+1][1]);
            var p=new Vector2(x,z);
            var segment=b-a;
            if (segment.LengthSquared()<.0001f) continue;
            float t=MathHelper.Clamp(Vector2.Dot(p-a,segment)/segment.LengthSquared(),0,1);
            if (Vector2.DistanceSquared(p,a+segment*t)<=Width*Width*.25f) return true;
        }
        return false;
    }
}

public sealed class VillageBuilding
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Interior { get; init; } = "";
    public string Zone { get; init; } = "";
    public float X { get; init; }
    public float Z { get; init; }
    public float Width { get; init; }
    public float Depth { get; init; }
    public float Height { get; init; }
    public float[] Approach { get; init; } = [];
    public bool IntersectsDisc(float x,float z,float radius)
    {
        float nearX=MathHelper.Clamp(x,X-Width/2,X+Width/2);
        float nearZ=MathHelper.Clamp(z,Z-Depth/2,Z+Depth/2);
        return (x-nearX)*(x-nearX)+(z-nearZ)*(z-nearZ)<=radius*radius;
    }
}
