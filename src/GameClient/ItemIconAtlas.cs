using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameClient;

// Draws icons from item GLBs once, then reuses the textures until a device reset.
public sealed class ItemIconAtlas : IDisposable
{
    readonly GraphicsDevice device;
    readonly ItemModels models;
    readonly Dictionary<string,RenderTarget2D> textures=new(StringComparer.Ordinal);
    readonly BasicEffect effect;
    public ItemIconAtlas(GraphicsDevice device,ItemModels models)
    {
        this.device=device;this.models=models;
        effect=new BasicEffect(device){VertexColorEnabled=true,LightingEnabled=false};
        device.DeviceReset+=OnDeviceReset;
        Rebuild();
    }
    public Texture2D this[string id]=>textures[id];
    void OnDeviceReset(object? sender,EventArgs e)=>Rebuild();
    public void Rebuild()
    {
        var built=new Dictionary<string,RenderTarget2D>(StringComparer.Ordinal);
        var previous=device.GetRenderTargets();
        try
        {
            foreach(var item in ItemCatalog.All)
            {
                var model=models[item.Id];
                var target=new RenderTarget2D(device,128,128,false,SurfaceFormat.Color,DepthFormat.Depth24);
                device.SetRenderTarget(target);
                device.Clear(Color.Transparent);
                device.DepthStencilState=DepthStencilState.Default;
                device.BlendState=BlendState.Opaque;
                device.RasterizerState=RasterizerState.CullNone;
                Vector3 center=(model.Min+model.Max)*.5f,extent=model.Max-model.Min;
                float size=Math.Max(extent.X,Math.Max(extent.Y,extent.Z));
                float frame=size*(item.Id is "leather_gloves" or "iron_gauntlets" ? 1.55f : 1.8f);
                effect.View=Matrix.CreateLookAt(center+new Vector3(1.6f,1.4f,2.8f),center,Vector3.Up);
                effect.Projection=Matrix.CreateOrthographic(frame,frame,.1f,20f);
                effect.World=Matrix.Identity;
                foreach(var part in model.Parts)
                    foreach(var pass in effect.CurrentTechnique.Passes)
                    {pass.Apply();device.DrawUserPrimitives(PrimitiveType.TriangleList,part.Vertices,0,part.Vertices.Length/3);}
                built.Add(item.Id,target);
            }
        }
        catch
        {
            foreach(var texture in built.Values)texture.Dispose();
            throw;
        }
        finally{device.SetRenderTargets(previous);}
        foreach(var texture in textures.Values)texture.Dispose();
        textures.Clear();
        foreach(var (id,texture) in built)textures.Add(id,texture);
    }
    public void Dispose()
    {
        device.DeviceReset-=OnDeviceReset;
        foreach(var texture in textures.Values)texture.Dispose();
        effect.Dispose();
    }
}
