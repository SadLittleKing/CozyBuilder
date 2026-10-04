using Game.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CharacterEditor;

public sealed class DogViewerGame : Microsoft.Xna.Framework.Game
{
    readonly GraphicsDeviceManager graphics;
    readonly string modelPath;
    readonly string? screenshot;
    readonly string configPath;
    GlbScene scene;
    DogAssembly dog;
    BasicEffect effect=null!;
    CharacterHud hud=null!;
    KeyboardState previousKeys;
    MouseState previousMouse;
    float yaw=.25f,elevation=.67f,zoom=1;
    bool rotating;
    string status="TRAIL DOG READY";
    public DogViewerGame(GlbScene scene,DogConfiguration config,string modelPath,string configPath,string? screenshot,bool backView)
    {
        this.scene=scene;dog=new DogAssembly(scene,config);this.modelPath=modelPath;this.configPath=configPath;
        this.screenshot=screenshot;if(backView)yaw=MathHelper.Pi+.25f;
        graphics=new GraphicsDeviceManager(this){PreferredBackBufferWidth=1100,PreferredBackBufferHeight=850,GraphicsProfile=GraphicsProfile.HiDef};
        IsMouseVisible=true;Window.AllowUserResizing=true;RefreshTitle();
    }
    void RefreshTitle()=>Window.Title=$"Trail Dog Viewer | F1 Collar {dog.Configuration.Collar} | F2 Helmet {dog.Configuration.Helmet} | F3 Armor {dog.Configuration.Armor} | F4 Backpack {dog.Configuration.Backpack}";
    protected override void LoadContent()
    {
        effect=new BasicEffect(GraphicsDevice){VertexColorEnabled=true,LightingEnabled=false};
        hud=new CharacterHud(GraphicsDevice);previousMouse=Mouse.GetState();
    }
    protected override void Update(GameTime time)
    {
        var keys=Keyboard.GetState();var mouse=Mouse.GetState();
        bool Press(Keys key)=>keys.IsKeyDown(key)&&previousKeys.IsKeyUp(key);
        if(keys.IsKeyDown(Keys.Escape))Exit();
        foreach(var (key,slot) in new[]{(Keys.F1,DogSlot.Collar),(Keys.F2,DogSlot.Helmet),(Keys.F3,DogSlot.Armor),(Keys.F4,DogSlot.Backpack)})
            if(Press(key)){dog.Toggle(slot);RefreshTitle();status="CHANGED DOG "+slot.ToString().ToUpperInvariant();}
        if(mouse.LeftButton==ButtonState.Pressed&&previousMouse.LeftButton==ButtonState.Released&&CharacterHud.Hit(mouse.Position) is { } row)
        {dog.Toggle((DogSlot)(int)row);RefreshTitle();status="CHANGED DOG "+((DogSlot)(int)row).ToString().ToUpperInvariant();}
        if(Press(Keys.F5))
        {
            try{dog.Configuration.Save(configPath);status="SAVED DOG CONFIG";}
            catch(Exception error){status="SAVE FAILED / SEE CONSOLE";Console.Error.WriteLine(error);}
        }
        if(Press(Keys.R))
        {
            try{var replacement=GlbScene.Load(modelPath);var assembled=new DogAssembly(replacement,dog.Configuration);
                scene=replacement;dog=assembled;status="DOG MODEL RELOADED";RefreshTitle();}
            catch(Exception error){status="RELOAD FAILED / KEPT MODEL";Console.Error.WriteLine(error);}
        }
        if(Press(Keys.Space))rotating=!rotating;
        if(Press(Keys.Home)){yaw=.25f;elevation=.67f;zoom=1;rotating=false;}
        float delta=(float)time.ElapsedGameTime.TotalSeconds;
        if(rotating)yaw+=delta*.45f;
        if(keys.IsKeyDown(Keys.Left))yaw-=delta;
        if(keys.IsKeyDown(Keys.Right))yaw+=delta;
        if(keys.IsKeyDown(Keys.Up))elevation+=delta*.7f;
        if(keys.IsKeyDown(Keys.Down))elevation-=delta*.7f;
        if(keys.IsKeyDown(Keys.OemPlus)||keys.IsKeyDown(Keys.Add))zoom*=MathF.Exp(-delta);
        if(keys.IsKeyDown(Keys.OemMinus)||keys.IsKeyDown(Keys.Subtract))zoom*=MathF.Exp(delta);
        if(mouse.LeftButton==ButtonState.Pressed&&previousMouse.LeftButton==ButtonState.Pressed&&
           !CharacterHud.Contains(mouse.Position)&&!CharacterHud.Contains(previousMouse.Position))
        {yaw-=(mouse.X-previousMouse.X)*.008f;elevation+=(mouse.Y-previousMouse.Y)*.006f;}
        zoom*=MathF.Pow(.9f,(mouse.ScrollWheelValue-previousMouse.ScrollWheelValue)/120f);
        elevation=MathHelper.Clamp(elevation,.12f,1.5f);zoom=MathHelper.Clamp(zoom,.4f,3);
        previousKeys=keys;previousMouse=mouse;base.Update(time);
    }
    protected override void Draw(GameTime time)
    {
        RenderTarget2D? target=null;
        if(screenshot!=null){target=new RenderTarget2D(GraphicsDevice,1100,850,false,SurfaceFormat.Color,DepthFormat.Depth24);GraphicsDevice.SetRenderTarget(target);}
        GraphicsDevice.Clear(new Color(226,220,199));
        GraphicsDevice.DepthStencilState=DepthStencilState.Default;
        GraphicsDevice.RasterizerState=RasterizerState.CullNone;
        var center=new Vector3(0,.48f,0);
        var direction=new Vector3(MathF.Sin(yaw)*MathF.Cos(elevation),MathF.Sin(elevation),MathF.Cos(yaw)*MathF.Cos(elevation));
        effect.View=Matrix.CreateLookAt(center+direction*4.0f,center,Vector3.Up);
        effect.Projection=Matrix.CreateOrthographic(2.3f*zoom*GraphicsDevice.Viewport.AspectRatio,2.3f*zoom,.01f,20);
        effect.Projection*=Matrix.CreateTranslation(.22f,0,0);
        effect.World=Matrix.Identity;
        var floor=new Color(187,200,164);
        DrawTriangles([V(-10,-.018f,-10,floor),V(10,-.018f,-10,floor),V(10,-.018f,10,floor),
            V(-10,-.018f,-10,floor),V(10,-.018f,10,floor),V(-10,-.018f,10,floor)]);
        foreach(var part in dog.VisibleParts)DrawTriangles(part.Vertices);
        hud.DrawDog(dog,GraphicsDevice.Viewport.Width,GraphicsDevice.Viewport.Height,status);
        if(target!=null)
        {
            GraphicsDevice.SetRenderTarget(null);
            using var stream=File.Create(Path.GetFullPath(screenshot!));target.SaveAsPng(stream,target.Width,target.Height);
            target.Dispose();Console.WriteLine($"Rendered dog preview: {Path.GetFullPath(screenshot!)}");Exit();
        }
        base.Draw(time);
    }
    void DrawTriangles(VertexPositionColor[] vertices)
    {
        foreach(var pass in effect.CurrentTechnique.Passes)
        {pass.Apply();GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList,vertices,0,vertices.Length/3);}
    }
    static VertexPositionColor V(float x,float y,float z,Color color)=>new(new Vector3(x,y,z),color);
    protected override void Dispose(bool disposing)
    {if(disposing){effect?.Dispose();hud?.Dispose();}base.Dispose(disposing);}
}

