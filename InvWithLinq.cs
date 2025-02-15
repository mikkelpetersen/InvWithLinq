using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.Shared.Cache;
using Ground_Items_With_Linq;
using ItemFilterLibrary;

namespace InvWithLinq;

public class InvWithLinq : BaseSettingsPlugin<InvWithLinqSettings>
{
    public static InvWithLinq Main;
    private readonly TimeCache<List<CustomItemData>> _inventItems;
    private bool _isInTown = true;
    public List<ItemFilter> _itemFilters;

    public InvWithLinq()
    {
        Name = "Inv With Linq";
        _inventItems = new TimeCache<List<CustomItemData>>(GetInventoryItems, 200);
    }

    public override bool Initialise()
    {
        Main = this;
        RulesDisplay.LoadAndApplyRules();
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        if (area.IsHideout ||
            area.IsTown ||
            area.DisplayName.Contains("Azurite Mine") ||
            area.DisplayName.Contains("Tane's Laboratory"))
            _isInTown = true;
        else
            _isInTown = false;
    }

    public override Job Tick()
    {
        return null;
    }

    public override void Render()
    {
        var hoveredItem = GetHoveredItem();
        if (!IsInventoryVisible())
            return;

        if (!_isInTown && !Settings.RunOutsideTown)
            return;

        foreach (var item in GetFilteredItems())
            if (hoveredItem != null && hoveredItem.Tooltip.GetClientRectCache.Intersects(item.ClientRectangleCache) &&
                hoveredItem.Entity.Address != item.Entity.Address)
                Graphics.DrawFrame(item.ClientRectangleCache, Settings.FrameColor.Value with { A = 45 },
                    Settings.FrameThickness);
            else
                Graphics.DrawFrame(item.ClientRectangleCache, Settings.FrameColor, Settings.FrameThickness);

        PerformItemFilterTest(hoveredItem);
    }

    public override void DrawSettings()
    {
        base.DrawSettings();


        RulesDisplay.DrawSettings();
    }


    private List<CustomItemData> GetInventoryItems()
    {
        var inventoryItems = new List<CustomItemData>();

        if (!IsInventoryVisible()) return inventoryItems;

        var inventory = GameController?.Game?.IngameState?.Data?.ServerData?.PlayerInventories[0]?.Inventory;
        var items = inventory?.InventorySlotItems;

        if (items == null) return inventoryItems;

        foreach (var item in items)
        {
            if (item.Item == null || item.Address == 0) continue;

            inventoryItems.Add(new CustomItemData(item.Item, GameController, item.GetClientRect()));
        }

        return inventoryItems;
    }

    private Element GetHoveredItem()
    {
        return GameController?.IngameState?.UIHover?.Address != 0 && GameController.IngameState.UIHover.Entity.IsValid
            ? GameController.IngameState.UIHover
            : null;
    }

    private bool IsInventoryVisible()
    {
        return GameController.IngameState.IngameUi.InventoryPanel.IsVisible;
    }

    private IEnumerable<CustomItemData> GetFilteredItems()
    {
        return _inventItems.Value.Where(x => _itemFilters.Any(y => y.Matches(x)));
    }

    private void PerformItemFilterTest(Element hoveredItem)
    {
        if (Settings.FilterTest.Value is { Length: > 0 } && hoveredItem != null)
        {
            var filter = ItemFilter.LoadFromString(Settings.FilterTest);
            var matched = filter.Matches(new ItemData(hoveredItem.Entity, GameController));
            DebugWindow.LogMsg($"{Name}: [Filter Test] Hovered Item: {matched}", 5);
        }
    }
}