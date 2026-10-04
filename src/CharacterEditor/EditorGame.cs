using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Game.Assets;

namespace CharacterEditor;

public sealed class EditorGame : Microsoft.Xna.Framework.Game
{
    readonly GraphicsDeviceManager graphics;
    GlbScene scene;
    CharacterAssembly character;
    CharacterHud hud = null!;
    string status = "READY / CHOOSE A CHARACTER";
    readonly string modelPath;
    readonly string? screenshot;
    BasicEffect effect = null!;
    readonly HashSet<Keys> heldKeys = [];
    readonly HashSet<Keys> pressedKeys = [];
    MouseState previousMouse;
    float yaw = .3f, elevation = .7f, zoom = 1;
    bool rotate;
    int frames;
    readonly RasterizerState solid = new() { CullMode = CullMode.None };
    readonly RasterizerState wire = new() { CullMode = CullMode.None, FillMode = FillMode.WireFrame };
    bool wireframe;

    public EditorGame(GlbScene scene, string modelPath, string? screenshot, CharacterConfiguration configuration, bool backView = false,GearVisualConfiguration? gear = null)
    {
        this.scene = scene; this.modelPath = modelPath; this.screenshot = screenshot;
        character = new CharacterAssembly(scene,configuration);
        character.SelectGear(gear??new GearVisualConfiguration());
        if (backView) yaw = MathHelper.Pi+.3f;
        graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = 1100, PreferredBackBufferHeight = 850, GraphicsProfile = GraphicsProfile.HiDef, PreferMultiSampling = true };
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        RefreshTitle();
        // Buffered events retain taps shorter than a frame and ignore key repeat.
        Window.KeyDown += (_, e) => { if (heldKeys.Add(e.Key)) pressedKeys.Add(e.Key); };
        Window.KeyUp += (_, e) => heldKeys.Remove(e.Key);
        Deactivated += (_, _) => { heldKeys.Clear(); pressedKeys.Clear(); };
    }
    protected override void LoadContent()
    {
        effect = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true, LightingEnabled = false };
        hud = new CharacterHud(GraphicsDevice);
        previousMouse = Mouse.GetState();
    }
    protected override void Update(GameTime time)
    {
        var keys = Keyboard.GetState(); var mouse = Mouse.GetState();
        bool Press(Keys key) => pressedKeys.Contains(key);
        if (Press(Keys.Escape) || keys.IsKeyDown(Keys.Escape)) Exit();
        if (Press(Keys.Space)) rotate = !rotate;
        if (Press(Keys.W)) wireframe = !wireframe;
        if (character.IsModular && IsActive)
        {
            if (Press(Keys.D1) || Press(Keys.NumPad1)) Swap(CharacterSlot.Hair);
            if (Press(Keys.D2) || Press(Keys.NumPad2)) Swap(CharacterSlot.Outfit);
            if (Press(Keys.D3) || Press(Keys.NumPad3)) Swap(CharacterSlot.Hand);
            if (Press(Keys.D4) || Press(Keys.NumPad4)) Swap(CharacterSlot.Back);
            if (Press(Keys.D5) || Press(Keys.NumPad5)) SwapGear(GearSlot.Head);
            if (Press(Keys.D6) || Press(Keys.NumPad6)) SwapGear(GearSlot.Body);
            if (Press(Keys.D7) || Press(Keys.NumPad7)) SwapGear(GearSlot.Gloves);
            if (Press(Keys.D8) || Press(Keys.NumPad8)) SwapGear(GearSlot.Feet);
            if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released && CharacterHud.Hit(mouse.Position) is { } slot) Swap(slot);
            if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released && CharacterHud.HitGear(mouse.Position) is { } gearSlot) SwapGear(gearSlot);
        }
        if (Press(Keys.F5))
        {
            try { character.Configuration.Save("character.json"); status = "SAVED CHARACTER.JSON"; }
            catch (Exception e) { status = "SAVE FAILED / SEE CONSOLE"; Console.Error.WriteLine(e.Message); }
        }
        if (Press(Keys.Home)) { yaw = .3f; elevation = .7f; zoom = 1; rotate = false; }
        if (Press(Keys.R))
        {
            try
            {
                var loaded = GlbScene.Load(modelPath);
                var assembled = new CharacterAssembly(loaded,character.Configuration);
                assembled.SelectGear(character.Gear);
                scene = loaded; character = assembled;
                RefreshTitle();
                status = "RELOADED / SELECTION PRESERVED";
                Console.WriteLine(status);
            }
            catch (Exception e) { status = "RELOAD FAILED / KEPT PREVIOUS MODEL"; Console.Error.WriteLine($"Reload failed; keeping previous model: {e.Message}"); }
        }
        float delta = (float)time.ElapsedGameTime.TotalSeconds;
        if (IsActive)
        {
            if (rotate) yaw += delta*.45f;
            if (keys.IsKeyDown(Keys.Left)) yaw -= delta;
            if (keys.IsKeyDown(Keys.Right)) yaw += delta;
            if (keys.IsKeyDown(Keys.Up)) elevation += delta*.7f;
            if (keys.IsKeyDown(Keys.Down)) elevation -= delta*.7f;
            if (keys.IsKeyDown(Keys.OemPlus) || keys.IsKeyDown(Keys.Add)) zoom *= MathF.Exp(-delta);
            if (keys.IsKeyDown(Keys.OemMinus) || keys.IsKeyDown(Keys.Subtract)) zoom *= MathF.Exp(delta);
            if (mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Pressed && !CharacterHud.Contains(mouse.Position) && !CharacterHud.Contains(previousMouse.Position))
            { yaw -= (mouse.X-previousMouse.X)*.008f; elevation += (mouse.Y-previousMouse.Y)*.006f; }
            zoom *= MathF.Pow(.9f,(mouse.ScrollWheelValue-previousMouse.ScrollWheelValue)/120f);
        }
        elevation = MathHelper.Clamp(elevation,.12f,1.5f); zoom = MathHelper.Clamp(zoom,.4f,3);
        pressedKeys.Clear(); previousMouse = mouse;
        base.Update(time);
    }
    protected override void Draw(GameTime time)
    {
        RenderTarget2D? target = null;
        if (screenshot != null) { target = new RenderTarget2D(GraphicsDevice,1100,850,false,SurfaceFormat.Color,DepthFormat.Depth24); GraphicsDevice.SetRenderTarget(target); }
        GraphicsDevice.Clear(new Color(226,220,199));
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.RasterizerState = solid;
        float height = Math.Max(scene.Max.Y-scene.Min.Y,.1f);
        var center = (scene.Min+scene.Max)*.5f;
        if (character.IsModular) center = new Vector3(0,height*.5f,0);
        float span = Math.Max(height,Math.Max(scene.Max.X-scene.Min.X,scene.Max.Z-scene.Min.Z));
        var direction = new Vector3(MathF.Sin(yaw)*MathF.Cos(elevation),MathF.Sin(elevation),MathF.Cos(yaw)*MathF.Cos(elevation));
        effect.View = Matrix.CreateLookAt(center+direction*span*5,center,Vector3.Up);
        effect.Projection = Matrix.CreateOrthographic(span*1.7f*zoom*GraphicsDevice.Viewport.AspectRatio,span*1.7f*zoom,.01f,span*20);
        // Keep the character beside the workshop panel, with stable framing on swaps.
        effect.Projection *= Matrix.CreateTranslation(.22f,0,0);
        effect.World = Matrix.Identity;
        var ground = new Color(195,200,166);
        float extent = span*20, y = scene.Min.Y-.012f;
        var floor = new[] { V(-extent,y,-extent,ground), V(extent,y,-extent,ground), V(extent,y,extent,ground), V(-extent,y,-extent,ground), V(extent,y,extent,ground), V(-extent,y,extent,ground) };
        DrawTriangles(floor);
        // A soft, layered contact ellipse anchors the character without textures.
        for (int layer = 0; layer < 8; layer++)
        {
            float radius = span*(.26f-layer*.016f);
            var shade = Color.Lerp(ground,new Color(137,148,117),.15f+layer*.055f);
            var shadow = new VertexPositionColor[48*3];
            for (int i = 0; i < 48; i++)
            {
                float a = i*MathHelper.TwoPi/48,b = (i+1)*MathHelper.TwoPi/48;
                float sy = y+.001f+layer*.0005f;
                shadow[i*3] = V(center.X,sy,center.Z,shade);
                shadow[i*3+1] = V(center.X+MathF.Cos(a)*radius,sy,center.Z+MathF.Sin(a)*radius*.65f,shade);
                shadow[i*3+2] = V(center.X+MathF.Cos(b)*radius,sy,center.Z+MathF.Sin(b)*radius*.65f,shade);
            }
            DrawTriangles(shadow);
        }
        GraphicsDevice.RasterizerState = wireframe ? wire : solid;
        foreach (var part in character.VisibleParts) DrawTriangles(part.Vertices);
        hud.Draw(character,GraphicsDevice.Viewport.Width,GraphicsDevice.Viewport.Height,status);
        if (target != null)
        {
            GraphicsDevice.SetRenderTarget(null);
            using var stream = File.Create(Path.GetFullPath(screenshot!));
            target.SaveAsPng(stream,target.Width,target.Height);
            target.Dispose();
            Console.WriteLine($"Rendered screenshot: {Path.GetFullPath(screenshot!)}");
            Exit();
        }
        if (++frames == 1) Console.WriteLine("First frame rendered successfully.");
        base.Draw(time);
    }
    void Swap(CharacterSlot slot)
    {
        character.Cycle(slot);
        RefreshTitle();
        status = $"CHANGED {slot.ToString().ToUpperInvariant()}";
        Console.WriteLine($"{status}: {character.Configuration}");
    }
    void SwapGear(GearSlot slot)
    {
        character.CycleGear(slot);RefreshTitle();status="CHANGED "+slot.ToString().ToUpperInvariant()+" GEAR";
    }
    void RefreshTitle()
    {
        var c = character.Configuration;
        Window.Title = $"CharacterEditor | Hair {c.Hair} | Outfit {c.Outfit} | Head {character.Gear.Head} | Body {character.Gear.Body} | Gloves {character.Gear.Gloves} | Feet {character.Gear.Feet}";
    }
    void DrawTriangles(VertexPositionColor[] vertices)
    {
        foreach (var pass in effect.CurrentTechnique.Passes) { pass.Apply(); GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList,vertices,0,vertices.Length/3); }
    }
    static VertexPositionColor V(float x,float y,float z,Color c) => new(new Vector3(x,y,z),c);
    protected override void Dispose(bool disposing)
    {
        if (disposing) { hud?.Dispose(); effect?.Dispose(); solid.Dispose(); wire.Dispose(); }
        base.Dispose(disposing);
    }
}
