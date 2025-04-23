// GridMap Constants
public const string GRIDMAP_IGC_STATUS_TAG = "GRIDMAP:STATUS";
public const string GRIDMAP_IGC_INIT_TAG = "GRIDMAP:INIT";
public const long GRIDMAP_TICK_INTERVAL_MS = 16; // 60Hz = 16.7 ms; round down so our check hits on the closest update
public const long GRIDMAP_STATUS_INTERVAL_TICKS = 200; // 200 ticks = 3.2s (every other Update100 updates)
public const long GRIDMAP_STATUS_INTERVAL_MS = GRIDMAP_TICK_INTERVAL_MS * GRIDMAP_STATUS_INTERVAL_TICKS; // 3.2s
public const long GRIDMAP_STALE_MULTIPLIER  = 3; // 3 x times the status interval
// Runtime update Types
public const UpdateType GRIDMAP_STATUS_UPDATE_TYPE= UpdateType.Update100; // 100 ticks = 1.67s
public const UpdateType GRIDMAP_CONTINUE_UPDATE_TYPE = UpdateType.Once;
public const UpdateType GRIDMAP_TRIGGERED_UPDATE_TYPES = UpdateType.Trigger | UpdateType.Terminal | UpdateType.Mod | UpdateType.Script; // Triggered updates
public const UpdateType GRIDMAP_RECURRING_UPDATE_TYPES = UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100 | UpdateType.Once; // Recurring updates
public const UpdateType GRIDMAP_IGC_UPDATE_TYPES = UpdateType.IGC | UpdateType.Once; // IGC updates

/// <summary>
/// Represents an immutable grid status message that can be converted to/from an immutable for IGC transmitted.
/// </summary>
public sealed class GridStatusMessage
{
    // Immutable properties
    public long Address { get; } // Unique IGC address of the grid's modem
    public string Name { get; } // Name of the grid
    public Vector3 Position { get; } // Current position of the grid in meters (antenna position)
    public float Range { get; } // Grid's broadcast range in meters
    public Vector3 Velocity { get; } // Current velocity of the grid in meters per second
    public bool Online { get; } // Whether the grid is online or not
    public long Timestamp { get; }