public static class DogViewerProgram
{
    public static void Run(string[] args)
    {
        string? model=null,screenshot=null;
        string configPath="dog.json";
        bool inspect=false,verify=false,backView=false,collarOff=false,helmet=false,armor=false,backpack=false;
        for(int i=0;i<args.Length;i++)switch(args[i])
        {
            case "--dog":break;
            case "--dog-model":model=args[++i];break;
            case "--dog-config":configPath=args[++i];break;
            case "--screenshot":screenshot=args[++i];break;
            case "--inspect":inspect=true;break;
            case "--verify-dog":verify=true;inspect=true;break;
            case "--back-view":backView=true;break;
            case "--no-collar":collarOff=true;break;
            case "--helmet":helmet=true;break;
            case "--armor":armor=true;break;
            case "--backpack":backpack=true;break;
            default:throw new ArgumentException("Dog viewer: --dog [--dog-model path] [--dog-config path] [--helmet] [--armor] [--backpack] [--no-collar] [--back-view] [--verify-dog|--inspect] [--screenshot path]");
        }
        model??=new[]{Directory.GetCurrentDirectory(),AppContext.BaseDirectory}
            .Select(folder=>Path.Combine(folder,"assets","dog","dog_modular.glb")).FirstOrDefault(File.Exists);
        if(model==null)throw new FileNotFoundException("No modular dog GLB found.");
        var config=File.Exists(configPath)?DogConfiguration.Load(configPath):new DogConfiguration();
        config=config with {Collar=collarOff?false:config.Collar,Helmet=helmet||config.Helmet,
            Armor=armor||config.Armor,Backpack=backpack||config.Backpack};
        var scene=GlbScene.Load(model);var dog=new DogAssembly(scene,config);
        Console.WriteLine($"Dog viewer loaded {Path.GetFullPath(model)}: {scene.Parts.Count} named parts; {config}");
        if(verify)dog.VerifyModules();
        if(!inspect){using var game=new DogViewerGame(scene,config,model,configPath,screenshot,backView);game.Run();}
    }
}
