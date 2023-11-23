using System.Collections.Generic;
using System.Text.Json.Serialization;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace InvWithLinq;

public class InvWithLinqSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public ColorNode FrameColor { get; set; } = new ColorNode(Color.Red);
    public RangeNode<int> FrameThickness { get; set; } = new RangeNode<int>(1, 1, 20);
    
    [JsonIgnore]
    public ButtonNode ReloadFilters { get; set; } = new ButtonNode();
    
    public List<InvRule> InvRules { get; set; } = new List<InvRule>();
}

public class InvRule
{
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public bool Enabled { get; set; } = false;

    public InvRule(string name, string location, bool enabled)
    {
        Name = name;
        Location = location;
        Enabled = enabled;
    }
}