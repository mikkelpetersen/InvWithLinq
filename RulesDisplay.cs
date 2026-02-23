using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using ExileCore;
using ImGuiNET;
using InvWithLinq;
using ItemFilterLibrary;
using static InvWithLinq.InvWithLinq;

namespace Ground_Items_With_Linq;

public class RulesDisplay
{
    public static void DrawSettings()
    {
        ImGui.Separator();
        if (ImGui.Button("Open Filter Folder"))
        {
            var configDirectory = Main.ConfigDirectory;
            var customConfigDirectory = !string.IsNullOrEmpty(Main.Settings.CustomConfigDirectory)
                ? Path.Combine(Path.GetDirectoryName(Main.ConfigDirectory)!, Main.Settings.CustomConfigDirectory)
                : null;

            var directoryToOpen = Directory.Exists(customConfigDirectory)
                ? customConfigDirectory
                : configDirectory;

            Process.Start("explorer.exe", directoryToOpen);
        }

        if (ImGui.Button("Reload Rules"))
            LoadAndApplyRules();

        ImGui.Separator();

        if (ImGui.BeginTable("RulesTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable))
        {
            ImGui.TableSetupColumn("Drag", ImGuiTableColumnFlags.WidthFixed, 40);
            ImGui.TableSetupColumn("Toggle", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("Color", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("File", ImGuiTableColumnFlags.None);
            ImGui.TableHeadersRow();

            var rules = Main.Settings.InvRules;
            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.PushID($"Drag_{rule.Location}");

                var dropTargetStart = ImGui.GetCursorScreenPos();

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
                ImGui.Button("=", new Vector2(30, 20));
                ImGui.PopStyleColor();

                if (ImGui.BeginDragDropSource())
                {
                    ImGuiHelpers.SetDragDropPayload("RuleIndex", i);
                    ImGui.Text(rule.Name);
                    ImGui.EndDragDropSource();
                }

                ImGui.SetCursorScreenPos(dropTargetStart);
                ImGui.InvisibleButton($"DropTarget_{rule.Location}", new Vector2(30, 20));

                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGuiHelpers.AcceptDragDropPayload<int>("RuleIndex");
                    if (payload != null && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    {
                        var movedRule = rules[payload.Value];
                        rules.RemoveAt(payload.Value);
                        rules.Insert(i, movedRule);
                        LoadAndApplyRules();
                    }

                    ImGui.EndDragDropTarget();
                }

                ImGui.PopID();

                ImGui.TableSetColumnIndex(1);
                ImGui.PushID($"Toggle_{rule.Location}");
                var enabled = rule.Enabled;
                if (ImGui.Checkbox("", ref enabled))
                {
                    rule.Enabled = enabled;
                    LoadAndApplyRules();
                }
                ImGui.PopID();

                ImGui.TableSetColumnIndex(2);
                ImGui.PushID($"Color_{rule.Location}");
                var c = rule.Color;
                var vec = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
                if (ImGui.ColorEdit4("##Color", ref vec,
                        ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
                {
                    rule.Color = new SharpDX.Color(
                        (byte)(vec.X * 255),
                        (byte)(vec.Y * 255),
                        (byte)(vec.Z * 255),
                        (byte)(vec.W * 255));
                }
                ImGui.PopID();

                ImGui.TableSetColumnIndex(3);
                ImGui.PushID(rule.Location);

                var directoryPart = Path.GetDirectoryName(rule.Location)?.Replace("\\", "/") ?? "";
                var fileName = Path.GetFileName(rule.Location);
                var fileFullPath = Path.Combine(GetConfigFileDirectory(), rule.Location);
                var cellWidth = ImGui.GetContentRegionAvail().X;

                ImGui.InvisibleButton($"FileCell_{rule.Location}", new Vector2(cellWidth, ImGui.GetFrameHeight()));
                ImGui.SameLine();
                StartContextMenu(fileName, fileFullPath, $"FileCell_{rule.Location}");

                var textPos = ImGui.GetItemRectMin();
                ImGui.SetCursorScreenPos(textPos);

                if (!string.IsNullOrEmpty(directoryPart))
                {
                    ImGui.TextColored(new Vector4(0.4f, 0.7f, 1.0f, 1.0f), directoryPart + "/");
                    ImGui.SameLine(0, 0);
                    ImGui.Text(fileName);
                }
                else
                {
                    ImGui.Text(fileName);
                }

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        void StartContextMenu(string fileName, string fileFullPath, string contextMenuId)
        {
            if (ImGui.BeginPopupContextItem(contextMenuId))
            {
                if (ImGui.MenuItem("Open"))
                    try
                    {
                        Process.Start(new ProcessStartInfo { FileName = fileFullPath, UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        DebugWindow.LogError($"[InvWithLinq] Failed to Open File: {ex.Message}", 10);
                    }

                ImGui.EndPopup();
            }
        }
    }

    private static string GetConfigFileDirectory()
    {
        var configFileDirectory = Main.ConfigDirectory;
        if (!string.IsNullOrEmpty(Main.Settings.CustomConfigDirectory))
        {
            var customConfigFileDirectory = Path.Combine(
                Path.GetDirectoryName(Main.ConfigDirectory) ?? Main.ConfigDirectory,
                Main.Settings.CustomConfigDirectory);

            if (Directory.Exists(customConfigFileDirectory))
                configFileDirectory = customConfigFileDirectory;
            else
                DebugWindow.LogError("[InvWithLinq] Custom Config Folder Does Not Exist.", 10);
        }

        return configFileDirectory;
    }

    private static ItemFilter LoadItemFilterWithRetry(string rulePath)
    {
        const int maxRetries = 10;
        var attempt = 0;
        while (true)
            try
            {
                return ItemFilter.LoadFromPath(rulePath);
            }
            catch (IOException e)
            {
                attempt++;
                if (attempt >= maxRetries)
                    throw new IOException($"[InvWithLinq] Failed to Load File: {rulePath}", e);
                Thread.Sleep(100);
            }
    }

    public static void LoadAndApplyRules()
    {
        var configFileDirectory = GetConfigFileDirectory();
        var existingRules = Main.Settings.InvRules;
        try
        {
            var diskFiles = new DirectoryInfo(configFileDirectory)
                .GetFiles("*.ifl", SearchOption.AllDirectories)
                .ToList();

            var newRules = diskFiles
                .Select(fileInfo => new InvRule(
                    fileInfo.Name,
                    Path.GetRelativePath(configFileDirectory, fileInfo.FullName),
                    false))
                .ExceptBy(existingRules.Select(rule => rule.Location), groundRule => groundRule.Location)
                .ToList();

            foreach (var groundRule in existingRules)
            {
                var fullPath = Path.Combine(configFileDirectory, groundRule.Location);
                if (File.Exists(fullPath))
                    newRules.Add(groundRule);
                else
                    DebugWindow.LogError($"[InvWithLinq] File '{groundRule.Name}' Not Found.", 10);
            }

            Main._itemFilters = newRules
                .Where(rule => rule.Enabled)
                .Select(rule =>
                {
                    var rulePath = Path.Combine(configFileDirectory, rule.Location);
                    return (LoadItemFilterWithRetry(rulePath), rule);
                })
                .ToList();

            Main.Settings.InvRules = newRules;
        }
        catch (Exception e)
        {
            DebugWindow.LogError($"[InvWithLinq] Error: {e.Message}", 10);
        }
    }
}