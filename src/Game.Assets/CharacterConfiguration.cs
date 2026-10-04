using System.Text.Json;

namespace Game.Assets;

public enum CharacterSlot { Hair, Outfit, Hand, Back }
public enum GearSlot { Head, Body, Gloves, Feet }

public sealed record GearVisualConfiguration(string Head="none",string Body="none",string Gloves="none",string Feet="none")
{
    public void Validate()
    {
        if(Head is not ("none" or "padded_hood" or "iron_helm")||
           Body is not ("none" or "padded_vest" or "iron_cuirass")||
           Gloves is not ("none" or "leather_gloves" or "iron_gauntlets")||
           Feet is not ("none" or "trail_boots" or "iron_boots"))
            throw new InvalidDataException("Unknown equipment visual ID.");
    }
    public GearVisualConfiguration Cycle(GearSlot slot)
    {
        static string Next(string id,string first,string second)=>id=="none"?first:id==first?second:"none";
        return slot switch
        {
            GearSlot.Head=>this with {Head=Next(Head,"padded_hood","iron_helm")},
            GearSlot.Body=>this with {Body=Next(Body,"padded_vest","iron_cuirass")},
            GearSlot.Gloves=>this with {Gloves=Next(Gloves,"leather_gloves","iron_gauntlets")},
            GearSlot.Feet=>this with {Feet=Next(Feet,"trail_boots","iron_boots")},
            _=>throw new ArgumentOutOfRangeException(nameof(slot))
        };
    }
}

// A character references one shared body and module IDs, never a combination GLB.
public sealed record CharacterConfiguration(
    string Body = "villager.base", string Hair = "swept", string Outfit = "sage",
    string Hand = "none", string Back = "none")
{
    public CharacterConfiguration Cycle(CharacterSlot slot) => slot switch
    {
        CharacterSlot.Hair => this with { Hair = Hair == "swept" ? "bun" : "swept" },
        CharacterSlot.Outfit => this with { Outfit = Outfit == "sage" ? "rust" : "sage" },
        CharacterSlot.Hand => this with { Hand = Hand == "none" ? "sword" : "none" },
        CharacterSlot.Back => this with { Back = Back == "none" ? "backpack" : "none" },
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public void Validate()
    {
        if (Body != "villager.base" || Hair is not ("swept" or "bun") || Outfit is not ("sage" or "rust") ||
            Hand is not ("none" or "sword") || Back is not ("none" or "backpack"))
            throw new InvalidDataException("Unknown character module ID. Expected villager.base; swept/bun; sage/rust; none/sword; none/backpack.");
    }
    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this,new JsonSerializerOptions { WriteIndented = true }));
    public static CharacterConfiguration Load(string path)
    {
        var config = JsonSerializer.Deserialize<CharacterConfiguration>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty character configuration.");
        config.Validate();
        return config;
    }
}

public sealed class CharacterAssembly
{
    public GlbScene Library { get; }
    public bool IsModular { get; }
    public CharacterConfiguration Configuration { get; private set; }
    public GearVisualConfiguration Gear { get; private set; }=new();
    public IReadOnlyList<MeshPart> VisibleParts { get; private set; } = [];

    public CharacterAssembly(GlbScene library, CharacterConfiguration configuration)
    {
        Library = library;
        IsModular = library.Parts.Any(p => p.Name.StartsWith("Shared__",StringComparison.Ordinal));
        if (IsModular)
        {
            foreach (string prefix in new[] { "Hair_Swept__", "Hair_Bun__", "Outfit_Sage__", "Outfit_Rust__", "Equipment_Sword__", "Equipment_Backpack__" })
                if (!library.Parts.Any(p => p.Name.StartsWith(prefix,StringComparison.Ordinal))) throw new InvalidDataException($"Missing module {prefix}");
            foreach (string socket in new[] { "Socket_Hand_R", "Socket_Back" })
                if (!library.NodeTransforms.ContainsKey(socket)) throw new InvalidDataException($"Missing attachment {socket}");
        }
        Configuration = configuration;
        Select(configuration);
    }

