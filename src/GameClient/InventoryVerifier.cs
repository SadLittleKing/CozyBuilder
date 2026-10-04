using Game.Assets;
using Microsoft.Xna.Framework;
using System.Text.Json;

namespace GameClient;

public static class InventoryVerifier
{
    public static void Verify(WorldGraph graph,GlbScene villager)
    {
        var character=new CharacterAssembly(villager,new CharacterConfiguration());
        character.VerifyGearVariants();
        var state=new WorldState(graph,"interior:residence",new Vector2(-2.35f,1.8f));
        string chest=state.NearbyChest()??throw new InvalidDataException("Residence chest cannot be opened.");
        if(state.Inventory.Chest(chest).Stacks.Count!=4)throw new InvalidDataException("Residence starter chest is missing gear.");
        foreach(var (starter,upgrade,slot) in new[]{
            ("padded_hood","iron_helm",GearSlot.Head),("padded_vest","iron_cuirass",GearSlot.Body),
            ("leather_gloves","iron_gauntlets",GearSlot.Gloves),("trail_boots","iron_boots",GearSlot.Feet)})
        {
            if(!state.Inventory.TryEquip(starter,out _)||state.Inventory.EquippedItem(slot)!=starter||
               !state.Inventory.TryTransfer(chest,false,upgrade,1,out _)||
               !state.Inventory.TryEquip(upgrade,out _)||state.Inventory.EquippedItem(slot)!=upgrade||
               state.Inventory.Bag.Count(starter)!=1)
                throw new InvalidDataException($"{slot} equip, chest withdrawal or swap failed.");
            character.SelectGear(state.Inventory.Visuals);
            if(!character.VisibleParts.Any(p=>p.Name.StartsWith(slot switch
               {GearSlot.Head=>"Gear_Head_Helm__",GearSlot.Body=>"Gear_Body_Cuirass__",
                GearSlot.Gloves=>"Gear_Gloves_Iron__",_=>"Gear_Feet_Iron__"},StringComparison.Ordinal)))
                throw new InvalidDataException($"{slot} did not select its visible mesh.");
        }
        if(!state.Inventory.TryTransfer(chest,true,"common_seed",2,out _)||
           state.Inventory.Bag.Count("common_seed")!=2||state.Inventory.Chest(chest).Count("common_seed")!=2||
           !state.Inventory.TryTransfer(chest,false,"common_seed",1,out _)||
           state.Inventory.Bag.Count("common_seed")!=3||state.Inventory.Chest(chest).Count("common_seed")!=1)
            throw new InvalidDataException("Chest stack deposit or withdrawal lost items.");
        var other=state.Inventory.Chest("interior:west_home:chest:2");
        if(other.Stacks.Count!=0)throw new InvalidDataException("Home and residence chest contents leaked between instances.");
        var chestIds=new HashSet<string>(StringComparer.Ordinal){chest};
        foreach(string home in new[]{"west_home","east_home","west_cottage","east_cottage"})
        {
            string map="interior:"+home;
            var approach=new Vector2(3.7f,-2.0f);
            if(!graph.CanWalk(map,approach))throw new InvalidDataException($"{home} chest approach is blocked.");
            var probe=new WorldState(graph,map,approach);
            string id=probe.NearbyChest()??throw new InvalidDataException($"{home} chest cannot be opened.");
            chestIds.Add(id);
            if(state.Inventory.Chest(id).Stacks.Count!=0)throw new InvalidDataException("Home chest is not independently empty.");
        }
        if(chestIds.Count!=5)throw new InvalidDataException("Expected five independent chest IDs.");
        var menuWorld=new WorldState(graph,"interior:residence",new Vector2(-2.35f,1.8f));
        var menu=new InventoryMenu();menu.ShowChest(chest);
        if(!menu.Activate(menuWorld,false,out _)||menuWorld.Inventory.Bag.Count("iron_helm")!=1)
            throw new InvalidDataException("Chest menu Enter did not withdraw selected gear.");
        menu.NextPane();menu.NextPane();menu.MoveSelection(menuWorld,-1);
        if(!menu.EquipSelected(menuWorld,out _)||menuWorld.Inventory.EquippedItem(GearSlot.Head)!="iron_helm")
            throw new InvalidDataException("Inventory menu did not equip withdrawn head gear.");
        var clickWorld=new WorldState(graph,"interior:residence",new Vector2(-2.35f,1.8f));
        var clickMenu=new InventoryMenu();clickMenu.ShowBag();
        var layout=new InventoryLayout(1100,850);
        int hoodIndex=clickWorld.Inventory.Bag.Stacks.ToList().FindIndex(s=>s.Id=="padded_hood");
        if(!clickMenu.Click(clickWorld,layout.BagRow(hoodIndex).Center,1100,850,true,out _)||
           clickWorld.Inventory.EquippedItem(GearSlot.Head)!="padded_hood"||
           !clickMenu.Click(clickWorld,layout.GearRow(0).Center,1100,850,true,out _)||
           clickWorld.Inventory.EquippedItem(GearSlot.Head)!=null||clickWorld.Inventory.Bag.Count("padded_hood")!=1)
            throw new InvalidDataException("Right-click equip or unequip failed.");
        var full=new ItemContainer(1);full.TryAdd("padded_hood",1);
        var source=new ItemContainer(2);source.TryAdd("rare_seed",1);
        if(source.TryTransferTo(full,"rare_seed",1)||source.Count("rare_seed")!=1||full.Count("padded_hood")!=1||
           source.TryTransferTo(full,"rare_seed",0)||source.TryTransferTo(full,"rare_seed",2))
            throw new InvalidDataException("Full chest or invalid transfer changed item counts.");
        var fullSave=new MazeSave {Seed=graph.Maze.Seed,MapId=graph.Maze.MapId(graph.Maze.RareNode),
            Inventory=Enumerable.Repeat(new ItemStack("padded_hood",1),InventoryState.BagCapacity).ToList()};
        var fullWorld=new WorldState(graph,fullSave.MapId,new Vector2(0,0),fullSave);
        if(fullWorld.TryCollect(out _)||fullWorld.RareCollected||fullWorld.Inventory.Bag.Count("rare_seed")!=0)
            throw new InvalidDataException("Full bag consumed a one-time pickup.");
        fullWorld.Inventory.Bag.TryRemove("padded_hood",1);
        if(!fullWorld.TryCollect(out _)||!fullWorld.RareCollected||fullWorld.Inventory.Bag.Count("rare_seed")!=1)
            throw new InvalidDataException("Rare pickup did not recover after space was freed.");
        var equipFull=new InventoryState(null);
        equipFull.TryEquip("padded_hood",out _);
        while(equipFull.Bag.Stacks.Count<InventoryState.BagCapacity)equipFull.Bag.TryAdd("padded_hood",1);
        if(equipFull.TryUnequip(GearSlot.Head,out _)||equipFull.EquippedItem(GearSlot.Head)!="padded_hood")
            throw new InvalidDataException("Full bag lost equipped head item.");
        string temp=Path.Combine(Path.GetTempPath(),$"game-inventory-{Guid.NewGuid():N}.json");
        try
        {
            state.Snapshot().Write(temp);
            var saved=MazeSave.Load(temp);
            var restored=new WorldState(graph,saved.MapId,new Vector2(saved.X,saved.Z),saved);
            if(restored.Inventory.EquippedItem(GearSlot.Head)!="iron_helm"||
               restored.Inventory.EquippedItem(GearSlot.Body)!="iron_cuirass"||
               restored.Inventory.EquippedItem(GearSlot.Gloves)!="iron_gauntlets"||
               restored.Inventory.EquippedItem(GearSlot.Feet)!="iron_boots"||
               restored.Inventory.Bag.Count("common_seed")!=3||restored.Inventory.Chest(chest).Count("common_seed")!=1||
               restored.Inventory.Chest("interior:west_home:chest:2").Stacks.Count!=0)
                throw new InvalidDataException("Inventory, gear or independent chests changed after save/reload.");
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
        var farm=new WorldState(graph,"homestead",new Vector2(graph.Homestead.Plots[0][0],graph.Homestead.Plots[0][1]));
        for(int i=0;i<4;i++)if(!farm.TryFarm(out _))throw new InvalidDataException("Farm seed cycle failed.");
        if(farm.Inventory.Bag.Count("common_seed")!=4||farm.Inventory.Bag.Count("crop")!=1||farm.HarvestCount!=1)
            throw new InvalidDataException("Farm planting/harvest inventory balance failed.");
        var farmSave=farm.Snapshot();
        var farmRestored=new WorldState(graph,farmSave.MapId,new Vector2(farmSave.X,farmSave.Z),farmSave);
        if(farmRestored.PlotStage(0)!=FarmStage.Tilled||farmRestored.HarvestCount!=1||farmRestored.Inventory.Bag.Count("crop")!=1)
            throw new InvalidDataException("Farm and crop state changed after reload.");
        string legacyPath=Path.Combine(Path.GetTempPath(),$"game-old-inventory-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(legacyPath,JsonSerializer.Serialize(new {Version=1,Seed=graph.Maze.Seed,MapId="village",
                X=0f,Z=3f,Discovered=new[]{0},RareCollected=true,CommonCollected=true,ShrineUnlocked=false}));
            var legacy=new WorldState(graph,"village",null,MazeSave.Load(legacyPath));
            if(legacy.Inventory.Bag.Count("rare_seed")!=1||legacy.Inventory.Bag.Count("common_seed")!=7)
                throw new InvalidDataException("Old maze pickup flags did not migrate to real seed items.");
        }
        finally{if(File.Exists(legacyPath))File.Delete(legacyPath);}
        Console.WriteLine("Verified inventory stacks, four visible gear slots, five independent chest instances, atomic transfers, full-bag pickups, seed farming and save compatibility.");
    }
}