    /// <summary>
    /// Constructor for constructing new GridStatusMessages (TX).
    /// </summary>
    /// <param name="name">The name of the grid</param>
    /// <param name="position">The current position of the grid in meters (antenna position)</param>
    /// <param name="range">The grid's broadcast range in meters</param>
    /// <param name="velocity">The current velocity of the grid in meters per second</param>
    /// <param name="online">The unique IGC address of the grid's modem</param>
    /// <param name="timestamp">The timestamp of the status</param>
    /// <remarks>Used for creating a GridStatusMessage for transmission</remarks>
    public GridStatusMessage(string name, Vector3 position, float range, Vector3 velocity, bool online, long timestamp)
    {
        if (name == null) {
            throw new ArgumentNullException(nameof(name));
        }
        Address = 0; // Address is not used for sender (TX)
        Name = name; 
        Position = position;
        Range = range;
        Velocity = velocity;
        Online = online;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Constructor for constructing GridStatusMessages from received data(RX).
    /// </summary>
    /// <param name="address">The unique IGC address of the sender</param>
    /// <param name="name">The name of the sender's grid</param>
    /// <param name="position">The current position of the grid in meters (antenna position)</param>
    /// <param name="range">The grid's broadcast range in meters</param>
    /// <param name="velocity">The current velocity of the grid in meters per second</param>
    /// <param name="online">Whether the grid is online or not</param>
    /// <param name="timestamp">The timestamp of the status</param>
    /// <remarks>Used for creating a GridStatusMessage from a received message</remarks>
    private GridStatusMessage(long address, string name, Vector3 position, float range, Vector3 velocity, bool online, long timestamp)
    {
        if (name == null) {
            throw new ArgumentNullException(nameof(name));
        }
        Address = address;
        Name = name;
        Position = position;
        Range = range;
        Velocity = velocity;
        Online = online;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Convert to ImmutableDictionary for transmission (TX).
    /// </summary>
    /// <returns>ImmutableDictionary with the grid status data</returns>
    public ImmutableDictionary<string, object> ToTransmittable()
    {
        return ImmutableDictionary<string, object>.Empty
            .Add("name", Name)
            .Add("position_x", Position.X)
            .Add("position_y", Position.Y)
            .Add("position_z", Position.Z)
            .Add("range", Range)
            .Add("velocity_x", Velocity.X)
            .Add("velocity_y", Velocity.Y)
            .Add("velocity_z", Velocity.Z)
            .Add("online", Online)
            .Add("timestamp", Timestamp);
    }

    /// <summary>
    /// Create from a MyIGCMessage received over IGC (RX).
    /// </summary>
    /// <param name="igcMessageData">a MyIGCMessage</param>
    public static GridStatusMessage FromTransmittable(MyIGCMessage igcMessage)
    {
        if (igcMessage == null) {
            throw new ArgumentNullException(nameof(igcMessage));
        }

        // Deserialize the message data
        var data = (ImmutableDictionary<string, object>)igcMessage.Data;
        if (data == null) {
            throw new ArgumentNullException(nameof(data));
        }

        return new GridStatusMessage(
            igcMessage.Source,
            (string)data["name"],
            new Vector3(
                Convert.ToSingle(data["position_x"]),
                Convert.ToSingle(data["position_y"]),
                Convert.ToSingle(data["position_z"])),
            Convert.ToSingle(data["range"]),
            new Vector3(
                Convert.ToSingle(data["velocity_x"]),
                Convert.ToSingle(data["velocity_y"]),
                Convert.ToSingle(data["velocity_z"])),
            Convert.ToBoolean(data["online"]),
            Convert.ToInt64(data["timestamp"])
        );
    }

    /// <summary>
    /// Override Equals for efficient value comparison. Objects are equal if all property values match.
    /// </summary>
    /// <param name="obj">The object to compare with</param>
    /// <returns>True if the objects are equal, otherwise false</returns>
    public override bool Equals(object obj)
    {
        return obj is GridStatusMessage other &&
                Address == other.Address &&
                Name == other.Name &&
                Position.Equals(other.Position) &&
                Range.Equals(other.Range) &&
                Velocity.Equals(other.Velocity) &&
                Online == other.Online &&
                Timestamp == other.Timestamp;
    }

    /// <summary>
    /// Override GetHashCode for efficient value comparison when used in collections
    /// </summary>
    /// <returns>Hash code for the GridStatusMessage</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Address, Name, Position, Range, Velocity, Timestamp);
    }

    /// <summary>
    /// Override ToString to provide a readable string representation of the object
    /// </summary>
    /// <returns>String representation of the GridStatusMessage</returns>
    public override string ToString()
    {
        return $"GridStatusMessage[Address={Address}, Name={Name}, Pos=({Position.X},{Position.Y},{Position.Z}), Range={Range}, " +
                $"Vel=({Velocity.X},{Velocity.Y},{Velocity.Z}), Online={Online}, Time={Timestamp}]";
    }
}

/// <summary>
/// class for storing and updating grid status information for grids on the GridNet.
/// </summary>
public class GridStatus
{
    public long Address { get; private set; }
    public string Name { get; private set; }
    public Vector3 Position { get; private set; }
    public float Range { get; private set; }
    public Vector3 Velocity { get; private set; }
    public bool Online { get; set; } // We'll need to be able to set this publicly
    public long LastUpdateTime { get; private set; }

    /// <summary>
    /// Constructor for creating a new GridStatus object.
    /// </summary>
    /// <param name="address">The unique IGC address of the grid's modem</param>
    /// <param name="name">The name of the grid</param>
    /// <param name="position">The current position of the grid in meters (antenna position)</param>
    /// <param name="range">The grid's broadcast range in meters</param>
    /// <param name="velocity">The current velocity of the grid in meters per second</param>
    /// <param name="online">Whether the grid is online or not</param>
    /// <param name="lastUpdateTime">The last update time of the grid status</param>
    public GridStatus(long address, string name, Vector3 position, float range, Vector3 velocity, bool online, long lastUpdateTime)
    {
        Address = address;
        Name = name;
        Position = position;
        Range = range;
        Velocity = velocity;
        Online = online;
        LastUpdateTime = lastUpdateTime;
    }

    /// <summary>
    /// Constructor for creating a new GridStatus object from a GridStatusMessage.
    /// </summary>
    /// <param name="message">The GridStatusMessage to create the GridStatus from</param>
    public GridStatus(GridStatusMessage message)
    {
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }
        Address = message.Address;
        Name = message.Name;
        Position = message.Position;
        Range = message.Range;
        Velocity = message.Velocity;
        Online = message.Online; // Assume online when created from message
        LastUpdateTime = message.Timestamp;
    }

