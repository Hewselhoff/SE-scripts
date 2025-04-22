// GridMap Constants
public const string GRIDMAP_IGC_STATUS_TAG = "GRIDMAP:STATUS";
public const string GRIDMAP_IGC_INIT_TAG = "GRIDMAP:INIT";
// TODO: better handling for this timing.
public const long GRIDMAP_STATUS_INTERVAL_MS = 1000; // 1 second
public const long GRIDMAP_STALE_MULTIPLIER  = 4; // 4 x times the status interval
public const UpdateType GRIDMAP_STATUS_UPDATE_TYPE = UpdateType.Update100;


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
    // public ImmutableDictionary<long, string> RemovedBlocks { get; }
    // public ImmutableDictionary<long, string> AddedBlocks { get; }
    // public ImmutableDictionary<long, string> ChangedBlocks { get; }
    public long Timestamp { get; }

    /// <summary>
    /// Constructor for constructing new GridStatusMessages (TX).
    /// </summary>
    /// <param name="name">The name of the grid</param>
    /// <param name="position">The current position of the grid in meters (antenna position)</param>
    /// <param name="range">The grid's broadcast range in meters</param>
    /// <param name="velocity">The current velocity of the grid in meters per second</param>
    /// <param name="timestamp">The timestamp of the status</param>
    public GridStatusMessage(string name, Vector3 position, float range, Vector3 velocity, long timestamp)
    {
        if (name == null) {
            throw new ArgumentNullException(nameof(name));
        }
        Address = 0; // Address is not used for sender (TX)
        Name = name; 
        Position = position;
        Range = range;
        Velocity = velocity;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Constructor for re-constructing GridStatusMessages from received MyIGCMessages(RX).
    /// </summary>
    /// <param name="igcMessageData">the data object from a MyIGCMessage</param>
    private GridStatusMessage(long address, string name, Vector3 position, float range, Vector3 velocity, long timestamp)
    {
        if (name == null) {
            throw new ArgumentNullException(nameof(name));
        }
        Address = address;
        Name = name;
        Position = position;
        Range = range;
        Velocity = velocity;
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
            .Add("timestamp", Timestamp);
    }

    /// <summary>
    // Create from a MyIGCMessage received over IGC (RX).
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
                $"Vel=({Velocity.X},{Velocity.Y},{Velocity.Z}), Time={Timestamp}]";
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
    public long LastUpdateTime { get; private set; }

    /// <summary>
    /// Constructor for creating a new GridStatus object.
    /// </summary>
    /// <param name="address">The unique IGC address of the grid's modem</param>
    /// <param name="name">The name of the grid</param>
    /// <param name="position">The current position of the grid in meters (antenna position)</param>
    /// <param name="range">The grid's broadcast range in meters</param>
    /// <param name="velocity">The current velocity of the grid in meters per second</param>
    /// <param name="lastUpdateTime">The last update time of the grid status</param>
    public GridStatus(long address, string name, Vector3 position, float range, Vector3 velocity, long lastUpdateTime)
    {
        Address = address;
        Name = name;
        Position = position;
        Range = range;
        Velocity = velocity;
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
        LastUpdateTime = message.Timestamp;
    }

    public Update(long Address, string name, Vector3 position, float range, Vector3 velocity)
    {
        Address = address;
        Name = name;
        Position = position;
        Range = range;
        Velocity = velocity;
        LastUpdateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

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
        LastUpdateTime = message.Timestamp;
    }

    public GridStatusMessage ToMessage()
    {
        return new GridStatusMessage(Name, Position, Range, Velocity, LastUpdateTime);
    }

    public bool IsStale(long timeout)
    {
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (currentTime < LastUpdateTime) {
            throw new ArgumentOutOfRangeException(nameof(currentTime), "Current time cannot be less than last update time.");
        }
        return (currentTime - LastUpdateTime) > timeout;
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

    public void AddOrUpdate(long address, GridStatusMessage statusMsg)
    {
        if (statusMsg == null) {
            throw new ArgumentNullException(nameof(status));
        }
        if (_map.ContainsKey(address))
        {
            _map[address].UpdateFromMessage(statusMsg);
        }
        else
        {
            _map[address] = new GridStatus(statusMsg);
        }
    }
}

/// TODO: FINISH
public class GridMapper
{
    /* Runtime Update Categories */
    /// <summary>
    /// The combined set of UpdateTypes that count as a 'triggered' update
    /// </summary>
    private UpdateType _triggeredUpdates = UpdateType.Terminal | UpdateType.Trigger | UpdateType.Mod | UpdateType.Script;

    /// <summary>
    /// the combined set of UpdateTypes and count as a 'recurring' update
    /// </summary>
    private UpdateType _recurringUpdates = UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100 | UpdateType.Once;

    /// <summary>
    /// The combined set of updates that count as a 'IGC' updates (or IGC state machine updates)
    /// </summary>
    private UpdateType _igcUpdates = UpdateType.IGC | UpdateType.Once;

    /* Private fields */
    MyGridProgram _gridProgram;
    private IMyCubeGrid _grid;
    private IMyRadioAntenna _antenna;
    private GridMap _gridMap;
    private GridStatus _myGridStatus;
    private IMyUnicastListener _unicastStatusChannel;
    private IMyBroadcastListener _broadcastStatusChannel;
    private IMyBroadcastListener _broadcastInitChannel;

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

    private void Initialize()
    {
        // Get the grid this programmable block is on
        _grid = _gridProgram.Me.CubeGrid;

        // Initialize the grid map
        _gridMap = new GridMap();

        // Initialize the grid status
        _myGridStatus = new GridStatus();
        UpdateGridStatus();

        // Initialize the IGC broadcast listeners
        _broadcastStatusChannel = _gridProgram.IGC.RegisterBroadcastListener(GRIDMAP_IGC_STATUS_TAG);
        _broadcastStatusChannel.SetMessageCallback(GRIDMAP_IGC_STATUS_TAG);
        _broadcastInitChannel = _gridProgram.IGC.RegisterBroadcastListener(GRIDMAP_IGC_INIT_TAG);
        _broadcastInitChannel.SetMessageCallback(GRIDMAP_IGC_INIT_TAG);

        // Initialize the IGC unicast listener
        _unicastStatusChannel = _gridProgram.IGC.UnicastListener;
        _unicastStatusChannel.SetMessageCallback(GRIDMAP_IGC_STATUS_TAG);

        // Is there anybody out there?
        GridStatusMessage myStatus = _myGridStatus.ToMessage();
        // Send the grid status message to the init broadcast channel
        _gridProgram.IGC.SendBroadcastMessage(GRIDMAP_IGC_INIT_TAG, myStatus.ToTransmittable());

        /// set status update frequency.
        !!!!!
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
            _grid.LinearVelocity
        );
    }

    public void ServiceGridStatusMessages()
    {
        // TODO: Implement the logic to process incoming grid status messages
        // if possible, use a state machine with yield to spread processing over 
        // multiple ticks to protect against timing or instruction overflow
        // if status traffic becomes high.
        return;
    }

    public void ServiceMyGridStatus()
    {        
        if (_myGridStatus.IsStale(GRIDMAP_STATUS_INTERVAL_MS))
        {
            // Send the grid status message to the broadcast channel
            GridStatusMessage myStatus = _myGridStatus.ToMessage();
            _gridProgram.IGC.SendBroadcastMessage(GRIDMAP_IGC_STATUS_TAG, myStatus.ToTransmittable());
        }
        return;
    }

    /// <summary>
    /// Check if a runtime update requires servicing the grid map and perform any necessary tasks.
    /// </summary>
    /// <param name="argument">The argument passed to the script main call</param>
    /// <param name="updateSource">The source of the runtime update (e.g., unicast, broadcast)</param>
    public void ServiceRuntimeUpdates(string Argument, UpdateType updateSource)
    {
        // Service IGC related updates
        if ((updateSource & _igcUpdates) > 0)
        {
            // Process (or continue) incoming grid status messages
            ServiceGridStatusMessages();
        }
        // Service recurring updates
        else if ((updateSource & _recurringUpdates) > 0)
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