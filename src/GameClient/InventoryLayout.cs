using Microsoft.Xna.Framework;

namespace GameClient;

// Shared by rendering and mouse hit testing so the clickable rows match the art.
public readonly struct InventoryLayout
{
    public readonly Rectangle Panel,Preview;
    public readonly int BagX,ChestX,GearX,BagTop,ChestTop,GearTop;
    public InventoryLayout(int width,int height)
    {
        int panelWidth=Math.Min(910,width-24),panelHeight=Math.Min(625,height-90);
        Panel=new Rectangle((width-panelWidth)/2,(height-panelHeight)/2,panelWidth,panelHeight);
        BagX=Panel.X+18;ChestX=Panel.X+333;GearX=Panel.Right-240;
        BagTop=Panel.Y+101;ChestTop=BagTop;GearTop=Panel.Y+432;
        Preview=new Rectangle(GearX,Panel.Y+102,220,280);
    }
    public Rectangle BagRow(int index)=>new(BagX-4,BagTop+index*27-3,290,25);
    public Rectangle ChestRow(int index)=>new(ChestX-4,ChestTop+index*20-3,245,19);
    public Rectangle GearRow(int index)=>new(GearX-4,GearTop+index*31-3,232,29);
}
