using Game.Assets;
using Microsoft.Xna.Framework;

namespace GameClient;

public sealed class ItemModels
{
    Dictionary<string,GlbScene> models=new(StringComparer.Ordinal);
    public ItemModels()
    {
        Reload();
    }
    public void Reload()
    {
        string? directory=new[]{Directory.GetCurrentDirectory(),AppContext.BaseDirectory}
            .Select(root=>Path.Combine(root,"assets","items"))
            .FirstOrDefault(Directory.Exists);
        if(directory==null)throw new DirectoryNotFoundException("Missing assets/items directory.");
        var loaded=new Dictionary<string,GlbScene>(StringComparer.Ordinal);
        foreach(var item in ItemCatalog.All)
            loaded.Add(item.Id,GlbScene.Load(Path.Combine(directory,item.Id+".glb")));
        models=loaded;
    }
    public GlbScene this[string id]=>models[id];
    public static Matrix DropWorld(GlbScene model,Vector2 position)
    {
        Vector3 extent=model.Max-model.Min;
        float scale=.46f/Math.Max(extent.X,Math.Max(extent.Y,extent.Z));
        Vector3 center=(model.Min+model.Max)*.5f;
        return Matrix.CreateTranslation(-center)*Matrix.CreateScale(scale)*
            Matrix.CreateTranslation(position.X,.07f+extent.Y*scale*.5f,position.Y);
    }
}
