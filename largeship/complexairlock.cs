//@ commons eventdriver
public class ComplexAirlock
{
    public class OpenQueueEntry
    {
        public int DesiredState { get; private set; }
        public HashSet<IMyDoor> Doors { get; private set; }

        public OpenQueueEntry(int desiredState, HashSet<IMyDoor> doors)
        {
            DesiredState = desiredState;
            Doors = doors;
        }
    }

    private const double RunDelay = 1.0;

    // Enums broken?
    private const int AIRLOCK_STATE_VACUUM = 0;
    private const int AIRLOCK_STATE_PRESSURIZED = 1;
    private const int AIRLOCK_STATE_UNKNOWN = -1;

    private readonly List<ZACommons.BlockGroup> rooms = new List<ZACommons.BlockGroup>();
    private readonly Dictionary<string, ZACommons.BlockGroup> roomsMap = new Dictionary<string, ZACommons.BlockGroup>();

    private readonly HashSet<IMyDoor> innerDoors = new HashSet<IMyDoor>();
    private readonly HashSet<IMyDoor> spaceDoors = new HashSet<IMyDoor>();

    private readonly Dictionary<string, ZACommons.BlockGroup> doorVentGroups = new Dictionary<string, ZACommons.BlockGroup>();
    private readonly Dictionary<IMyDoor, ZACommons.BlockGroup> doorVentRooms = new Dictionary<IMyDoor, ZACommons.BlockGroup>(); // Reverse mapping of rooms
    private readonly Dictionary<IMyDoor, List<IMyAirVent>> doorVentMap = new Dictionary<IMyDoor, List<IMyAirVent>>();

    private readonly Dictionary<string, OpenQueueEntry> openQueue = new Dictionary<string, OpenQueueEntry>();

    private void Init(ZACommons commons)
    {
        var groups = commons.GetBlockGroupsWithPrefix("Airlock");
        // Classify each group
        for (var e = groups.GetEnumerator(); e.MoveNext();)
        {
            var group = e.Current;
            if (string.Equals("AirlockDoorInner", group.Name, ZACommons.IGNORE_CASE))
            {
                innerDoors.UnionWith(ToDoors(ZACommons.GetBlocksOfType<IMyDoor>(group.Blocks)));
            }
            else if (string.Equals("AirlockDoorSpace", group.Name, ZACommons.IGNORE_CASE))
            {
                spaceDoors.UnionWith(ToDoors(ZACommons.GetBlocksOfType<IMyDoor>(group.Blocks)));
            }
            else if (group.Name.StartsWith("AirlockDoor", ZACommons.IGNORE_CASE))
            {
                doorVentGroups.Add(group.Name, group);

                var vents = ToVents(ZACommons.GetBlocksOfType<IMyAirVent>(group.Blocks));
                var doors = ToDoors(ZACommons.GetBlocksOfType<IMyDoor>(group.Blocks));
                for (var f = doors.GetEnumerator(); f.MoveNext();)
                {
                    doorVentMap.Add(f.Current, vents);
                }
            }
            else
            {
                rooms.Add(group);

                roomsMap.Add(group.Name, group);

                var doors = ToDoors(ZACommons.GetBlocksOfType<IMyDoor>(group.Blocks));
                for (var f = doors.GetEnumerator(); f.MoveNext();)
                {
                    doorVentRooms.Add(f.Current, group);
                }
            }
        }
    }

    private void Clear()
    {
        rooms.Clear();
        roomsMap.Clear();
        innerDoors.Clear();
        spaceDoors.Clear();
        doorVentGroups.Clear();
        doorVentRooms.Clear();
        doorVentMap.Clear();
    }

    private int GetAirlockState(List<IMyAirVent> vents)
    {
        if (vents.Count == 0) return AIRLOCK_STATE_UNKNOWN;

        float level = 0.0f;
        for (var e = vents.GetEnumerator(); e.MoveNext();)
        {
            level += e.Current.GetOxygenLevel();
        }
        level /= vents.Count;

        if (level == 0.0f) return AIRLOCK_STATE_VACUUM;
        else if (level > 0.5f) return AIRLOCK_STATE_PRESSURIZED;
        else { return AIRLOCK_STATE_UNKNOWN; }
    }

