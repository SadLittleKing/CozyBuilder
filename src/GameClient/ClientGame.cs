using Game.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace GameClient;

public sealed class ClientGame : Microsoft.Xna.Framework.Game
{
    readonly GraphicsDeviceManager graphics;
    readonly CharacterAssembly character;
    readonly DogAssembly dog;
    readonly DogCompanion companion;
    readonly string villagePath,homesteadPath,interiorsPath;
    readonly string? screenshotPath;
    readonly int screenshotAfter;
    readonly bool previewHarvest;
    readonly string? savePath;
    readonly string? dogConfigPath;
    readonly WorldState world;
    readonly InventoryMenu inventoryMenu=new();
    readonly ItemModels itemModels;
    BasicEffect effect = null!;
    WorldHud hud = null!;
    RenderTarget2D characterPreview = null!;
    VertexPositionColor[] terrain = null!,plots = [],resources=[];
    float heading;
    bool overview;
    bool mazeMap;
    float saveTimer;
    string status;
    KeyboardState previousKeys;
    MouseState previousMouse;
    int frames;

    public ClientGame(CharacterAssembly character,DogAssembly dog,WorldState world,ItemModels itemModels,string villagePath,string homesteadPath,string interiorsPath,string? screenshotPath,bool overview,string? savePath,bool mazeMap,string? dogConfigPath,int screenshotAfter,bool previewInventory,bool previewChest,bool previewHarvest)
    {
        this.character=character;this.dog=dog;this.world=world;companion=new DogCompanion(world);
        this.itemModels=itemModels;
        character.SelectGear(world.Inventory.Visuals);
        this.villagePath=villagePath;this.homesteadPath=homesteadPath;this.interiorsPath=interiorsPath;
        this.screenshotPath=screenshotPath;this.screenshotAfter=screenshotAfter;this.overview=overview;this.savePath=savePath;this.mazeMap=mazeMap;this.dogConfigPath=dogConfigPath;this.previewHarvest=previewHarvest;
        if(previewChest)inventoryMenu.ShowChest(world.NearbyChest()??throw new InvalidOperationException("Preview chest is out of reach."));
        else if(previewInventory)inventoryMenu.ShowBag();
        status=world.MapId switch
        {
            "village" => "WALK TO A DOOR OR WEST GATE / PRESS E TO ENTER",
            "homestead" => "E AT THE HOUSE OR VILLAGE GATE / F AT FARM PLOTS",
            _ when WoodedMaze.TryNode(world.MapId,out _) => "HOLD F TO HARVEST / F PICK UP / TAB MAP",
            _ => "WALK TO THE SOUTH DOOR / PRESS E TO EXIT"
        };
        graphics=new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth=1100,PreferredBackBufferHeight=850,
            GraphicsProfile=GraphicsProfile.HiDef
        };
        IsMouseVisible=true;Window.AllowUserResizing=true;
        RefreshTitle();
    }
    void RefreshTitle() => Window.Title=$"GameClient | {world.Graph.Title(world.MapId)} | Q dog scent: {world.ScentMode} | F1-F4 dog gear";

    protected override void LoadContent()
    {
        effect=new BasicEffect(GraphicsDevice){VertexColorEnabled=true,LightingEnabled=false};
        hud=new WorldHud(GraphicsDevice,itemModels);
        characterPreview=new RenderTarget2D(GraphicsDevice,220,280,false,SurfaceFormat.Color,DepthFormat.Depth24);
        previousMouse=Mouse.GetState();
        RebuildScene();
    }
    void RebuildScene()
    {
        terrain=WorldTerrain.Build(world);
        plots=WorldTerrain.BuildPlots(world);
        resources=MazeResources.Build(world);
        RefreshTitle();
        Console.WriteLine($"Loaded map {world.MapId}: {terrain.Length/3:N0} static triangles, {plots.Length/3:N0} plot triangles.");
    }

    protected override void Update(GameTime time)
    {
        var keys=Keyboard.GetState();
        var mouse=Mouse.GetState();
        bool Press(Keys key)=>keys.IsKeyDown(key)&&previousKeys.IsKeyUp(key);
        if(Press(Keys.Escape))
        {
            if(inventoryMenu.Open)inventoryMenu.Close();else Exit();
        }
        if(Press(Keys.I))
        {
            if(inventoryMenu.Open)inventoryMenu.Close();else inventoryMenu.ShowBag();
        }
        if(Press(Keys.C))
        {
            if(inventoryMenu.Open)inventoryMenu.Close();
            else if(world.NearbyChest() is string chest)inventoryMenu.ShowChest(chest);
            else status="MOVE NEAR A CHEST";
        }
        if(inventoryMenu.Open)
        {
            world.UpdateHarvest(false,0,out _);
            if(Press(Keys.Tab))inventoryMenu.NextPane();
            if(Press(Keys.Up))inventoryMenu.MoveSelection(world,-1);
            if(Press(Keys.Down))inventoryMenu.MoveSelection(world,1);
            if(Press(Keys.Enter))
            {
                if(inventoryMenu.Activate(world,keys.IsKeyDown(Keys.LeftShift)||keys.IsKeyDown(Keys.RightShift),out status))
                {character.SelectGear(world.Inventory.Visuals);SaveProgress();}
            }
            if(Press(Keys.E))
            {
                if(inventoryMenu.EquipSelected(world,out status))
                {character.SelectGear(world.Inventory.Visuals);SaveProgress();}
            }
            bool rightClick=mouse.RightButton==ButtonState.Pressed&&previousMouse.RightButton==ButtonState.Released;
            bool leftClick=mouse.LeftButton==ButtonState.Pressed&&previousMouse.LeftButton==ButtonState.Released;
            if(rightClick||leftClick)
            {
                bool changed=inventoryMenu.Click(world,new Point(mouse.X,mouse.Y),GraphicsDevice.Viewport.Width,GraphicsDevice.Viewport.Height,rightClick,out string message);
                if(message.Length>0)status=message;
                if(changed){character.SelectGear(world.Inventory.Visuals);SaveProgress();}
            }
            previousKeys=keys;previousMouse=mouse;base.Update(time);return;
        }
        if(Press(Keys.M))overview=!overview;
        if(Press(Keys.Tab))mazeMap=!mazeMap;
        if(Press(Keys.Q))
        {
            world.CycleDogScent();status=DogScent.Advise(world).Message;RefreshTitle();SaveProgress();
        }
        foreach(var (key,slot) in new[]{(Keys.F1,DogSlot.Collar),(Keys.F2,DogSlot.Helmet),(Keys.F3,DogSlot.Armor),(Keys.F4,DogSlot.Backpack)})
            if(Press(key))
            {
                dog.Toggle(slot);status=$"DOG {slot.ToString().ToUpperInvariant()} {(slot switch {DogSlot.Collar=>dog.Configuration.Collar,DogSlot.Helmet=>dog.Configuration.Helmet,DogSlot.Armor=>dog.Configuration.Armor,_=>dog.Configuration.Backpack}?"ON":"OFF")}";
                if(dogConfigPath!=null)
                    try{dog.Configuration.Save(dogConfigPath);}catch(Exception error){Console.Error.WriteLine($"Dog config save failed: {error.Message}");}
            }
        if(Press(Keys.E))
        {
            if(world.TryInteract(out string message))
            { status=message;companion.Snap(world);RebuildScene();SaveProgress(); }
            else status=message;
        }
        bool consumedF=false;
        if(Press(Keys.F))
        {
            if(WoodedMaze.TryNode(world.MapId,out _))
            {
                if(world.NearbyDrop()!=null)
                {consumedF=true;if(world.TryPickupDrop(out status)){resources=MazeResources.Build(world);SaveProgress();}}
                else if(world.NearbyResource()==null && world.TryCollect(out status))
                {consumedF=true;RebuildScene();SaveProgress();}
            }
            else if(world.TryFarm(out status))SaveProgress();
            plots=WorldTerrain.BuildPlots(world);
        }
        string harvestMessage="";
        if(!previewHarvest&&world.UpdateHarvest(WoodedMaze.TryNode(world.MapId,out _)&&keys.IsKeyDown(Keys.F)&&!consumedF&&world.NearbyDrop()==null,
            (float)time.ElapsedGameTime.TotalSeconds,out harvestMessage))
        {status=harvestMessage;resources=MazeResources.Build(world);SaveProgress();}
        else if(harvestMessage.Length>0)status=harvestMessage;
        if(Press(Keys.J))
        {
            if(world.TryJumpShrine(out status)){companion.Snap(world);RebuildScene();SaveProgress();}
        }
        if(Press(Keys.R))
        {
            try
            {
                var replacement=WorldGraph.Load(villagePath,homesteadPath,interiorsPath,world.Graph.Maze.Seed);
                replacement.Verify();
                itemModels.Reload();
                hud.RebuildItemIcons();
                world.ReplaceGraph(replacement);
                companion.Snap(world);
                RebuildScene();
                status="MAPS AND ITEMS RELOADED / FARM STATE KEPT";
            }
            catch(Exception error)
            {
                status="RELOAD FAILED / KEPT PREVIOUS MAPS";
                Console.Error.WriteLine($"{status}: {error.Message}");
            }
        }
        var motion=Vector2.Zero;
        if(keys.IsKeyDown(Keys.W)||keys.IsKeyDown(Keys.Up))motion.Y-=1;
        if(keys.IsKeyDown(Keys.S)||keys.IsKeyDown(Keys.Down))motion.Y+=1;
        if(keys.IsKeyDown(Keys.A)||keys.IsKeyDown(Keys.Left))motion.X-=1;
        if(keys.IsKeyDown(Keys.D)||keys.IsKeyDown(Keys.Right))motion.X+=1;
        if(motion!=Vector2.Zero)
        {
            motion.Normalize();
            heading=MathF.Atan2(motion.X,motion.Y);
            world.Move(motion*(float)time.ElapsedGameTime.TotalSeconds*(world.MapId.StartsWith("interior:")?2.4f:3.0f));
        }
        companion.Follow(world,(float)time.ElapsedGameTime.TotalSeconds,DogScent.Advise(world));
        saveTimer+=(float)time.ElapsedGameTime.TotalSeconds;
        if(saveTimer>=2f){SaveProgress();saveTimer=0;}
        previousKeys=keys;
        previousMouse=mouse;
        base.Update(time);
    }
    void SaveProgress()
    {
        if(savePath==null)return;
        try{world.Snapshot().Write(savePath);}
        catch(Exception error){status="SAVE FAILED / "+error.Message;Console.Error.WriteLine(status);}
    }

    protected override void Draw(GameTime time)
    {
        if(inventoryMenu.Open)DrawCharacterPreview();
        RenderTarget2D? target=null;
        if(screenshotPath!=null&&frames>=screenshotAfter)
        {
            target=new RenderTarget2D(GraphicsDevice,1100,850,false,SurfaceFormat.Color,DepthFormat.Depth24);
            GraphicsDevice.SetRenderTarget(target);
        }
        GraphicsDevice.Clear(world.MapId.StartsWith("interior:")?new Color(74,90,82):new Color(219,224,196));
        GraphicsDevice.DepthStencilState=DepthStencilState.Default;
        GraphicsDevice.BlendState=BlendState.Opaque;
        GraphicsDevice.RasterizerState=RasterizerState.CullNone;
        bool indoors=world.MapId.StartsWith("interior:");
        bool maze=WoodedMaze.TryNode(world.MapId,out _);
        float extent=world.Graph.Extent(world.MapId);
        var focus=indoors||maze||overview ? Vector3.Zero : new Vector3(world.Position.X,0,world.Position.Y);
        float distance=indoors ? 13f : maze ? 15f : overview ? extent*1.55f : 14f;
        var eye=focus+new Vector3(distance*.35f,distance*.80f,distance*.95f);
        effect.View=Matrix.CreateLookAt(eye,focus+new Vector3(0,.45f,0),Vector3.Up);
        float frameHeight=indoors ? 12.3f : maze ? 16f : overview ? extent*1.22f : 15.2f;
        effect.Projection=Matrix.CreateOrthographic(frameHeight*GraphicsDevice.Viewport.AspectRatio,frameHeight,.1f,250f);
        effect.World=Matrix.Identity;
        DrawTriangles(terrain);
        if(plots.Length>0)DrawTriangles(plots);
        if(resources.Length>0)DrawTriangles(resources);
        if(WoodedMaze.TryNode(world.MapId,out int dropNode))
            foreach(var drop in world.GroundDrops.Where(d=>d.Node==dropNode))
            {
                var model=itemModels[drop.ItemId];
                effect.World=ItemModels.DropWorld(model,drop.Position);
                foreach(var part in model.Parts)DrawTriangles(part.Vertices);
            }
        effect.World=Matrix.Identity;
        DrawShadow();
        effect.World=Matrix.CreateRotationY(heading)*Matrix.CreateTranslation(world.Position.X,0,world.Position.Y);
        foreach(var part in character.VisibleParts)DrawTriangles(part.Vertices);
        effect.World=Matrix.CreateRotationY(companion.Heading)*Matrix.CreateTranslation(companion.Position.X,0,companion.Position.Y);
        foreach(var part in dog.VisibleParts)DrawTriangles(part.Vertices);
        hud.Draw(world,effect.View,effect.Projection,overview,mazeMap,DogScent.Advise(world).Message,status,inventoryMenu,inventoryMenu.Open?characterPreview:null);
        if(target!=null)
        {
            GraphicsDevice.SetRenderTarget(null);
            using var output=File.Create(Path.GetFullPath(screenshotPath!));
            target.SaveAsPng(output,target.Width,target.Height);
            target.Dispose();
            Console.WriteLine($"GameClient rendered {world.MapId}: {Path.GetFullPath(screenshotPath!)}");
            Exit();
        }
        if(++frames==1)Console.WriteLine($"GameClient rendered first frame of {world.MapId}.");
        base.Draw(time);
    }
    void DrawCharacterPreview()
    {
        GraphicsDevice.SetRenderTarget(characterPreview);
        GraphicsDevice.Clear(Color.Transparent);
        GraphicsDevice.DepthStencilState=DepthStencilState.Default;
        GraphicsDevice.BlendState=BlendState.Opaque;
        GraphicsDevice.RasterizerState=RasterizerState.CullNone;
        var focus=new Vector3(0,.98f,0);
        effect.View=Matrix.CreateLookAt(focus+new Vector3(2.4f,1.75f,3.7f),focus,Vector3.Up);
        effect.Projection=Matrix.CreateOrthographic(2.6f*characterPreview.Width/characterPreview.Height,2.6f,.1f,20f);
        effect.World=Matrix.Identity;
        foreach(var part in character.VisibleParts)DrawTriangles(part.Vertices);
        GraphicsDevice.SetRenderTarget(null);
    }
    void DrawShadow()
    {
        var shadow=new VertexPositionColor[48*3];
        var shade=new Color(143,157,128);
        for(int i=0;i<48;i++)
        {
            float a=i*MathHelper.TwoPi/48,b=(i+1)*MathHelper.TwoPi/48;
            shadow[i*3]=V(world.Position.X,-.005f,world.Position.Y,shade);
            shadow[i*3+1]=V(world.Position.X+MathF.Cos(a)*.42f,-.005f,world.Position.Y+MathF.Sin(a)*.27f,shade);
            shadow[i*3+2]=V(world.Position.X+MathF.Cos(b)*.42f,-.005f,world.Position.Y+MathF.Sin(b)*.27f,shade);
        }
        DrawTriangles(shadow);
    }
    void DrawTriangles(VertexPositionColor[] vertices)
    {
        foreach(var pass in effect.CurrentTechnique.Passes)
        {pass.Apply();GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList,vertices,0,vertices.Length/3);}
    }
    static VertexPositionColor V(float x,float y,float z,Color c)=>new(new Vector3(x,y,z),c);
    protected override void Dispose(bool disposing)
    {if(disposing){SaveProgress();characterPreview?.Dispose();hud?.Dispose();effect?.Dispose();}base.Dispose(disposing);}
}