    public void Select(CharacterConfiguration configuration)
    {
        configuration.Validate();
        Configuration = configuration;
        VisibleParts = IsModular ? Library.Parts.Where(IsVisible).ToArray() : Library.Parts;
    }

    public void Cycle(CharacterSlot slot) => Select(Configuration.Cycle(slot));
    public void SelectGear(GearVisualConfiguration gear)
    {
        gear.Validate();
        if(gear!=new GearVisualConfiguration()&&
           !Library.Parts.Any(p=>p.Name.StartsWith("Gear_Head_",StringComparison.Ordinal)))
            throw new InvalidDataException("This villager GLB does not contain equipment meshes.");
        Gear=gear;Select(Configuration);
    }
    public void CycleGear(GearSlot slot)=>SelectGear(Gear.Cycle(slot));

    bool IsVisible(MeshPart part)
    {
        string name = part.Name;
        if (name.StartsWith("Shared__",StringComparison.Ordinal))
        {
            // Both fitted outfits replace the torso surface. The long sleeve
            // outfit additionally replaces upper arms and forearms; hands stay.
            if (name == "Shared__Body_Torso") return false;
            if (Configuration.Outfit == "rust" && (name.StartsWith("Shared__Body_UpperArm_",StringComparison.Ordinal) || name.StartsWith("Shared__Body_Forearm_",StringComparison.Ordinal))) return false;
            if(Gear.Gloves!="none"&&(name.StartsWith("Shared__Body_Hand_",StringComparison.Ordinal)||name.StartsWith("Shared__Body_Thumb_",StringComparison.Ordinal)))return false;
            if(Gear.Feet!="none"&&name.StartsWith("Shared__Footwear_",StringComparison.Ordinal))return false;
            return true;
        }
        return (Gear.Head=="none"&&name.StartsWith(Configuration.Hair == "swept" ? "Hair_Swept__" : "Hair_Bun__",StringComparison.Ordinal))
            || name.StartsWith(Configuration.Outfit == "sage" ? "Outfit_Sage__" : "Outfit_Rust__",StringComparison.Ordinal)
            || (Configuration.Hand == "sword" && name.StartsWith("Equipment_Sword__",StringComparison.Ordinal))
            || (Configuration.Back == "backpack" && name.StartsWith("Equipment_Backpack__",StringComparison.Ordinal))
            || name.StartsWith(Gear.Head switch {"padded_hood"=>"Gear_Head_Hood__","iron_helm"=>"Gear_Head_Helm__",_=>"~"},StringComparison.Ordinal)
            || name.StartsWith(Gear.Body switch {"padded_vest"=>"Gear_Body_Vest__","iron_cuirass"=>"Gear_Body_Cuirass__",_=>"~"},StringComparison.Ordinal)
            || name.StartsWith(Gear.Gloves switch {"leather_gloves"=>"Gear_Gloves_Leather__","iron_gauntlets"=>"Gear_Gloves_Iron__",_=>"~"},StringComparison.Ordinal)
            || name.StartsWith(Gear.Feet switch {"trail_boots"=>"Gear_Feet_Trail__","iron_boots"=>"Gear_Feet_Iron__",_=>"~"},StringComparison.Ordinal);
    }

