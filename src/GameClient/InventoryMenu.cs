using Game.Assets;
using Microsoft.Xna.Framework;

namespace GameClient;

public enum InventoryPane { Bag, Chest, Equipment }

public sealed class InventoryMenu
{
    public bool Open { get; private set; }
    public string? ChestId { get; private set; }
    public InventoryPane Pane { get; private set; }
    public int Selection { get; private set; }
    public void ShowBag(){Open=true;ChestId=null;Pane=InventoryPane.Bag;Selection=0;}
    public void ShowChest(string id){Open=true;ChestId=id;Pane=InventoryPane.Chest;Selection=0;}
    public void Close(){Open=false;ChestId=null;Selection=0;}
    public void NextPane()
    {
        Pane=Pane switch
        {
            InventoryPane.Bag=>ChestId==null?InventoryPane.Equipment:InventoryPane.Chest,
            InventoryPane.Chest=>InventoryPane.Equipment,
            _=>InventoryPane.Bag
        };
        Selection=0;
    }
    public void MoveSelection(WorldState world,int direction)
    {
        int count=Pane switch
        {
            InventoryPane.Bag=>world.Inventory.Bag.Stacks.Count,
            InventoryPane.Chest=>ChestId==null?0:world.Inventory.Chest(ChestId).Stacks.Count,
            _=>4
        };
        Selection=count==0?0:(Selection+direction+count)%count;
    }
    public bool Activate(WorldState world,bool fullStack,out string message)
    {
        if(Pane==InventoryPane.Equipment)
            return world.Inventory.TryUnequip((GearSlot)Selection,out message);
        var source=Pane==InventoryPane.Bag?world.Inventory.Bag:ChestId==null?null:world.Inventory.Chest(ChestId);
        if(source==null||Selection>=source.Stacks.Count)
        {message="NO ITEM SELECTED";return false;}
        var stack=source.Stacks[Selection];
        bool changed;
        if(ChestId==null)
            changed=world.Inventory.TryEquip(stack.Id,out message);
        else
            changed=world.Inventory.TryTransfer(ChestId,Pane==InventoryPane.Bag,stack.Id,fullStack?stack.Quantity:1,out message);
        int remaining=Pane==InventoryPane.Bag?world.Inventory.Bag.Stacks.Count:world.Inventory.Chest(ChestId!).Stacks.Count;
        if(Selection>=remaining)Selection=Math.Max(0,remaining-1);
        return changed;
    }
    public bool EquipSelected(WorldState world,out string message)
    {
        if(Pane!=InventoryPane.Bag||Selection>=world.Inventory.Bag.Stacks.Count)
        {message="SELECT GEAR IN YOUR BAG";return false;}
        bool changed=world.Inventory.TryEquip(world.Inventory.Bag.Stacks[Selection].Id,out message);
        if(changed&&Selection>=world.Inventory.Bag.Stacks.Count)
            Selection=Math.Max(0,world.Inventory.Bag.Stacks.Count-1);
        return changed;
    }
    public bool Click(WorldState world,Point point,int width,int height,bool rightClick,out string message)
    {
        var layout=new InventoryLayout(width,height);
        for(int i=0;i<InventoryState.BagCapacity;i++)if(layout.BagRow(i).Contains(point))
        {
            Pane=InventoryPane.Bag;Selection=i;
            if(!rightClick){message="";return false;}
            return EquipSelected(world,out message);
        }
        if(ChestId!=null)for(int i=0;i<InventoryState.ChestCapacity;i++)if(layout.ChestRow(i).Contains(point))
        {
            Pane=InventoryPane.Chest;Selection=i;
            message="";return false;
        }
        for(int i=0;i<4;i++)if(layout.GearRow(i).Contains(point))
        {
            Pane=InventoryPane.Equipment;Selection=i;
            if(!rightClick){message="";return false;}
            return world.Inventory.TryUnequip((GearSlot)i,out message);
        }
        message="";return false;
    }
}
