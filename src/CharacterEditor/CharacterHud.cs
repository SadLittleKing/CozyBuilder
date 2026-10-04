using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Game.Assets;

namespace CharacterEditor;

// Small original bitmap font: portable, no content compiler or OS font dependency.
public sealed class CharacterHud : IDisposable
{
    readonly SpriteBatch batch;
    readonly Texture2D pixel;
    static readonly Dictionary<char,string> Glyphs = new();
    static CharacterHud()
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
    public CharacterHud(GraphicsDevice device)
    {
        batch = new SpriteBatch(device);
        pixel = new Texture2D(device,1,1);
        pixel.SetData(new[] { Color.White });
    }
    public static Rectangle Row(int index) => new(24,106+index*58,286,50);
    public static bool Contains(Point point) => new Rectangle(12,12,310,620).Contains(point);
    public static CharacterSlot? Hit(Point point)
    {
        for (int i = 0; i < 4; i++) if (Row(i).Contains(point)) return (CharacterSlot)i;
        return null;
    }
    public static GearSlot? HitGear(Point point)
    {
        for(int i=0;i<4;i++)if(Row(i+4).Contains(point))return (GearSlot)i;
        return null;
    }
    public void Draw(CharacterAssembly character, int width, int height,string status)
    {
        batch.Begin(samplerState:SamplerState.PointClamp);
        var ink = new Color(241,231,203);
        Fill(new Rectangle(12,12,310,620),new Color(44,58,51,245));
        Text("CHARACTER EDITOR",24,28,ink,2);
        Text("ONE BODY / MIX YOUR PARTY",24,54,new Color(178,197,167),1);
        Text(character.IsModular ? "CLICK A ROW OR PRESS 1-8" : "LEGACY MODEL / NO SWAPS",24,80,ink,1);
        var c = character.Configuration;
        string[] labels = ["1 HAIR", "2 OUTFIT", "3 HAND", "4 BACK"];
        string[] values = [c.Hair == "swept" ? "SWEPT" : "BUN",c.Outfit == "sage" ? "SAGE / SHORT SLEEVES" : "RUST / LONG SLEEVES",c.Hand.ToUpperInvariant(),c.Back.ToUpperInvariant()];
        for (int i = 0; i < 4; i++)
        {
            var r = Row(i);
            Fill(r,new Color(65,82,69));
            Text(labels[i],r.X+10,r.Y+7,new Color(181,201,170),1);
            Text(character.IsModular ? values[i] : "FIXED",r.X+10,r.Y+25,ink,2);
        }
        var gear=character.Gear;
        string[] gearLabels=["5 HEAD","6 BODY","7 GLOVES","8 FEET"];
        string[] gearValues=[gear.Head,gear.Body,gear.Gloves,gear.Feet];
        for(int i=0;i<4;i++)
        {
            var row=Row(i+4);Fill(row,new Color(65,82,69));
            Text(gearLabels[i],row.X+10,row.Y+7,new Color(181,201,170),1);
            Text(gearValues[i].Replace('_',' ').ToUpperInvariant(),row.X+10,row.Y+25,ink,1);
        }
        Text("F5 SAVE CHARACTER.JSON",24,580,ink,1);
        Text("R RELOAD / KEEP SELECTION",24,599,ink,1);
        Text($"{character.VisibleParts.Count} VISIBLE PARTS / STATIC",24,618,new Color(178,197,167),1);
        Fill(new Rectangle(12,height-66,Math.Min(width-24,760),54),new Color(44,58,51,235));
        Text("DRAG / ARROWS ORBIT   WHEEL / +/- ZOOM   SPACE TURNTABLE",24,height-55,ink,1);
        Text("HOME RESET CAMERA   W WIREFRAME   ESC EXIT",24,height-39,ink,1);
        Text(status,24,height-23,new Color(215,190,133),1);
        batch.End();
    }
    public void DrawDog(DogAssembly dog,int width,int height,string status)
    {
        batch.Begin(samplerState:SamplerState.PointClamp);
        var ink=new Color(241,231,203);
        Fill(new Rectangle(12,12,310,402),new Color(44,58,51,245));
        Text("TRAIL DOG VIEWER",24,28,ink,2);
        Text("ONE DOG / FOUR OPTIONAL SLOTS",24,54,new Color(178,197,167),1);
        Text("CLICK A ROW OR PRESS F1-F4",24,80,ink,1);
        var c=dog.Configuration;
        string[] labels=["F1 COLLAR","F2 HELMET","F3 ARMOR","F4 BACKPACK"];
        bool[] enabled=[c.Collar,c.Helmet,c.Armor,c.Backpack];
        for(int i=0;i<4;i++)
        {
            var row=Row(i);Fill(row,new Color(65,82,69));
            Text(labels[i],row.X+10,row.Y+7,new Color(181,201,170),1);
            Text(enabled[i]?"ON":"OFF",row.X+10,row.Y+25,ink,2);
        }
        Text("F5 SAVE DOG.JSON",24,354,ink,1);
        Text("R RELOAD / KEEP SELECTION",24,373,ink,1);
        Text($"{dog.VisibleParts.Count} VISIBLE PARTS / STATIC",24,392,new Color(178,197,167),1);
        Fill(new Rectangle(12,height-66,Math.Min(width-24,760),54),new Color(44,58,51,235));
        Text("DRAG / ARROWS ORBIT   WHEEL / +/- ZOOM   SPACE TURNTABLE",24,height-55,ink,1);
        Text("HOME RESET CAMERA   F1-F4 GEAR   ESC EXIT",24,height-39,ink,1);
        Text(status,24,height-23,new Color(215,190,133),1);
        batch.End();
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
    public void Dispose() { batch.Dispose(); pixel.Dispose(); }
}