    // A headless integration check over the actual exported library, including
    // reversibility of the exact Cycle operation used by keyboard and mouse UI.
    public void VerifyAllCombinations()
    {
        if (!IsModular) throw new InvalidOperationException("Swap verification requires the modular library.");
        var saved = Configuration;
        var savedGear=Gear;
        int combinations = 0;
        try
        {
            SelectGear(new GearVisualConfiguration());
            foreach (string hair in new[] { "swept", "bun" })
            foreach (string outfit in new[] { "sage", "rust" })
            foreach (string hand in new[] { "none", "sword" })
            foreach (string back in new[] { "none", "backpack" })
            {
                var config = new CharacterConfiguration(Hair:hair,Outfit:outfit,Hand:hand,Back:back);
                Select(config);
                var parts = VisibleParts;
                void Require(bool condition,string message) { if (!condition) throw new InvalidDataException($"{config}: {message}"); }
                Require(parts.Count(p => p.Name == "Shared__Body_Head") == 1,"Expected exactly one shared head.");
                Require(parts.Count(p => p.Name.StartsWith("Outfit_",StringComparison.Ordinal) && p.Name.EndsWith("__Tunic",StringComparison.Ordinal)) == 1,"Expected one tunic.");
                Require(parts.Count(p => p.Name.StartsWith("Hair_",StringComparison.Ordinal) && p.Name.EndsWith("__Cap",StringComparison.Ordinal)) == 1,"Expected one hair cap.");
                Require(parts.All(p => p.Name != "Shared__Body_Torso"),"Torso must be covered.");
                Require(parts.Any(p => p.Name == "Shared__Body_Forearm_R") == (outfit == "sage"),"Sleeve/body mask mismatch.");
                Require(parts.Any(p => p.Name == "Equipment_Sword__Blade") == (hand == "sword"),"Sword visibility mismatch.");
                Require(parts.Any(p => p.Name == "Equipment_Backpack__Bag") == (back == "backpack"),"Backpack visibility mismatch.");
                Require(parts.All(p => Library.Parts.Any(source => ReferenceEquals(source,p))),"Geometry must be shared, not cloned per character.");
                foreach (var slot in Enum.GetValues<CharacterSlot>())
                {
                    Cycle(slot); Require(Configuration != config,"Cycle did not change configuration.");
                    Cycle(slot); Require(Configuration == config,"Cycle did not restore configuration.");
                    Require(VisibleParts.SequenceEqual(parts),"Cycle did not restore visible parts.");
                }
                combinations++;
            }
        }
        finally { SelectGear(savedGear);Select(saved); }
        Console.WriteLine($"Verified {combinations} configurations: shared geometry, outfit masks, equipment, and reversible UI swaps.");
    }
    public void VerifyGearVariants()
    {
        string[] prefixes=["Gear_Head_Hood__","Gear_Head_Helm__","Gear_Body_Vest__","Gear_Body_Cuirass__",
            "Gear_Gloves_Leather__","Gear_Gloves_Iron__","Gear_Feet_Trail__","Gear_Feet_Iron__"];
        foreach(string prefix in prefixes)if(!Library.Parts.Any(p=>p.Name.StartsWith(prefix,StringComparison.Ordinal)))
            throw new InvalidDataException("Missing equipment mesh group "+prefix);
        var original=Gear;int count=0;
        try
        {
            foreach(string head in new[]{"none","padded_hood","iron_helm"})
            foreach(string body in new[]{"none","padded_vest","iron_cuirass"})
            foreach(string gloves in new[]{"none","leather_gloves","iron_gauntlets"})
            foreach(string feet in new[]{"none","trail_boots","iron_boots"})
            {
                var gear=new GearVisualConfiguration(head,body,gloves,feet);SelectGear(gear);
                if(VisibleParts.Count(p=>p.Name=="Shared__Body_Head")!=1||
                   VisibleParts.Any(p=>p.Name.StartsWith("Shared__Body_Hand_",StringComparison.Ordinal))==(gloves!="none")||
                   VisibleParts.Any(p=>p.Name.StartsWith("Shared__Footwear_",StringComparison.Ordinal))==(feet!="none")||
                   VisibleParts.Any(p=>!Library.Parts.Any(source=>ReferenceEquals(source,p))))
                    throw new InvalidDataException("Equipment body mask or shared geometry failed.");
                foreach(var slot in Enum.GetValues<GearSlot>())
                {
                    var selected=VisibleParts;CycleGear(slot);CycleGear(slot);CycleGear(slot);
                    if(Gear!=gear||!VisibleParts.SequenceEqual(selected))
                        throw new InvalidDataException("Equipment cycling failed for "+slot);
                }
                count++;
            }
        }
        finally{SelectGear(original);}
        Console.WriteLine($"Verified {count} visible head/body/gloves/feet combinations and all eight equipment mesh groups.");
    }
}
