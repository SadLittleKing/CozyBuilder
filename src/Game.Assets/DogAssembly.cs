using System.Text.Json;

namespace Game.Assets;

public enum DogSlot { Collar, Helmet, Armor, Backpack }

public sealed record DogConfiguration(bool Collar=true,bool Helmet=false,bool Armor=false,bool Backpack=false)
{
    public DogConfiguration Toggle(DogSlot slot)=>slot switch
    {
        DogSlot.Collar=>this with {Collar=!Collar},
        DogSlot.Helmet=>this with {Helmet=!Helmet},
        DogSlot.Armor=>this with {Armor=!Armor},
        DogSlot.Backpack=>this with {Backpack=!Backpack},
        _=>throw new ArgumentOutOfRangeException(nameof(slot))
    };
    public static DogConfiguration Load(string path)=>JsonSerializer.Deserialize<DogConfiguration>(File.ReadAllText(path))
        ?? throw new InvalidDataException("Empty dog configuration.");
    public void Save(string path)=>File.WriteAllText(path,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
}

public sealed class DogAssembly
{
    public GlbScene Library { get; }
    public DogConfiguration Configuration { get; private set; }=new();
    public IReadOnlyList<MeshPart> VisibleParts { get; private set; }=[];
    static readonly string[] Prefixes=["Dog_Base__","Dog_Collar__","Dog_Helmet__","Dog_Armor__","Dog_Backpack__"];
    public DogAssembly(GlbScene library,DogConfiguration configuration)
    {
        Library=library;
        foreach(string prefix in Prefixes)
            if(!library.Parts.Any(p=>p.Name.StartsWith(prefix,StringComparison.Ordinal)))
                throw new InvalidDataException($"Dog module missing {prefix}");
        if(!library.NodeTransforms.ContainsKey("Socket_Dog_Backpack"))
            throw new InvalidDataException("Dog backpack socket missing.");
        if(library.Parts.Any(p=>!Prefixes.Any(prefix=>p.Name.StartsWith(prefix,StringComparison.Ordinal))))
            throw new InvalidDataException("Unexpected dog module name.");
        Select(configuration);
    }
    public void Select(DogConfiguration configuration)
    {
        Configuration=configuration;
        VisibleParts=Library.Parts.Where(p=>p.Name.StartsWith("Dog_Base__",StringComparison.Ordinal)||
            configuration.Collar&&p.Name.StartsWith("Dog_Collar__",StringComparison.Ordinal)||
            configuration.Helmet&&p.Name.StartsWith("Dog_Helmet__",StringComparison.Ordinal)||
            configuration.Armor&&p.Name.StartsWith("Dog_Armor__",StringComparison.Ordinal)||
            configuration.Backpack&&p.Name.StartsWith("Dog_Backpack__",StringComparison.Ordinal)).ToArray();
    }
    public void Toggle(DogSlot slot)=>Select(Configuration.Toggle(slot));
    public void VerifyModules()
    {
        if(Library.Min.Y<-.05f||Library.Max.Y<.6f||Library.Max.Y>1.5f||
           Library.Max.Z-Library.Min.Z<1f||Library.Max.X-Library.Min.X>2f)
            throw new InvalidDataException("Dog export has unexpected scale or axis.");
        if(Library.Parts.Count(p=>p.Name=="Dog_Base__Head")!=1)
            throw new InvalidDataException("Dog needs exactly one base head.");
        var socket=Library.NodeTransforms["Socket_Dog_Backpack"].Translation;
        if(socket.Y<.65f||socket.Y>1.2f||MathF.Abs(socket.X)>.1f)
            throw new InvalidDataException("Dog backpack socket is not above the spine.");
        var original=Configuration;
        try
        {
            for(int mask=0;mask<16;mask++)
            {
                var config=new DogConfiguration((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0);
                Select(config);
                if(VisibleParts.Count(p=>p.Name=="Dog_Base__Head")!=1||
                   VisibleParts.Any(p=>!Library.Parts.Any(originalPart=>ReferenceEquals(p,originalPart))))
                    throw new InvalidDataException("Dog base was duplicated or copied.");
                foreach(var slot in Enum.GetValues<DogSlot>())
                {
                    string prefix=slot switch {DogSlot.Collar=>"Dog_Collar__",DogSlot.Helmet=>"Dog_Helmet__",
                        DogSlot.Armor=>"Dog_Armor__",_=>"Dog_Backpack__"};
                    bool enabled=slot switch {DogSlot.Collar=>config.Collar,DogSlot.Helmet=>config.Helmet,
                        DogSlot.Armor=>config.Armor,_=>config.Backpack};
                    if(VisibleParts.Any(p=>p.Name.StartsWith(prefix,StringComparison.Ordinal))!=enabled)
                        throw new InvalidDataException($"Dog {slot} mask failed.");
                    var selected=VisibleParts;
                    Toggle(slot);Toggle(slot);
                    if(Configuration!=config||!VisibleParts.SequenceEqual(selected))
                        throw new InvalidDataException($"Dog {slot} toggle failed to restore.");
                }
            }
        }
        finally{Select(original);}
        Console.WriteLine($"Verified dog library: {Library.Parts.Count} named parts, 16 accessory variants, backpack socket {socket}, bounds {Library.Min} to {Library.Max}.");
    }
}
