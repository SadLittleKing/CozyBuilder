using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient;

// Small original bitmap font: portable, no content compiler or OS font dependency.
public sealed class WorldHud : IDisposable
{
    readonly SpriteBatch batch;
    readonly Texture2D pixel;
    readonly ItemIconAtlas icons;
    static readonly Dictionary<char,string> Glyphs = new();
    static WorldHud()
    {
        string[] rows = [
            "A:01110/10001/10001/11111/10001/10001/10001", "B:11110/10001/10001/11110/10001/10001/11110",
            "C:01111/10000/10000/10000/10000/10000/01111", "D:11110/10001/10001/10001/10001/10001/11110",
            "E:11111/10000/10000/11110/10000/10000/11111", "F:11111/10000/10000/11110/10000/10000/10000",
            "G:01111/10000/10000/10111/10001/10001/01111", "H:10001/10001/10001/11111/10001/10001/10001",
            "I:111/010/010/010/010/010/111", "J:00111/00010/00010/00010/10010/10010/01100",
            "K:10001/10010/10100/11000/10100/10010/10001", "L:10000/10000/10000/10000/10000/10000/11111",
            "M:10001/11011/10101/10101/10001/10001/10001", "N:10001/11001/10101/10011/10001/10001/10001",
            "O:01110/10001/10001/10001/10001/10001/01110", "P:11110/10001/10001/11110/10000/10000/10000",
            "Q:01110/10001/10001/10001/10101/10010/01101", "R:11110/10001/10001/11110/10100/10010/10001",
            "S:01111/10000/10000/01110/00001/00001/11110", "T:11111/00100/00100/00100/00100/00100/00100",
            "U:10001/10001/10001/10001/10001/10001/01110", "V:10001/10001/10001/10001/10001/01010/00100",
            "W:10001/10001/10001/10101/10101/10101/01010", "X:10001/10001/01010/00100/01010/10001/10001",
            "Y:10001/10001/01010/00100/00100/00100/00100", "Z:11111/00001/00010/00100/01000/10000/11111",
            "0:01110/10001/10011/10101/11001/10001/01110", "1:00100/01100/00100/00100/00100/00100/01110",
            "2:01110/10001/00001/00010/00100/01000/11111", "3:11110/00001/00001/01110/00001/00001/11110",
            "4:00010/00110/01010/10010/11111/00010/00010", "5:11111/10000/10000/11110/00001/00001/11110",
            "6:01110/10000/10000/11110/10001/10001/01110", "7:11111/00001/00010/00100/01000/01000/01000",
            "8:01110/10001/10001/01110/10001/10001/01110", "9:01110/10001/10001/01111/00001/00001/01110",
            "-:00000/00000/00000/11111/00000/00000/00000", "+:00000/00100/00100/11111/00100/00100/00000",
            "/:00001/00001/00010/00100/01000/10000/10000", ".:0/0/0/0/0/1/1",
        ];
        foreach (string row in rows) Glyphs[row[0]] = row[2..];
        Glyphs[':'] = "0/1/1/0/1/1/0";
    }
    public WorldHud(GraphicsDevice device,ItemModels models)
    {
        batch = new SpriteBatch(device);
        pixel = new Texture2D(device,1,1);
        pixel.SetData(new[] { Color.White });
        icons=new ItemIconAtlas(device,models);
    }
    public void Draw(WorldState world,Matrix view,Matrix projection,bool overview,bool mazeMap,string dogAdvice,string status,InventoryMenu inventoryMenu,Texture2D? characterPreview)
    {
        int width=batch.GraphicsDevice.Viewport.Width,height=batch.GraphicsDevice.Viewport.Height;
        batch.Begin(samplerState:SamplerState.PointClamp);
        var ink = new Color(241,231,203);
        Fill(new Rectangle(12,12,480,99),new Color(44,58,51,240));
        Text(world.Graph.Title(world.MapId),24,24,ink,1);
        string place=world.MapId switch
        {
            "village" => overview ? "VILLAGE OVERVIEW" : world.Graph.Village.NearbyPlace(world.Position.X,world.Position.Y),
            "homestead" => overview ? "HOMESTEAD OVERVIEW" : "YOUR FARM AND HOUSE",
            _ when WoodedMaze.TryNode(world.MapId,out int node) => $"WOODLAND / NODE {node+1} OF {WoodedMaze.Count}",
            _ => "INTERIOR / ROOFLESS BLOCKOUT"
        };
        Text(place,24,44,new Color(232,207,151),2);
        var portal=world.NearbyPortal();
        var plot=world.NearbyPlot();
        var chest=world.NearbyChest();
        string prompt=chest!=null?"C OPEN CHEST":portal!=null ? "E "+portal.Label : plot!=null ? "F USE FARM PLOT" : "WALK TO A DOOR OR GATE";
        if(WoodedMaze.TryNode(world.MapId,out int current))
        {
            prompt=world.Graph.Maze.ShrineNode==current && Vector2.Distance(world.Position,Vector2.Zero)<1.7f ? "E SHRINE TO VILLAGE" :
                world.Graph.Maze.RareNode==current && !world.RareCollected && Vector2.Distance(world.Position,Vector2.Zero)<1.7f ? "F COLLECT RARE SEED" :
                world.Graph.Maze.CommonNode==current && !world.CommonCollected && Vector2.Distance(world.Position,Vector2.Zero)<1.7f ? "F COLLECT COMMON SEEDS" :
                portal!=null ? "E "+portal.Label : "FOLLOW AN OPEN PATH";
        }
        else if(world.MapId=="village"&&world.ShrineUnlocked&&Vector2.Distance(world.Position,world.Graph.DungeonGate)<1.6f)
            prompt="E MAZE START / J RETURN TO SHRINE";
        if(world.NearbyDrop() is { } drop)prompt="F PICK UP "+ItemCatalog.Get(drop.ItemId).Name+" X"+drop.Quantity;
        else if(world.NearbyResource() is { } resource)prompt="HOLD F "+(resource.Kind is ResourceKind.Rock or ResourceKind.OreRock?"MINE ":"CHOP ")+resource.Name;
        Text(prompt,24,73,new Color(182,201,170),1);
        if(world.HarvestRemaining>0 && world.NearbyResource()!=null)
        {
            int barX=width/2-126,barY=height-103;
            Fill(new Rectangle(barX-5,barY-19,262,48),new Color(35,49,43,239));
            Text($"{world.HarvestRemaining:0.0} SEC",barX,barY-15,ink,1);
            Fill(new Rectangle(barX,barY,252,17),new Color(106,86,68));
            Fill(new Rectangle(barX,barY,(int)(252*world.HarvestFraction),17),new Color(207,168,98));
        }
        Text(dogAdvice,24,91,new Color(226,201,145),1);
        if(world.MapId=="village")
        {
            foreach(var b in world.Graph.Village.Buildings)
            {
                if(!overview && Vector2.Distance(world.Position,new Vector2(b.X,b.Z))>16)continue;
                Label(b.Label,new Vector3(b.X,b.Height+.55f,b.Z));
            }
            var gate=world.Graph.Homestead.VillageApproach;
            Label("HOMESTEAD",new Vector3(gate[0],1.5f,gate[1]-1.9f));
            var dungeon=world.Graph.DungeonGate;
            Label("WOODLAND MAZE",new Vector3(dungeon.X,2.2f,dungeon.Y-1.7f));
        }
        else if(world.MapId=="homestead")
        {
            var h=world.Graph.Homestead;
            Label("RESIDENCE",new Vector3(h.House.X,h.House.Height+.55f,h.House.Z));
            Label("FARM PLOTS",new Vector3(h.Field.X,.45f,h.Field.Z));
            Label("VILLAGE",new Vector3(h.VillageGate[0],1.6f,h.VillageGate[1]));
        }
        else if(WoodedMaze.TryNode(world.MapId,out int mazeNode))
        {
            foreach(int neighbor in world.Graph.Maze.Neighbors(mazeNode))
            {
                var (dx,dz)=world.Graph.Maze.Direction(mazeNode,neighbor);
                var gate=WoodedMaze.Gate(dx,dz);
                Label(dx<0?"WEST":dx>0?"EAST":dz<0?"NORTH":"SOUTH",new Vector3(gate.X,.7f,gate.Y));
            }
            if(mazeNode==0)Label("VILLAGE",new Vector3(-5.15f,.7f,0));
            if(mazeNode==world.Graph.Maze.RareNode&&!world.RareCollected)Label("RARE SEED",new Vector3(0,1.8f,0));
            if(mazeNode==world.Graph.Maze.CommonNode&&!world.CommonCollected)Label("COMMON SEEDS",new Vector3(0,1.3f,0));
            if(mazeNode==world.Graph.Maze.ShrineNode)Label("SHRINE",new Vector3(0,2.2f,0));
        }
        else
        {
            foreach(var f in world.Graph.Room(world.MapId).Furniture)
                Label(f.Label,new Vector3(f.X,f.Height+.32f,f.Z));
            Label("EXIT",new Vector3(0,.3f,3.7f));
        }
        if(mazeMap)DrawMazeMap(world,width);
        if(inventoryMenu.Open)DrawInventory(world,inventoryMenu,width,height,characterPreview);
        Fill(new Rectangle(12,height-66,Math.Min(width-24,1000),54),new Color(44,58,51,235));
        Text("WASD MOVE   E TRAVEL   HOLD F HARVEST / F PICK UP   I PACK   C CHEST",24,height-55,ink,1);
        Text("J SHRINE   F1-F4 DOG GEAR   TAB MAP   M OVERVIEW   ESC EXIT   HARVESTS "+world.HarvestCount,24,height-39,ink,1);
        Text(status,24,height-23,new Color(215,190,133),1);
        batch.End();

        void Label(string label,Vector3 location)
        {
            var p=batch.GraphicsDevice.Viewport.Project(location,projection,view,Matrix.Identity);
            int x=(int)p.X-label.Length*6-5,y=(int)p.Y-8;
            if(p.Z<0||p.Z>1||x<8||y<114||x+label.Length*12+10>width||y>height-66)return;
            Fill(new Rectangle(x,y,label.Length*12+10,21),new Color(49,64,55,225));
            Text(label,x+5,y+4,ink,2);
        }
    }
    void DrawInventory(WorldState world,InventoryMenu menu,int width,int height,Texture2D? characterPreview)
    {
        var layout=new InventoryLayout(width,height);
        int left=layout.Panel.X,top=layout.Panel.Y;
        var ink=new Color(241,231,203);var muted=new Color(183,204,171);
        Fill(layout.Panel,new Color(27,45,39,250));
        Text(menu.ChestId==null?"PACK AND EQUIPMENT":"CHEST / "+world.Graph.Title(world.MapId),left+18,top+14,ink,2);
        Text("RIGHT CLICK BAG GEAR TO EQUIP / SLOT TO UNEQUIP",left+18,top+41,muted,1);
        Text("TAB PANE   UP DOWN SELECT   ENTER TRANSFER / EQUIP   I C CLOSE",left+18,top+55,muted,1);
        Text($"BAG / {world.Inventory.Bag.Stacks.Count} OF {InventoryState.BagCapacity}",layout.BagX,top+81,ink,1);
        for(int i=0;i<InventoryState.BagCapacity;i++)
        {
            int y=layout.BagRow(i).Y+3;
            if(menu.Pane==InventoryPane.Bag&&menu.Selection==i)Fill(layout.BagRow(i),new Color(78,105,81));
            if(i<world.Inventory.Bag.Stacks.Count)
            {
                var item=world.Inventory.Bag.Stacks[i];
                DrawIcon(item.Id,new Rectangle(layout.BagX,y-2,24,24));
                Text(ItemCatalog.Get(item.Id).Name+" X"+item.Quantity,layout.BagX+31,y+5,ink,1);
            }
            else Text("EMPTY",layout.BagX+31,y+5,new Color(105,133,111),1);
        }
        if(menu.ChestId!=null)
        {
            var contents=world.Inventory.Chest(menu.ChestId).Stacks;
            Text($"CHEST / {contents.Count} OF {InventoryState.ChestCapacity}",layout.ChestX,top+81,ink,1);
            for(int i=0;i<InventoryState.ChestCapacity;i++)
            {
                int y=layout.ChestRow(i).Y+3;
                if(menu.Pane==InventoryPane.Chest&&menu.Selection==i)Fill(layout.ChestRow(i),new Color(78,105,81));
                if(i<contents.Count)
                {
                    DrawIcon(contents[i].Id,new Rectangle(layout.ChestX,y-1,18,18));
                    Text(ItemCatalog.Get(contents[i].Id).Name+" X"+contents[i].Quantity,layout.ChestX+24,y+3,ink,1);
                }
                else Text("EMPTY",layout.ChestX+24,y+3,new Color(105,133,111),1);
            }
        }
        else
        {
            Text("ITEMS",layout.ChestX,top+81,ink,1);
            Text("SEEDS / FARMING",layout.ChestX,top+110,muted,1);
            Text("WOOD / STONE / ORE",layout.ChestX,top+130,muted,1);
            Text("FRUIT / FOOD",layout.ChestX,top+150,muted,1);
            Text("GEAR / RIGHT CLICK",layout.ChestX,top+170,muted,1);
        }
        Text("CHARACTER",layout.GearX,top+81,ink,1);
        Fill(layout.Preview,new Color(42,63,52,255));
        if(characterPreview!=null)batch.Draw(characterPreview,layout.Preview,Color.White);
        Text("EQUIPPED SLOTS",layout.GearX,layout.GearTop-27,ink,1);
        for(int i=0;i<4;i++)
        {
            int y=layout.GearRow(i).Y+3;
            if(menu.Pane==InventoryPane.Equipment&&menu.Selection==i)Fill(layout.GearRow(i),new Color(78,105,81));
            var slot=(Game.Assets.GearSlot)i;
            var id=world.Inventory.EquippedItem(slot);
            if(id!=null)DrawIcon(id,new Rectangle(layout.GearX,y-1,24,24));
            Text(slot.ToString().ToUpperInvariant()+" / "+(id==null?"EMPTY":ItemCatalog.Get(id).Name),layout.GearX+29,y+6,ink,1);
        }
    }
    void DrawIcon(string id,Rectangle rectangle)=>batch.Draw(icons[id],rectangle,Color.White);
    public void RebuildItemIcons()=>icons.Rebuild();
    void DrawMazeMap(WorldState world,int width)
    {
        var maze=world.Graph.Maze;
        int left=width-322,top=12,step=27,size=20;
        Fill(new Rectangle(left-9,top,310,320),new Color(30,49,42,245));
        Text("WOODLAND MAP",left,top+8,new Color(241,231,203),2);
        Text("VISITED / SEEN EXITS",left,top+30,new Color(188,205,177),1);
        var seen=new HashSet<int>(world.Discovered);
        foreach(int node in world.Discovered)foreach(int neighbor in maze.Neighbors(node))seen.Add(neighbor);
        for(int node=0;node<WoodedMaze.Count;node++)
        {
            if(!seen.Contains(node))continue;
            var (x,y)=maze.Coordinates(node);int px=left+x*step,py=top+55+y*step;
            bool visited=world.Discovered.Contains(node);
            Fill(new Rectangle(px,py,size,size),visited?new Color(116,157,111):new Color(69,91,79));
            if(WoodedMaze.TryNode(world.MapId,out int current)&&current==node)
                Fill(new Rectangle(px+5,py+5,10,10),new Color(245,218,132));
            else if(visited)
            {
                string icon=node==maze.RareNode?"R":node==maze.CommonNode?"C":node==maze.ShrineNode?"S":node==0?"V":"";
                if(icon.Length>0)Text(icon,px+7,py+6,new Color(248,232,178),1);
            }
            if(!visited)continue;
            foreach(int neighbor in maze.Neighbors(node))
            {
                var (dx,dz)=maze.Direction(node,neighbor);
                if(dx>0)Fill(new Rectangle(px+size,py+8,step-size,4),new Color(181,194,142));
                if(dz>0)Fill(new Rectangle(px+8,py+size,4,step-size),new Color(181,194,142));
                if(dx<0)Fill(new Rectangle(px-(step-size),py+8,step-size,4),new Color(181,194,142));
                if(dz<0)Fill(new Rectangle(px+8,py-(step-size),4,step-size),new Color(181,194,142));
            }
        }
        Text($"EXPLORED {world.Discovered.Count} / {WoodedMaze.Count}",left,top+300,new Color(222,207,164),1);
    }
    void Fill(Rectangle r,Color color) => batch.Draw(pixel,r,color);
    void Text(string text,int x,int y,Color color,int scale)
    {
        foreach (char c in text.ToUpperInvariant())
        {
            if (Glyphs.TryGetValue(c,out string? pattern))
            {
                var rows = pattern.Split('/');
                for (int row = 0; row < rows.Length; row++)
                for (int col = 0; col < rows[row].Length; col++)
                    if (rows[row][col] == '1') Fill(new Rectangle(x+col*scale,y+row*scale,scale,scale),color);
            }
            x += 6*scale;
        }
    }
    public void Dispose() { icons.Dispose(); batch.Dispose(); pixel.Dispose(); }
}
