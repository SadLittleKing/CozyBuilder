using Game.Assets;

namespace GameClient;

public sealed record ItemDefinition(string Id,string Name,int MaxStack,GearSlot? Slot=null);
public sealed record ItemStack(string Id,int Quantity);

public static class ItemCatalog
{
    public static readonly ItemDefinition[] All=
    [
        new("common_seed","COMMON SEED",99),new("rare_seed","RARE SEED",10),new("crop","FARM CROP",99),
        new("wood","WOOD",99),new("stone","STONE",99),new("ore","ORE",99),
        new("apple","RED APPLE",99),new("berry","BLUE BERRY",99),new("plum","PURPLE PLUM",99),
        new("padded_hood","PADDED HOOD",1,GearSlot.Head),new("iron_helm","IRON HELM",1,GearSlot.Head),
        new("padded_vest","PADDED VEST",1,GearSlot.Body),new("iron_cuirass","IRON CUIRASS",1,GearSlot.Body),
        new("leather_gloves","LEATHER GLOVES",1,GearSlot.Gloves),new("iron_gauntlets","IRON GAUNTLETS",1,GearSlot.Gloves),
        new("trail_boots","TRAIL BOOTS",1,GearSlot.Feet),new("iron_boots","IRON BOOTS",1,GearSlot.Feet)
    ];
    static readonly Dictionary<string,ItemDefinition> ById=All.ToDictionary(i=>i.Id,StringComparer.Ordinal);
    public static ItemDefinition Get(string id)=>ById.TryGetValue(id,out var definition)?definition:
        throw new InvalidDataException($"Unknown item ID: {id}");
}

public sealed class ItemContainer
{
    public int Capacity { get; }
    readonly List<ItemStack> stacks=[];
    public IReadOnlyList<ItemStack> Stacks=>stacks;
    public ItemContainer(int capacity,IEnumerable<ItemStack>? initial=null)
    {
        Capacity=capacity;
        if(initial!=null)foreach(var stack in initial)
            if(!TryAdd(stack.Id,stack.Quantity))throw new InvalidDataException("Invalid or overfull item container.");
    }
    public int Count(string id)=>stacks.Where(s=>s.Id==id).Sum(s=>s.Quantity);
    public bool CanAdd(string id,int quantity)
    {
        if(quantity<=0)return false;
        int maximum=ItemCatalog.Get(id).MaxStack;
        int room=stacks.Where(s=>s.Id==id).Sum(s=>maximum-s.Quantity)+(Capacity-stacks.Count)*maximum;
        return room>=quantity;
    }
    public bool TryAdd(string id,int quantity)
    {
        if(!CanAdd(id,quantity))return false;
        int maximum=ItemCatalog.Get(id).MaxStack;
        for(int i=0;i<stacks.Count&&quantity>0;i++)if(stacks[i].Id==id&&stacks[i].Quantity<maximum)
        {
            int amount=Math.Min(quantity,maximum-stacks[i].Quantity);
            stacks[i]=stacks[i] with {Quantity=stacks[i].Quantity+amount};quantity-=amount;
        }
        while(quantity>0)
        {
            int amount=Math.Min(quantity,maximum);stacks.Add(new ItemStack(id,amount));quantity-=amount;
        }
        return true;
    }
    public bool TryRemove(string id,int quantity)
    {
        if(quantity<=0||Count(id)<quantity)return false;
        for(int i=stacks.Count-1;i>=0&&quantity>0;i--)if(stacks[i].Id==id)
        {
            int amount=Math.Min(quantity,stacks[i].Quantity);quantity-=amount;
            int remaining=stacks[i].Quantity-amount;
            if(remaining==0)stacks.RemoveAt(i);else stacks[i]=stacks[i] with {Quantity=remaining};
        }
        return true;
    }
    public bool TryTransferTo(ItemContainer destination,string id,int quantity)
    {
        if(quantity<=0||Count(id)<quantity||!destination.CanAdd(id,quantity))return false;
        if(!TryRemove(id,quantity)||!destination.TryAdd(id,quantity))
            throw new InvalidOperationException("Validated item transfer failed.");
        return true;
    }
    public bool CanAddBoth(string first,int firstAmount,string second,int secondAmount)
    {
        var copy=new ItemContainer(Capacity,stacks);
        return copy.TryAdd(first,firstAmount)&&copy.TryAdd(second,secondAmount);
    }
    internal void Replace(ItemContainer other)
    {
        stacks.Clear();stacks.AddRange(other.stacks);
    }
    public List<ItemStack> Snapshot()=>stacks.Select(s=>new ItemStack(s.Id,s.Quantity)).ToList();
}

