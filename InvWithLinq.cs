using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.Shared.Cache;
using ImGuiNET;
using ItemFilterLibrary;

namespace InvWithLinq;

public class InvWithLinq : BaseSettingsPlugin<InvWithLinqSettings>
{
    private readonly TimeCache<List<CustomItemData>> _inventItems;
    private List<ItemFilter> _itemFilters;
    private bool _isInTown = true;

    public InvWithLinq()
    {
        Name = "Inv With Linq";
        _inventItems = new TimeCache<List<CustomItemData>>(GetInventoryItems, 200);
    }

    public override bool Initialise()
    {
        Settings.ReloadFilters.OnPressed = LoadRules;
        LoadRules();
        return true;
    }

    public override void AreaChange(AreaInstance area)
    {
        if (area.IsHideout ||
            area.IsTown ||
            area.DisplayName.Contains("Azurite Mine") ||
            area.DisplayName.Contains("Tane's Laboratory"))
        {
            _isInTown = true;
        }
        else
        {
            _isInTown = false;
        }
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
        {
            if (hoveredItem != null && hoveredItem.Tooltip.GetClientRectCache.Intersects(item.ClientRectangleCache) && hoveredItem.Entity.Address != item.Entity.Address)
            {
                Graphics.DrawFrame(item.ClientRectangleCache, Settings.FrameColor.Value with { A = 45 }, Settings.FrameThickness);
            }
            else
            {
                Graphics.DrawFrame(item.ClientRectangleCache, Settings.FrameColor, Settings.FrameThickness);
            }
        }
        
        PerformItemFilterTest(hoveredItem);
    }
    
    public override void DrawSettings()
    {
        base.DrawSettings();

        if (ImGui.Button("Open Build Folder"))
        {
            var configDirectory = ConfigDirectory;
            var customConfigDirectory = !string.IsNullOrEmpty(Settings.CustomConfigDirectory)
                ? Path.Combine(Path.GetDirectoryName(ConfigDirectory)!, Settings.CustomConfigDirectory)
                : null;
            
            var directoryToOpen = Directory.Exists(customConfigDirectory)
                ? customConfigDirectory
                : configDirectory;

            Process.Start("explorer.exe", directoryToOpen);
        }

        ImGui.Separator();
        ImGui.BulletText("Select Rules To Load");

        var invRules = new List<InvRule>(Settings.InvRules);

        for (int i = 0; i < invRules.Count; i++)
        {
            if (ImGui.ArrowButton($"##UpButton{i}", ImGuiDir.Up) && i > 0)
                (invRules[i - 1], invRules[i]) = (invRules[i], invRules[i - 1]);

            ImGui.SameLine(); ImGui.Text(" "); ImGui.SameLine();

            if (ImGui.ArrowButton($"##DownButton{i}", ImGuiDir.Down) && i < invRules.Count - 1)
                (invRules[i + 1], invRules[i]) = (invRules[i], invRules[i + 1]);

            ImGui.SameLine(); ImGui.Text(" - "); ImGui.SameLine();

            var refToggle = invRules[i].Enabled;
            if (ImGui.Checkbox($"{invRules[i].Name}##Checkbox{i}", ref refToggle))
                invRules[i].Enabled = refToggle;
        }

        Settings.InvRules = invRules;
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

    private void LoadRules()
    {
        string configDirectory = ConfigDirectory;
        List<InvRule> existingRules = Settings.InvRules;

        if (!string.IsNullOrEmpty(Settings.CustomConfigDirectory))
        {
            var customConfigFileDirectory = Path.Combine(Path.GetDirectoryName(ConfigDirectory)!, Settings.CustomConfigDirectory);
            
            if (Directory.Exists(customConfigFileDirectory))
            {
                configDirectory = customConfigFileDirectory;
            }
            else
            {
                DebugWindow.LogError( $"{Name}: Custom Config Folder does not exist.", 15);
            }
        }

        try
        {
            var newRules = new DirectoryInfo(configDirectory).GetFiles("*.ifl")
                .Select(x => new InvRule(x.Name, Path.GetRelativePath(configDirectory, x.FullName), false))
                .ExceptBy(existingRules.Select(x => x.Location), x => x.Location)
                .ToList();

            foreach (var rule in existingRules)
            {
                var fullPath = Path.Combine(configDirectory, rule.Location);
                if (File.Exists(fullPath))
                {
                    newRules.Add(rule);
                }
                else
                {
                    DebugWindow.LogError($"{Name}: File \"{rule.Name}\" does not exist.", 15);
                }
            }

            _itemFilters = newRules
                .Where(x => x.Enabled)
                .Select(x => ItemFilter.LoadFromPath(Path.Combine(configDirectory, x.Location)))
                .ToList();

            Settings.InvRules = newRules;
        }
        catch (Exception e)
        {
            DebugWindow.LogError($"{Name}: Filter Load Error.\n{e}", 15);
        }
    }
}