    private void DepressurizeVents(IEnumerable<IMyAirVent> vents, bool depressurize)
    {
        var e = vents.GetEnumerator();
        while (e.MoveNext())
        {
            var vent = e.Current;
            vent.SetValue<bool>("Depressurize", depressurize);
        }
    }

    private void OpenCloseDoors(IEnumerable<IMyDoor> doors, bool open)
    {
        var e = doors.GetEnumerator();
        while (e.MoveNext())
        {
            var door = e.Current;
            door.SetValue<bool>("Open", open);
        }
    }

    private void ChangeRoomState(string roomName,
                                 List<IMyAirVent> vents, List<IMyDoor> doors,
                                 int current, int target,
                                 IEnumerable<IMyDoor> targetDoors = null)
    {
        if (target != current && target != AIRLOCK_STATE_UNKNOWN)
        {
            OpenCloseDoors(doors, false);
            DepressurizeVents(vents, target == AIRLOCK_STATE_VACUUM);

        }

        // Open doors regardless
        var entry = new OpenQueueEntry(target,
                                       targetDoors != null ?
                                       new HashSet<IMyDoor>(targetDoors) :
                                       new HashSet<IMyDoor>());
        openQueue[roomName] = entry;
    }

    private void HandleCommandInternal(string argument)
    {
        var parts = argument.Split(new char[] { ' ' }, 2);
        if (parts.Length != 2) return;
        var command = parts[0];
        argument = parts[1].Trim();

        if (command == "inner" || command == "space" || command == "toggle")
        {
            ZACommons.BlockGroup room;
            if (roomsMap.TryGetValue(argument, out room))
            {
                var vents = ToVents(ZACommons.GetBlocksOfType<IMyAirVent>(room.Blocks));
                var current = GetAirlockState(vents);
                int target = AIRLOCK_STATE_UNKNOWN;
                switch (command)
                {
                    case "space":
                        target = AIRLOCK_STATE_VACUUM;
                        break;
                    case "inner":
                        target = AIRLOCK_STATE_PRESSURIZED;
                        break;
                    case "toggle":
                        target = current == AIRLOCK_STATE_PRESSURIZED ?
                            AIRLOCK_STATE_VACUUM : AIRLOCK_STATE_PRESSURIZED;
                        break;
                }

                ChangeRoomState(room.Name,
                                vents, ToDoors(ZACommons.GetBlocksOfType<IMyDoor>(room.Blocks)),
                                current, target, null);
            }
        }
        else if (command == "open")
        {
            // Find named group
            ZACommons.BlockGroup group;
            if (doorVentGroups.TryGetValue(argument, out group))
            {
                var doors = ToDoors(ZACommons.GetBlocksOfType<IMyDoor>(group.Blocks));
                // Only need to do one (assumes door groups were set up correctly... heh)
                if (doors.Count > 0)
                {
                    var door = doors[0];
                    ZACommons.BlockGroup room;
                    if (doorVentRooms.TryGetValue(door, out room))
                    {
                        var otherVents = ToVents(ZACommons.GetBlocksOfType<IMyAirVent>(group.Blocks));
                        var roomVents = ToVents(ZACommons.GetBlocksOfType<IMyAirVent>(room.Blocks));
                        var target = GetAirlockState(otherVents);
                        var current = GetAirlockState(roomVents);

                        ChangeRoomState(room.Name,
                                        roomVents,
                                        ToDoors(ZACommons.GetBlocksOfType<IMyDoor>(room.Blocks)),
                                        current, target, doors);
                    }
                }
            }
        }
    }