    /// <summary>
    /// Update the grid status properties with new values.
    /// </summary>
    /// <param name="address">The unique IGC address of the grid's modem</param>
    /// <param name="name">The name of the grid</param>
    /// <param name="position">The current position of the grid in meters (antenna position)</param>
    /// <param name="range">The grid's broadcast range in meters</param>
    /// <param name="velocity">The current velocity of the grid in meters per second</param>
    /// <param name="online">Whether the grid is online or not</param>
    public void Update(long address, string name, Vector3 position, float range, Vector3 velocity, bool online)
    {
        Address = address;
        Name = name;
        Position = position;
        Range = range;
        Velocity = velocity;
        Online = online;
        LastUpdateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Update the grid status properties from a GridStatusMessage.
    /// </summary>
    /// <param name="message">The GridStatusMessage to update the GridStatus from</param>
    public void UpdateFromMessage(GridStatusMessage message)
    {
        if (message == null) {
            throw new ArgumentNullException(nameof(message));
        }
        Address = message.Address;
        Name = message.Name;
        Position = message.Position;
        Range = message.Range;
        Velocity = message.Velocity;
        Online = message.Online;
        LastUpdateTime = message.Timestamp;
    }

    /// <summary>
    /// Check if the grid status is stale based on the last update time and a timeout.
    /// </summary>
    /// <param name="timeout">The timeout in milliseconds</param>
    /// <returns>True if the grid status is stale, otherwise false</returns>
    public bool IsStale(long timeout)
    {
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (currentTime < LastUpdateTime) {
            throw new ArgumentOutOfRangeException(nameof(currentTime), "Current time cannot be less than last update time.");
        }
        return (currentTime - LastUpdateTime) > timeout;
    }

    /// <summary>
    /// Set the grid status to offline if we haven't heard from it in a while.
    /// </summary>
    public void UpdateIfStale()
    {
        if (IsStale(GRIDMAP_STATUS_INTERVAL_MS * GRIDMAP_STALE_MULTIPLIER))
        {
            Online = false;
        }
    }

    /// <summary>
    /// Convert the GridStatus object to a GridStatusMessage for transmission.
    /// </summary>
    /// <returns>A GridStatusMessage with the grid status data</returns>
    public GridStatusMessage ToMessage()
    {
        return new GridStatusMessage(Name, Position, Range, Velocity, LastUpdateTime);
    }
}

/// TODO: FINISH
public class GridMap
{
    private Dictionary<long, GridStatus> _map;

    public GridMap()
    {
        _map = new Dictionary<long, GridStatus>();
    }

    public void AddOrUpdate(GridStatusMessage statusMsg)
    {
        if (statusMsg == null) {
            throw new ArgumentNullException(nameof(status));
        }
        if (_map.ContainsKey(statusMsg.Address))
        {
            _map[statusMsg.Address].UpdateFromMessage(statusMsg);
        }
        else
        {
            _map[statusMsg.Address] = new GridStatus(statusMsg);
        }
    }

    // TODO: implement method for updating online status for all stale grid statuses
}



public abstract class GridStatusMessageHandler
{
    protected string _channelTag;
    protected IMyBroadcastListener _broadcastListener;
    protected IMyUnicastListener _unicastListener;
    protected GridMap _gridMap; // Store reference to the original GridMap
    protected GridStatus _myGridMapStatus;
    protected IMyIntergridCommunicationSystem _igc;
    protected readonly Func<bool> _hasPendingMessage;
    protected IEnumerator<bool> _currentStateMachine;
    
    /// <summary>
    /// Constructor for GridStatusMessage Handler.
    /// </summary>
    /// <param name="igc">The IGC system</param>
    /// <param name="channelTag">The IGC channel tag</param>
    /// <param name="gridMap">Reference to the GridMap being used</param>
    /// <param name="myGridMapStatus">The grid status of the local grid</param>
    /// <param name="unicast">Whether handler should use unicast. Defaults to broadcast.</param>
    protected GridStatusMessageHandler(IMyIntergridCommunicationSystem igc, string channelTag, GridMap gridMap, GridStatus myGridMapStatus, bool unicast=false)
    {
        _channelTag = channelTag;
        _gridMap = gridMap; // Store reference to the original GridMap
        _myGridMapStatus = myGridMapStatus;
        _igc = igc;
        _currentStateMachine = null;
        if (unicast)
        {
            _unicastListener = igc.UnicastListener;
            _unicastStatusChannel.SetMessageCallback(channelTag);
            _broadcastListener = null;
            _hasPendingMessage = () => _unicastListener.HasPendingMessage;
        }
        else{
            _broadcastListener = igc.RegisterBroadcastListener(channelTag);
            _broadcastListener.SetMessageCallback(channelTag);
            _unicastListener = null;
            _hasPendingMessage = () => _broadcastListener.HasPendingMessage;
        }
        
    }
    
