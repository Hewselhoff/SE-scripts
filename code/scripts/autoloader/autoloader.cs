/* Auto-Loader System for Space Engineers
 * 
 * This script automatically loads materials from source containers to target containers
 * based on the configuration specified in the Programmable Block's custom data.
 * 
 * For full documentation and usage instructions, please see the README.md file.
 */        

private const string VERSION = "1.0.0";
private const string DISPLAY_TEXT_PANEL_TAG = "[AUTOLOAD]";
private string BROADCAST_TAG = "[GANTRY_INVENTORY]";

private IMyTextPanel _displayPanel;
private StringBuilder _outputBuilder = new StringBuilder();
private List<string> _sourceNames = new List<string>();
private List<string> _targetNames = new List<string>();
private Dictionary<string, float> _componentAmounts = new Dictionary<string, float>();
private Dictionary<string, float> _lastComponentAmounts = new Dictionary<string, float>();
private List<IMyTerminalBlock> _sourceContainers = new List<IMyTerminalBlock>();
private List<IMyTerminalBlock> _targetContainers = new List<IMyTerminalBlock>();


public Program()
{
    // Constructor
    Runtime.UpdateFrequency = UpdateFrequency.Once;

    
    //ConfigFile.RegisterProperty("inventoryTag", ConfigValueType.String, "[GANTRY_INVENTORY]");
    //BROADCAST_TAG = ConfigFile.Get<string>("inventoryTag");

    // Parse the initial configuration to set up default values
    ParseConfiguration();

    Echo("Auto-Loader System Initialized");
}        

public void Main(string argument, UpdateType updateSource)
{
    _outputBuilder.Clear();
    _outputBuilder.AppendLine("Auto-Loader System v" + VERSION);
    _outputBuilder.AppendLine();
    
    // Parse configuration before each run
    if (!ParseConfiguration())
    {
        _outputBuilder.AppendLine("ERROR: Failed to parse configuration.");
        _outputBuilder.AppendLine("Please check your Custom Data format.");
        UpdateDisplay();
        return;
    }

    // Find the display panel
    _displayPanel = FindDisplayPanel();
    if (_displayPanel == null)
    {
        Echo("WARNING: No display panel found.");
        Echo($"Name a text panel with the tag {DISPLAY_TEXT_PANEL_TAG} to show status.");
    }            

    // Find target containers
    _targetContainers.Clear();
    
    if (_sourceNames.Count == 0)
    {
        _outputBuilder.AppendLine("ERROR: No source containers specified in configuration.");
        UpdateDisplay();
        return;
    }
    
    if (_targetNames.Count == 0)
    {
        _outputBuilder.AppendLine("ERROR: No target containers specified in configuration.");
        UpdateDisplay();
        return;
    }

    // Find target containers
    foreach (string targetName in _targetNames)
    {
        IMyTerminalBlock targetBlock = FindBlockByName(targetName);
        if (targetBlock == null)
        {
            _outputBuilder.AppendLine($"ERROR: Target container '{targetName}' not found.");
            continue;
        }

        if (!(targetBlock is IMyCargoContainer || targetBlock is IMyShipConnector || targetBlock is IMyShipWelder))
        {
            _outputBuilder.AppendLine($"ERROR: '{targetName}' is not a cargo container or connector.");
            continue;
        }

        _targetContainers.Add(targetBlock);
    }
    
    if (_targetContainers.Count == 0)
    {
        _outputBuilder.AppendLine("ERROR: No valid target containers found.");
        UpdateDisplay();
        return;
    }

    // Check if we need to perform loading action
    if (argument.Trim().ToLower() == "load")
    {
        PerformLoading(_sourceNames);
    }
    
    // Update display with current status
    UpdateInventoryStatus();
    UpdateDisplay();
    ReportInventory();
}