    private void CloseDoorsAsNeeded(EventDriver eventDriver,
                                    ZACommons.BlockGroup room, List<IMyDoor> doors,
                                    HashSet<IMyDoor> targetDoors,
                                    int checkState)
    {
        var openDoors = new HashSet<IMyDoor>();
        OpenQueueEntry entry;
        if (openQueue.TryGetValue(room.Name, out entry))
        {
            if (entry.DesiredState == checkState)
            {
                openQueue.Remove(room.Name);
                openDoors = entry.Doors;
                if (openDoors.Count == 0)
                {
                    openDoors = new HashSet<IMyDoor>(targetDoors); // NB copy
                }

                // Limit to just doors in this room
                openDoors.IntersectWith(doors);
            }
        }

        // Close and lock all doors with different pressure
        for (var f = doors.GetEnumerator(); f.MoveNext();)
        {
            var door = f.Current;

            int otherState;
            List<IMyAirVent> otherVents;
            if (doorVentMap.TryGetValue(door, out otherVents))
            {
                otherState = GetAirlockState(otherVents);
            }
            else { otherState = AIRLOCK_STATE_UNKNOWN; }

            if (targetDoors.Contains(door) || otherState == checkState)
            {
                // Unlock
                if (!door.Enabled)
                {
                    door.SetValue<bool>("OnOff", true);
                }
            }
            else
            {
                // Close & lock all others
                if (door.Open)
                {
                    door.SetValue<bool>("Open", false);
                }
                else if (door.OpenRatio == 0.0f && door.Enabled)
                {
                    door.SetValue<bool>("OnOff", false);
                }
            }
        }

        // Open all required doors after some delay
        if (openDoors.Count > 0)
        {
            eventDriver.Schedule(2.5, (p, e) =>
                    {
                        for (var f = openDoors.GetEnumerator(); f.MoveNext();)
                        {
                            var door = f.Current;
                            door.SetValue<bool>("Open", true);
                        }
                    });
        }
    }

    private void OpenCloseDoorsAsNeeded(EventDriver eventDriver)
    {
        for (var e = rooms.GetEnumerator(); e.MoveNext();)
        {
            var room = e.Current;
            var vents = ToVents(ZACommons.GetBlocksOfType<IMyAirVent>(room.Blocks));
            if (vents.Count == 0) continue;

            var doors = ToDoors(ZACommons.GetBlocksOfType<IMyDoor>(room.Blocks));
            if (doors.Count == 0) continue;

            // Determine room state
            var state = GetAirlockState(vents);

            switch (state)
            {
                case AIRLOCK_STATE_VACUUM:
                    // Close and lock all but space doors
                    CloseDoorsAsNeeded(eventDriver, room, doors, spaceDoors,
                                       AIRLOCK_STATE_VACUUM);
                    break;
                case AIRLOCK_STATE_PRESSURIZED:
                    // Close and lock all but inner doors
                    CloseDoorsAsNeeded(eventDriver, room, doors, innerDoors,
                                       AIRLOCK_STATE_PRESSURIZED);
                    break;
                case AIRLOCK_STATE_UNKNOWN:
                    // Close and lock all doors
                    for (var f = doors.GetEnumerator(); f.MoveNext();)
                    {
                        var door = f.Current;
                        door.SetValue<bool>("Open", false);
                        if (door.OpenRatio == 0.0f && door.Enabled)
                        {
                            door.SetValue<bool>("OnOff", false);
                        }
                    }
                    break;
            }
        }
    }

    private void RunInternal(ZACommons commons, EventDriver eventDriver, string argument)
    {
        Init(commons);

        if (!string.IsNullOrWhiteSpace(argument))
        {
            HandleCommandInternal(argument);
        }

        OpenCloseDoorsAsNeeded(eventDriver);

        Clear();
    }

    public void Init(ZACommons commons, EventDriver eventDriver)
    {
        eventDriver.Schedule(0.0, Run);
    }

    public void Run(ZACommons commons, EventDriver eventDriver)
    {
        RunInternal(commons, eventDriver, ""); // Why...
        eventDriver.Schedule(RunDelay, Run);
    }

    public void HandleCommand(ZACommons commons, EventDriver eventDriver, string argument)
    {
        RunInternal(commons, eventDriver, argument); // Why...
    }

    private List<IMyDoor> ToDoors(IEnumerable<IMyTerminalBlock> blocks)
    {
        var result = new List<IMyDoor>();
        for (var e = blocks.GetEnumerator(); e.MoveNext();)
        {
            var door = e.Current as IMyDoor;
            if (door != null) result.Add(door);
        }
        return result;
    }

    private List<IMyAirVent> ToVents(IEnumerable<IMyTerminalBlock> blocks)
    {
        var result = new List<IMyAirVent>();
        for (var e = blocks.GetEnumerator(); e.MoveNext();)
        {
            var vent = e.Current as IMyAirVent;
            if (vent != null) result.Add(vent);
        }
        return result;
    }
}