public sealed class InventoryState
{
    public const int BagCapacity=16,ChestCapacity=20;
    public ItemContainer Bag { get; }
    readonly Dictionary<string,ItemContainer> chests=new(StringComparer.Ordinal);
    readonly Dictionary<GearSlot,string?> equipped=Enum.GetValues<GearSlot>().ToDictionary(slot=>slot,_=>(string?)null);
    public IReadOnlyDictionary<GearSlot,string?> Equipped=>equipped;
    public InventoryState(MazeSave? save)
    {
        if(save?.Inventory==null)
        {
            Bag=new ItemContainer(BagCapacity);
            foreach(var stack in new[]{new ItemStack("common_seed",4),new ItemStack("padded_hood",1),
                new ItemStack("padded_vest",1),new ItemStack("leather_gloves",1),new ItemStack("trail_boots",1)})
                Bag.TryAdd(stack.Id,stack.Quantity);
            if(save?.CommonCollected==true)Bag.TryAdd("common_seed",3);
            if(save?.RareCollected==true)Bag.TryAdd("rare_seed",1);
            var starterChest=new ItemContainer(ChestCapacity);
            foreach(string item in new[]{"iron_helm","iron_cuirass","iron_gauntlets","iron_boots"})starterChest.TryAdd(item,1);
            chests["interior:residence:chest:2"]=starterChest;
        }
        else
        {
            Bag=new ItemContainer(BagCapacity,save.Inventory);
            if(save.Equipment!=null)foreach(var (name,id) in save.Equipment)
            {
                if(!Enum.TryParse<GearSlot>(name,true,out var slot)||!equipped.ContainsKey(slot)||
                   id!=null&&ItemCatalog.Get(id).Slot!=slot)
                    throw new InvalidDataException("Invalid equipped item in save.");
                equipped[slot]=id;
            }
            if(save.Chests!=null)foreach(var (id,contents) in save.Chests)
                chests.Add(id,new ItemContainer(ChestCapacity,contents));
        }
    }
    public ItemContainer Chest(string id)
    {
        if(!chests.TryGetValue(id,out var container))chests[id]=container=new ItemContainer(ChestCapacity);
        return container;
    }
    public string? EquippedItem(GearSlot slot)=>equipped[slot];
    public GearVisualConfiguration Visuals=>new(
        equipped[GearSlot.Head]??"none",equipped[GearSlot.Body]??"none",
        equipped[GearSlot.Gloves]??"none",equipped[GearSlot.Feet]??"none");
    public bool TryEquip(string id,out string message)
    {
        var definition=ItemCatalog.Get(id);
        if(definition.Slot is not GearSlot slot){message="ITEM CANNOT BE EQUIPPED";return false;}
        if(Bag.Count(id)<1){message="ITEM IS NOT IN BAG";return false;}
        var trial=new ItemContainer(BagCapacity,Bag.Stacks);
        trial.TryRemove(id,1);
        string? previous=equipped[slot];
        if(previous!=null&&!trial.TryAdd(previous,1)){message="BAG FULL / CANNOT SWAP";return false;}
        Bag.Replace(trial);equipped[slot]=id;
        message="EQUIPPED "+definition.Name;return true;
    }
    public bool TryUnequip(GearSlot slot,out string message)
    {
        string? id=equipped[slot];
        if(id==null){message="SLOT IS EMPTY";return false;}
        if(!Bag.TryAdd(id,1)){message="BAG FULL / CANNOT UNEQUIP";return false;}
        equipped[slot]=null;message="UNEQUIPPED "+ItemCatalog.Get(id).Name;return true;
    }
    public bool TryTransfer(string chestId,bool bagToChest,string id,int quantity,out string message)
    {
        var source=bagToChest?Bag:Chest(chestId);
        var destination=bagToChest?Chest(chestId):Bag;
        if(!source.TryTransferTo(destination,id,quantity))
        {message="TRANSFER FAILED / CHECK COUNT OR SPACE";return false;}
        message=(bagToChest?"STORED ":"TOOK ")+quantity+" "+ItemCatalog.Get(id).Name;return true;
    }
    public Dictionary<string,string?> EquipmentSnapshot()=>equipped.ToDictionary(p=>p.Key.ToString(),p=>p.Value,StringComparer.Ordinal);
    public Dictionary<string,List<ItemStack>> ChestSnapshot()=>chests.ToDictionary(p=>p.Key,p=>p.Value.Snapshot(),StringComparer.Ordinal);
}