private void PerformLoading(List<string> sourceNames)
{
    // If no connector on this grid is connected, warn the user
    List<IMyShipConnector> connectors = new List<IMyShipConnector>();
    GridTerminalSystem.GetBlocksOfType(connectors, c => c.IsSameConstructAs(Me) && c.Status == MyShipConnectorStatus.Connected);
    if (connectors.Count == 0)
    {
        _outputBuilder.AppendLine("ERROR: No docked connectors found.");
        UpdateDisplay();
        ReportInventory();
        return;
    }            

    // Find source and target containers
    _sourceContainers.Clear();
    
    // Find source containers
    foreach (string sourceName in sourceNames)
    {
        IMyTerminalBlock sourceBlock = FindBlockByName(sourceName);
        if (sourceBlock == null)
        {
            _outputBuilder.AppendLine($"INFO: Source container '{sourceName}' not found.");
            continue;
        }

        if (!(sourceBlock is IMyCargoContainer || sourceBlock is IMyShipConnector))
        {
            _outputBuilder.AppendLine($"ERROR: '{sourceName}' is not a cargo container or connector.");
            continue;
        }

        _sourceContainers.Add(sourceBlock);
    }

    if (_sourceContainers.Count == 0)
    {
        _outputBuilder.AppendLine("ERROR: No valid source containers found.");
        UpdateDisplay();
        ReportInventory();
        return;
    }
    
    _outputBuilder.AppendLine("Loading components...");
    
    foreach (var componentEntry in _componentAmounts)
    {
        string componentName = componentEntry.Key;
        float targetAmount = componentEntry.Value;
        
        // Check if the target amount is a fraction or absolute value
        bool isFractional = targetAmount > 0 && targetAmount <= 1;
        
        // Calculate current amount in target containers
        float currentAmount = GetItemAmountInContainers(componentName, _targetContainers);
        
        // Calculate target amount if fractional
        float targetAbsoluteAmount;
        if (isFractional)
        {
            float maxCapacity = GetMaxVolumeCapacity(_targetContainers);
            targetAbsoluteAmount = maxCapacity * targetAmount;
        }
        else
        {
            targetAbsoluteAmount = targetAmount;
        }
        
        // Skip if we already have enough of this component
        if (currentAmount >= targetAbsoluteAmount)
        {
            continue;
        }
          // Calculate amount to transfer
        float amountToTransfer = targetAbsoluteAmount - currentAmount;
        
        // Try to transfer from source containers
        float amountTransferred = TransferItemToTargets(componentName, amountToTransfer);
        
        if (amountTransferred <= 0)
        {
            _outputBuilder.AppendLine($"Warning: No {componentName} available to transfer");
        }
        else if (amountTransferred < amountToTransfer)
        {
            float percentTransferred = (amountTransferred / amountToTransfer) * 100;
            _outputBuilder.AppendLine($"Partial transfer: {componentName} - {Math.Floor(amountTransferred)}/{Math.Floor(amountToTransfer)} ({Math.Floor(percentTransferred)}%)");
        }
    }
    
    _outputBuilder.AppendLine("Loading complete.");
}

private void UpdateInventoryStatus()
{
    _outputBuilder.AppendLine("Loaded Components:");
    
    // Store previous status for comparison
    _lastComponentAmounts = new Dictionary<string, float>(_componentAmounts);
    
    // For each component, check current amount in target containers
    foreach (var componentEntry in _componentAmounts)
    {
        string componentName = componentEntry.Key;
        float targetAmount = componentEntry.Value;
        
        float currentAmount = GetItemAmountInContainers(componentName, _targetContainers);
        float percentage;
        
        // Calculate percentage based on whether target is fractional or absolute
        if (targetAmount > 0 && targetAmount <= 1)
        {
            float maxCapacity = GetMaxVolumeCapacity(_targetContainers);
            percentage = (currentAmount / (maxCapacity * targetAmount)) * 100;
        }
        else
        {
            percentage = (currentAmount / targetAmount) * 100;
        }
        
        // Cap the percentage at 100% for display
        percentage = Math.Min(percentage, 100);
        
        _outputBuilder.AppendLine($"  {componentName}: {Math.Floor(currentAmount)} ({Math.Floor(percentage)}%)");
    }
}

/// <summary>
/// Report inventory of cargo containers.
/// </summary>
private void ReportInventory(){
   StringBuilder statusStr = new StringBuilder();
   IGC.SendBroadcastMessage(BROADCAST_TAG, _outputBuilder.ToString());
}

private void UpdateDisplay()
{
    // If we have a display panel, update it
    if (_displayPanel != null)
    {
        _displayPanel.ContentType = ContentType.TEXT_AND_IMAGE;
        _displayPanel.WriteText(_outputBuilder.ToString());
    }
    
    // Also echo the output to the programmable block's terminal
    Echo(_outputBuilder.ToString());
}

private IMyTextPanel FindDisplayPanel()
{
    List<IMyTextPanel> panels = new List<IMyTextPanel>();
    GridTerminalSystem.GetBlocksOfType(panels, p => p.CustomName.Contains(DISPLAY_TEXT_PANEL_TAG) && p.IsSameConstructAs(Me));
    
    return panels.FirstOrDefault();
}        

