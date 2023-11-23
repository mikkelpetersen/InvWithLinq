using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ItemFilterLibrary;

public class CustomItemData : ItemData
{
    public CustomItemData(Entity queriedItem, GameController gc) : base(queriedItem, gc)
    {
    }

    public CustomItemData(Entity queriedItem, GameController gc, SharpDX.RectangleF getClientRectCache) : base(queriedItem, gc)
    {
        ClientRectangleCache = getClientRectCache;
    }

    public SharpDX.RectangleF ClientRectangleCache { get; set; }
}