    /// <summary>
    /// Abstract method that must be implemented by subclasses to handle messages.
    /// </summary>
    /// <param name="argument">The argument passed to the script main call (channel tag if IGC update)</param>
    /// <param name="updateSource">The source of the runtime update (e.g., IGC, Once)</param>
    /// <returns>True if the state machine has completed, otherwise false</returns>
    protected abstract IEnumerator<bool> _MessageHandler(string argument, UpdateType updateSource);

    /// <summary>
    /// Handle any new messages on the channel.
    /// </summary>
    /// <param name="argument">The argument passed to the script main call</param>
    /// <param name="updateSource">The source of the runtime update (e.g., IGC, Once)</param>
    /// <returns>True if the state machine has completed, otherwise false</returns>
    public bool HandleMessages(string argument, UpdateType updateSource)
    {
        // Check if the current state machine is null or has completed
        if (_currentStateMachine == null && _hasPendingMessage())
        {
            // Start a new state machine for message handling
            _currentStateMachine = _MessageHandler(argument, updateSource);
        }
        bool hasCompleted = _currentStateMachine.MoveNext();

        // If we've handled all messages in the queue
        if (hasCompleted)
        {
            // dispose of the state machine
            _currentStateMachine.Dispose();
            _currentStateMachine = null;
        } 
        // Return whether the state machine has completed
        return hasCompleted;
    }
}


/// <summary>
/// Handler for broadcast status messages from other grids.
/// </summary>
public class BroadcastStatusMessageHandler : GridStatusMessageHandler
{
    /// <summary>
    /// Constructor for BroadcastStatusMessageHandler.
    /// </summary>
    /// <param name="igc">The IGC system</param>
    /// <param name="channelTag">The IGC channel tag</param>
    /// <param name="gridMap">Reference to the GridMap being used</param>
    /// <param name="myGridMapStatus">The grid status of the local grid</param>
    public BroadcastStatusMessageHandler(IMyIntergridCommunicationSystem igc, string channelTag, GridMap gridMap, GridStatus myGridMapStatus)
        : base(igc, channelTag, gridMap)
    {
    }

    /// <summary>
    /// Process broadcast status messages from other grids.
    /// </summary>
    /// <param name="argument">The argument passed to the script main call</param>
    /// <param name="updateSource">The source of the runtime update</param>
    /// <returns>Iterator that yields false while processing and true when complete</returns>
    protected override IEnumerator<bool> _MessageHandler(string argument, UpdateType updateSource)
    {
        // Check if this message is for our tag
        if (((updateSource & UpdateType.IGC) > 0) && (argument != _channelTag))
        {
            yield return true;  // This message is not for us, so we're done
        }

        // Process all pending messages
        while (_hasPendingMessage())
        {
            // Get the message from the broadcast listener and update the grid map
            MyIGCMessage message = _broadcastListener.AcceptMessage();            
            _gridMap.AddOrUpdate(GridStatusMessage.FromTransmittable(message));            
            yield return false;  // Not done processing yet
        }

        yield return true;  // All messages processed
    }
}

/// <summary>
/// Handler for broadcast init messages from other grids.
/// </summary>
public class BroadcastInitMessageHandler : GridStatusMessageHandler
{
    /// <summary>
    /// Constructor for BroadcastInitMessageHandler.
    /// </summary>
    /// <param name="igc">The IGC system</param>
    /// <param name="channelTag">The IGC channel tag</param>
    /// <param name="gridMap">Reference to the GridMap being used</param>
    /// <param name="myGridMapStatus">The grid status of the local grid</param>
    public BroadcastInitMessageHandler(IMyIntergridCommunicationSystem igc, string channelTag, GridMap gridMap, GridStatus myGridMapStatus)
        : base(igc, channelTag, gridMap)
    {
    }

