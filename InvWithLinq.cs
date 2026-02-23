using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.Shared.Cache;
using Ground_Items_With_Linq;
using ItemFilterLibrary;
using SharpDX;

namespace InvWithLinq;

public class InvWithLinq : BaseSettingsPlugin<InvWithLinqSettings>
{
    public static InvWithLinq Main;
    private readonly TimeCache<List<CustomItemData>> _inventItems;
    private bool _isInTown = true;
    public List<(ItemFilter Filter, InvRule Rule)> _itemFilters;

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
        _isInTown = area.IsHideout ||
                    area.IsTown ||
                    area.DisplayName.Contains("Azurite Mine") ||
                    area.DisplayName.Contains("Tane's Laboratory");
    }

    public override Job Tick() => null;

    public override void Render()
    {
        var hoveredItem = GetHoveredItem();
        if (!IsInventoryVisible()) return;
        if (!_isInTown && !Settings.RunOutsideTown) return;

        foreach (var (item, color) in GetFilteredItems())
        {
            var drawColor = hoveredItem != null &&
                            hoveredItem.Tooltip.GetClientRectCache.Intersects(item.ClientRectangleCache) &&
                            hoveredItem.Entity.Address != item.Entity.Address
                ? color with { A = 45 }
                : color;

            Graphics.DrawFrame(item.ClientRectangleCache, drawColor, Settings.FrameThickness);
        }

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

    private bool IsInventoryVisible() =>
        GameController.IngameState.IngameUi.InventoryPanel.IsVisible;

    private IEnumerable<(CustomItemData Item, Color Color)> GetFilteredItems()
    {
        foreach (var item in _inventItems.Value)
        {
            foreach (var (filter, rule) in _itemFilters)
            {
                if (filter.Matches(item))
                {
                    yield return (item, rule.Color);
                    break;
                }
            }
        }
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