        //$root: ../$// 
        
        //$requires: GridDNS/GridDNS.cs$//

        private const string GRIDNET_NAME_TAG = "[GRIDNET:ANTENNA]";
        /// <summary>
        /// Returns first GridNet antenna on the grid with broadcasting enabled.
        /// </summary>
        /// <param name="program">The MyGridProgram instance</param>
        /// <returns>The first found antenna or null if none found</returns>
        private IMyRadioAntenna FindAntenna(MyGridProgram program)
        {
            IMyCubeGrid grid = program.Me.CubeGrid;
            var antennas = new List<IMyRadioAntenna>();
            program.GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(
                antennas, 
                antenna => antenna.CubeGrid == grid 
                    && antenna.CustomName.Contains(GRIDNET_NAME_TAG) 
                    && antenna.EnableBroadcasting);
            if (antennas.Count > 0)
            {
                return antennas[0];
            }
            program.Echo("No antenna found on the grid.");
            return null;
        }

        private GridDNS gridDNS;

        public Program() {
            // Initialize the grid dns with the first found antenna
            IMyRadioAntenna antenna = FindAntenna(this);
            if (antenna != null)
            {
                gridDNS = new GridDNS(this, antenna);
            }
            else
            {
                throw new Exception("No GridNet antenna found on the grid.");
            }
        }

        public void Main(string args, UpdateType updateSource) {
            // If this is a triggered update, check and handle args
            if ((updateSource & GRIDMAP_TRIGGERED_UPDATE_TYPES) > 0) {
                // If args is the string "--summary", print a summary of the grid map
                switch (args)
                {
                    case "--summary":
                        // Print a summary of the grid map
                        Echo("Grid Map Summary:");
                        foreach (var status in gridDNS.gridMap.Values)
                        {
                            Echo($"  {status.Name}: {(status.Online ? "Online" : "Offline")}");
                        }
                        break;
                    default:
                        Echo($"Unknown argument: {args}");
                        break;
                }
                return;
            }

            // Check if the grid dns is initialized
            if (gridDNS != null)
            {
                // Service the grid dns with the current update type
                gridDNS.ServiceRuntimeUpdates(args, updateSource);
            }
            else
            {
                Echo("Grid dns not initialized.");
            }
        }