    /// <summary>
    /// Process broadcast init messages from other grids.
    /// </summary>
    /// <param name="argument">The argument passed to the script main call</param>
    /// <param name="updateSource">The source of the runtime update</param>
    /// <returns>Iterator that yields false while processing and true when complete</returns>
    protected override IEnumerator<bool> _MessageHandler(string argument, UpdateType updateSource)
    {
        // Check if this message is for our tag
        if (((updateSource & UpdateType.IGC) > 0) && (argument != _channelTag))
        {
            yield return true;  // This message is not for us, so we're done
        }

        // Process all pending messages
        while (_hasPendingMessage())
        {
            // Get the message from the broadcast listener and update the grid map
            MyIGCMessage message = _broadcastListener.AcceptMessage();
            _gridMap.AddOrUpdate(GridStatusMessage.FromTransmittable(message));
            // Get updated grid status and send unicast message to the sender
            GridStatusMessage myStatus = _myGridMapStatus.ToMessage();
            _igc.SendUnicastMessage(message.Source, GRIDMAP_IGC_INIT_TAG, myStatus.ToTransmittable());
            yield return false;  // Not done processing yet
        }

        yield return true;  // All messages processed
    }
}

/// <summary>
/// Handler for unicast status messages from other grids.
/// </summary>
public class UnicastStatusMessageHandler : GridStatusMessageHandler
{
    /// <summary>
    /// Constructor for UnicastStatusMessageHandler.
    /// </summary>
    /// <param name="igc">The IGC system</param>
    /// <param name="channelTag">The IGC channel tag</param>
    /// <param name="gridMap">Reference to the GridMap being used</param>
    /// <param name="myGridMapStatus">The grid status of the local grid</param>
    public UnicastStatusMessageHandler(IMyIntergridCommunicationSystem igc, string channelTag, GridMap gridMap, GridStatus myGridMapStatus)
        : base(igc, channelTag, gridMap, myGridMapStatus, true)
    {
    }

    /// <summary>
    /// Process unicast status messages from other grids.
    /// </summary>
    /// <param name="argument">The argument passed to the script main call</param>
    /// <param name="updateSource">The source of the runtime update</param>
    /// <returns>Iterator that yields false while processing and true when complete</returns>
    protected override IEnumerator<bool> _MessageHandler(string argument, UpdateType updateSource)
    {
        // Check if this message is for our tag
        if (((updateSource & UpdateType.IGC) > 0) && (argument != _channelTag))
        {
            yield return true;  // This message is not for us, so we're done
        }

        // Process all pending messages
        while (_hasPendingMessage())
        {
            // Get the message from the unicast listener and update the grid map
            MyIGCMessage message = _unicastListener.AcceptMessage();
            _gridMap.AddOrUpdate(GridStatusMessage.FromTransmittable(message));
            yield return false;  // Not done processing yet
        }

        yield return true;  // All messages processed
    }
}

/// TODO: FINISH
public class GridMapper
{
    /* Private fields */
    MyGridProgram _gridProgram;
    private IMyCubeGrid _grid;
    private IMyRadioAntenna _antenna;
    private GridMap _gridMap;
    private GridStatus _myGridStatus;
    private BroadcastStatusMessageHandler _broadcastStatusHandler;
    private BroadcastInitMessageHandler _broadcastInitHandler;
    private UnicastStatusMessageHandler _unicastStatusHandler;

    /// <summary>
    /// Constructor for the GridMapper class.
    /// </summary>
    /// <param name="gridProgram">reference to the main program instance</param>
    /// <param name="antenna">The antenna used by the grid for GridNet connectivity</param>
    public GridMapper(MyGridProgram gridProgram, IMyRadioAntenna antenna)
    {
        _gridProgram = gridProgram;
        _antenna = antenna;
        // initialize the grid mapper
        Initialize();
    }