private IMyTerminalBlock FindBlockByName(string name)
{
    List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocks(blocks);
    
    // Match blocks that contain the specified string in their name, rather than requiring exact match
    return blocks.FirstOrDefault(b => b.CustomName.Contains(name));
}

private float GetItemAmountInContainers(string itemName, List<IMyTerminalBlock> containers)
{
    float totalAmount = 0;
    
    foreach (var container in containers)
    {
        IMyInventory inventory = container.GetInventory(0);
        if (inventory == null)
        {
            continue;
        }
        
        List<MyInventoryItem> items = new List<MyInventoryItem>();
        inventory.GetItems(items);
        
        foreach (var item in items)
        {
            if (item.Type.SubtypeId.ToString() == itemName)
            {
                totalAmount += (float)item.Amount;
            }
        }
    }
    
    return totalAmount;
}

private float GetMaxVolumeCapacity(List<IMyTerminalBlock> containers)
{
    float totalCapacity = 0;
    
    foreach (var container in containers)
    {
        IMyInventory inventory = container.GetInventory(0);
        if (inventory == null)
        {
            continue;
        }
        
        totalCapacity += (float)inventory.MaxVolume;
    }
    
    return totalCapacity;
}

private float TransferItemToTargets(string itemName, float amount)
{
    float remainingAmount = amount;
    float transferredAmount = 0;
    
    // For each source container, try to transfer the requested items
    foreach (var sourceContainer in _sourceContainers)
    {
        IMyInventory sourceInventory = sourceContainer.GetInventory(0);
        if (sourceInventory == null)
        {
            continue;
        }
        
        List<MyInventoryItem> items = new List<MyInventoryItem>();
        sourceInventory.GetItems(items);
        
        foreach (var item in items)
        {
            if (item.Type.SubtypeId.ToString() == itemName)
            {
                float availableAmount = (float)item.Amount;
                float transferAmount = Math.Min(availableAmount, remainingAmount);
                  // Try to transfer to each target container
                foreach (var targetContainer in _targetContainers)
                {
                    IMyInventory targetInventory = targetContainer.GetInventory(0);
                    if (targetInventory == null)
                    {
                        continue;
                    }
                    
                    // Check how much space is available in the target inventory
                    float availableVolume = (float)(targetInventory.MaxVolume - targetInventory.CurrentVolume);
                    // Get the volume per item (approximate if we don't know exactly)
                    float volumePerItem = item.Type.GetItemInfo().Volume;
                    
                    // Calculate maximum items that can fit in the available space
                    float maxItemsFit = volumePerItem > 0 ? availableVolume / volumePerItem : transferAmount;
                    
                    // Adjust transfer amount based on available space
                    float adjustedTransferAmount = Math.Min(transferAmount, maxItemsFit);
                    
                    // Skip if no items can be added
                    if (adjustedTransferAmount <= 0 || !targetInventory.CanItemsBeAdded((VRage.MyFixedPoint)adjustedTransferAmount, item.Type))
                    {
                        continue;
                    }
                      // Perform transfer with space-adjusted amount
                    if (sourceInventory.TransferItemTo(targetInventory, item, (VRage.MyFixedPoint)adjustedTransferAmount))
                    {
                        remainingAmount -= adjustedTransferAmount;
                        transferredAmount += adjustedTransferAmount;
                        transferAmount -= adjustedTransferAmount; // Reduce the amount we're still trying to transfer
                        
                        // If we couldn't transfer the full amount because of space limitations, log it
                        if (adjustedTransferAmount < transferAmount && adjustedTransferAmount < maxItemsFit)
                        {
                            // This component transfer was limited by space
                            _outputBuilder.AppendLine($"Note: Transfer of {itemName} limited by available space in target container");
                        }
                        
                        // Don't break here - continue to next container if we still have items to transfer
                        if (transferAmount <= 0)
                        {
                            break; // Break only if we've transferred everything from this source item
                        }
                    }
                }
                
                // If we've transferred all we need, we can stop
                if (remainingAmount <= 0)
                {
                    return transferredAmount;
                }
            }
        }
    }
    
    // Return the actual amount transferred
    return transferredAmount;
}