    /// <summary>
    /// Initialize the grid mapper by setting up the grid map and IGC listeners.
    /// </summary>
    /// <remarks>Called in the constructor</remarks>
    private void Initialize()
    {
        // Get the grid this programmable block is on
        _grid = _gridProgram.Me.CubeGrid;

        // Initialize the grid map
        _gridMap = new GridMap();

        // Initialize broadcast message handlers
        _broadcastStatusHandler = new BroadcastStatusMessageHandler(_gridProgram.IGC, GRIDMAP_IGC_STATUS_TAG, _gridMap, _myGridStatus);
        _broadcastInitHandler = new BroadcastInitMessageHandler(_gridProgram.IGC, GRIDMAP_IGC_INIT_TAG, _gridMap, _myGridStatus);
        // Initialize unicast message handlers
        _unicastStatusHandler = new UnicastStatusMessageHandler(_gridProgram.IGC, GRIDMAP_IGC_STATUS_TAG, _gridMap, _myGridStatus, true);

        // Initialize the grid status
        _myGridStatus = new GridStatus();
        UpdateGridStatus();

        // Is there anybody out there?
        BroadcastStatus(true);

        /// set status update frequency.
        _gridProgram.Runtime.UpdateFrequency |= GRIDMAP_STATUS_UPDATE_TYPE;
    }

    /// <summary>
    /// Update the grid status with the current position, range, and velocity.
    /// </summary>
    private void UpdateGridStatus()
    {
        // Get the current position of the antenna
        Vector3 position = _antenna.GetPosition();
        // Get the current range of the antenna
        float range = _antenna.Radius;
        // Get the current velocity of the antenna
        Vector3 velocity = _antenna.GetShipVelocities().LinearVelocity;

        // Create a new GridStatusMessage with the current status
        _myGridStatus.Update(
            _gridProgram.IGC.Me,
            _grid.CustomName,
            _antenna.GetPosition(),
            _antenna.Radius,
            _grid.LinearVelocity,
            true
        );
    }

    /// <summary>
    /// Broadcast the grid status message to other grids on the GridNet.
    /// </summary>
    /// <param name="init">Whether this is an initial broadcast or a status update</param>
    private void BroadcastStatus(bool init = false)
    {
        if (init) {
            string channel = GRIDMAP_IGC_INIT_TAG;
        }
        else {
            string channel = GRIDMAP_IGC_STATUS_TAG;
        }
        // Send the grid status message to the broadcast channel
        GridStatusMessage myStatus = _myGridStatus.ToMessage();
        _gridProgram.IGC.SendBroadcastMessage(channel, myStatus.ToTransmittable());
    }

    /// <summary>
    /// Process incoming grid status messages and update the grid map.
    /// </summary>
    /// <param name="argument">The argument passed to the script main call</param>
    /// <param name="updateSource">The source of the runtime update (e.g., IGC, Once)</param>
    private void ServiceGridStatusMessages(string argument, UpdateType updateSource)
    {
        bool doneProcessing = false;
        doneProcessing = doneProcessing || _broadcastStatusHandler.HandleMessages(argument, updateSource);
        doneProcessing = doneProcessing || _broadcastInitHandler.HandleMessages(argument, updateSource);
        doneProcessing = doneProcessing || _unicastStatusHandler.HandleMessages(argument, updateSource);
        if (!doneProcessing)
        {
            // tell runtime to update again
            _gridProgram.Runtime.UpdateFrequency |= GRIDMAP_CONTINUE_UPDATE_TYPE;
        }
        return;
    }

    /// <summary>
    /// Check if the grid status is stale and update the grid status if necessary and notify other grids.
    /// </summary>
    private void ServiceMyGridStatus()
    {        
        if (_myGridStatus.IsStale(GRIDMAP_STATUS_INTERVAL_MS))
        {
            // Update the grid status
            UpdateGridStatus();
            // Send the grid status message to the broadcast channel
            BroadcastStatus();
        }
        return;
    }

    /// <summary>
    /// Check if a runtime update requires servicing the grid map and perform any necessary tasks.
    /// </summary>
    /// <param name="argument">The argument passed to the script main call</param>
    /// <param name="updateSource">The source of the runtime update (e.g., unicast, broadcast)</param>
    public void ServiceRuntimeUpdates(string argument, UpdateType updateSource)
    {
        // Service IGC related updates
        if ((updateSource & GRIDMAP_IGC_UPDATE_TYPES) > 0)
        {
            // Process (or continue) incoming grid status messages
            ServiceGridStatusMessages(argument);
        }
        // Service recurring updates
        else if ((updateSource & GRIDMAP_RECURRING_UPDATE_TYPES) > 0)
        {
            if ((updateSource & GRIDMAP_STATUS_UPDATE_TYPE) > 0)
            {
                // Notify other grids of my status as appropriate
                ServiceMyGridStatus();
            }
            // other specific recurring updates can be handled here
            // ...
        }
    }
}