/// <summary>
/// Parses the configuration from the ProgrammableBlock's CustomData
/// </summary>
private bool ParseConfiguration()
{
    string configText = Me.CustomData;
    if (string.IsNullOrWhiteSpace(configText))
    {
        // Generate default configuration
        configText = GenerateDefaultConfig();
        Me.CustomData = configText;
        Echo("No configuration found. Default config added to Custom Data.");
    }

    try
    {
        // Clear previous configuration
        _sourceNames.Clear();
        _targetNames.Clear();
        _componentAmounts.Clear();

        // Split the text into lines
        var lines = configText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        
        string currentSection = null;
        int expectedIndent = 0;

        foreach (var line in lines)
        {
            // Skip blank or comment lines
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                continue;

            string trimmed = line.Trim();
            int colonIndex = trimmed.IndexOf(':');
            if (colonIndex < 0) continue; // Invalid line, skip

            int indent = CountLeadingSpaces(line);

            // Check if this is a section header or a property
            if (indent == 0)
            {
                // Root level
                string key = trimmed.Substring(0, colonIndex).Trim();
                string value = trimmed.Substring(colonIndex + 1).Trim();
                
                // Check if this is a section header
                if (colonIndex == trimmed.Length - 1)
                {
                    currentSection = key.ToLower();
                    expectedIndent = -1; // Will be set on first line within section
                }
                else
                {
                    // This is a root property
                    currentSection = null;
                    
                    // Parse based on property name
                    if (key.ToLower() == "sources")
                    {
                        _sourceNames = ParseStringList(value);
                    }
                    else if (key.ToLower() == "targets")
                    {
                        _targetNames = ParseStringList(value);
                    }
                    else if (key.ToLower() == "tag")
                    {
                       BROADCAST_TAG = value;
                    }
                }
            }
            else
            {
                // This is part of a section
                if (currentSection != null)
                {
                    // Set expected indent on first line in section
                    if (expectedIndent < 0)
                    {
                        expectedIndent = indent;
                    }

                    // Only process if this is at the expected indent level
                    if (indent >= expectedIndent)
                    {
                        string key = trimmed.Substring(0, colonIndex).Trim();
                        string value = trimmed.Substring(colonIndex + 1).Trim();
                        
                        // Process based on current section
                        if (currentSection == "components")
                        {
                            // Parse component amount
                            float componentAmount;
                            if (float.TryParse(value, out componentAmount))
                            {
                                _componentAmounts[key] = componentAmount;
                            }
                            else
                            {
                                Echo($"Failed to parse component amount for '{key}': {value}");
                            }
                        }
                    }
                }
            }
        }

        return true;
    }
    catch (Exception ex)
    {
        Echo("Error parsing configuration: " + ex.Message);
        return false;
    }
}

/// <summary>
/// Parses a string list from a bracketed, comma-separated format: [item1, item2, item3]
/// </summary>
private List<string> ParseStringList(string value)
{
    List<string> result = new List<string>();
    
    value = value.Trim();
    if (value.StartsWith("[") && value.EndsWith("]"))
    {
        value = value.Substring(1, value.Length - 2);
    }

    string[] elements = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
    foreach (var elem in elements)
    {
        result.Add(elem.Trim());
    }

    return result;
}

/// <summary>
/// Counts the number of leading spaces in a string.
/// </summary>
private static int CountLeadingSpaces(string line)
{
    int count = 0;
    for (int i = 0; i < line.Length; i++)
    {
        if (line[i] == ' ')
            count++;
        else
            break;
    }
    return count;
}

/// <summary>
/// Generates a default configuration.
/// </summary>
private string GenerateDefaultConfig()
{
    StringBuilder sb = new StringBuilder();
    
    sb.AppendLine("# Auto-Loader System Configuration");
    sb.AppendLine();
    sb.AppendLine("# Broadcast Tag for remote displays");
    sb.AppendLine("tag: [TAG_NAME]");
    sb.AppendLine();
    sb.AppendLine("# Source containers to pull materials from");
    sb.AppendLine("sources: [Base Cargo]");
    sb.AppendLine();
    sb.AppendLine("# Target containers to load materials into");
    sb.AppendLine("targets: [Ship Cargo]");
    sb.AppendLine();
    sb.AppendLine("# Components to load and their amounts");
    sb.AppendLine("# Values between 0 and 1 are treated as fractions of available space");
    sb.AppendLine("# Values greater than 1 are treated as absolute amounts");
    sb.AppendLine("components:");
    sb.AppendLine("    SteelPlate: 1000");
    sb.AppendLine("    Construction: 500");
    sb.AppendLine("    SmallTube: 300");
    sb.AppendLine("    Motor: 100");
    
    return sb.ToString();
}
