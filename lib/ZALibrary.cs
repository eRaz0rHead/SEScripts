// ZALibrary v1.4.0
public static class ZALibrary
{
    public const StringComparison IGNORE_CASE = StringComparison.CurrentCultureIgnoreCase;

    public class Ship
    {
        public List<IMyTerminalBlock> Blocks { get; private set; }

        public Ship(List<IMyTerminalBlock> blocks)
        {
            Blocks = blocks;
        }

        public Ship(MyGridProgram program, string groupName)
        {
            var group = groupName != null ?
                ZALibrary.GetBlockGroupWithName(program, groupName) : null;
            if (group != null)
            {
                // Use the named group's blocks
                Blocks = group.Blocks;
            }
            else
            {
                // Use all blocks on the same grid as this script's programmable block
                initSameGrid(program);
            }
        }

        public Ship(MyGridProgram program)
        {
            initSameGrid(program);
        }

        private void initSameGrid(MyGridProgram program)
        {
            Blocks = new List<IMyTerminalBlock>();
            program.GridTerminalSystem
                .GetBlocksOfType<IMyTerminalBlock>(Blocks,
                                                   block => block.CubeGrid == program.Me.CubeGrid);
        }

        public List<T> GetBlocksOfType<T>(Func<T, bool> collect = null)
        {
            return ZALibrary.GetBlocksOfType<T>(Blocks, collect);
        }

        public T GetBlockWithName<T>(string name)
            where T : IMyTerminalBlock
        {
            return ZALibrary.GetBlockWithName<T>(Blocks, name);
        }

        public bool StartTimerBlockWithName(string name, Func<IMyTimerBlock, bool> condition = null)
        {
            return ZALibrary.StartTimerBlockWithName(Blocks, name, condition);
        }

        public bool IsConnectedAnywhere(Func<IMyShipConnector, bool> collect = null)
        {
            var connectors = GetBlocksOfType<IMyShipConnector>(collect);
            return ZALibrary.IsConnectedAnywhere(connectors);
        }

        public void ForEachBlockOfType<T>(Action<T> action)
        {
            ZALibrary.ForEachBlockOfType<T>(Blocks, action);
        }
    }

    public static IMyBlockGroup GetBlockGroupWithName(MyGridProgram program, string name)
    {
        var groups = new List<IMyBlockGroup>();
        program.GridTerminalSystem.GetBlockGroups(groups);

        for (var e = groups.GetEnumerator(); e.MoveNext();)
        {
            var group = e.Current;
            if (group.Name.Equals(name, IGNORE_CASE)) return group;
        }
        return null;
    }

    public static List<IMyBlockGroup> GetBlockGroupsWithPrefix(MyGridProgram program, string prefix)
    {
        var groups = new List<IMyBlockGroup>();
        program.GridTerminalSystem.GetBlockGroups(groups);

        var result = new List<IMyBlockGroup>();

        for (var e = groups.GetEnumerator(); e.MoveNext();)
        {
            var group = e.Current;
            if (group.Name.StartsWith(prefix, IGNORE_CASE)) result.Add(group);
        }
        return result;
    }

    public static List<T> GetBlocksOfType<T>(IEnumerable<IMyTerminalBlock> blocks,
                                             Func<T, bool> collect = null)
    {
        List<T> list = new List<T>();
        for (var e = blocks.GetEnumerator(); e.MoveNext();)
        {
            var block = e.Current;
            if (block is T && (collect == null || collect((T)block))) list.Add((T)block);
        }
        return list;
    }

    public static T GetBlockWithName<T>(IEnumerable<IMyTerminalBlock> blocks, string name)
        where T : IMyTerminalBlock
    {
        for (var e = blocks.GetEnumerator(); e.MoveNext();)
        {
            var block = e.Current;
            if(block is T && block.CustomName.Equals(name, IGNORE_CASE)) return (T)block;
        }
        return default(T);
    }

    public static void EnableBlocks(IEnumerable<IMyTerminalBlock> blocks, bool enabled)
    {
        for (var e = blocks.GetEnumerator(); e.MoveNext();)
        {
            var block = e.Current;
            // Not all blocks will implement IMyFunctionalBlock, so can't checked Enabled
            block.GetActionWithName(enabled ? "OnOff_On" : "OnOff_Off").Apply(block);
        }
    }

    public static bool IsBatteryRecharging(IMyBatteryBlock battery)
    {
        return !battery.ProductionEnabled;
    }

    public static void SetBatteryRecharge(IMyBatteryBlock battery, bool recharge)
    {
        var recharging = IsBatteryRecharging(battery);
        if ((recharging && !recharge) || (!recharging && recharge))
        {
            battery.GetActionWithName("Recharge").Apply(battery);
        }
    }

    public static void SetBatteryRecharge(IEnumerable<IMyBatteryBlock> batteries, bool recharge)
    {
        for (var e = batteries.GetEnumerator(); e.MoveNext();)
        {
            var battery = e.Current;
            SetBatteryRecharge(battery, recharge);
        }
    }

    public static string FormatPower(float value)
    {
        if (value >= 1.0f)
        {
            return string.Format("{0:F2} MW", value);
        }
        else if (value >= 0.001)
        {
            return string.Format("{0:F2} kW", value * 1000f);
        }
        else
        {
            return string.Format("{0:F2} W", value * 1000000f);
        }
    }

    public static bool StartTimerBlockWithName(IEnumerable<IMyTerminalBlock> blocks, string name,
                                               Func<IMyTimerBlock, bool> condition = null)
    {
        var timer = GetBlockWithName<IMyTimerBlock>(blocks, name);
        if (timer != null && timer.Enabled && !timer.IsCountingDown &&
            (condition == null || condition(timer)))
        {
            timer.GetActionWithName("Start").Apply(timer);
            return true;
        }
        return false;
    }

    public static bool IsConnectedAnywhere(IEnumerable<IMyShipConnector> connectors)
    {
        for (var e = connectors.GetEnumerator(); e.MoveNext();)
        {
            var connector = e.Current;
            if (connector.IsLocked && connector.IsConnected)
            {
                return true;
            }
        }
        return false;
    }

    public static void KickLoopTimerBlock(MyGridProgram program, string argument)
    {
        // Only bother if argument is non-empty e.g. a command invocation
        if (argument != null && argument.Length > 0)
        {
            var timers = new List<IMyTerminalBlock>();
            // Search for timer blocks on the same ship contain our magic string
            program.GridTerminalSystem.SearchBlocksOfName(ZALIBRARY_LOOP_TIMER_BLOCK_NAME, timers,
                                                          block => block is IMyTimerBlock &&
                                                          block.CubeGrid == program.Me.CubeGrid);
            // Just kick em all
            for (var e = timers.GetEnumerator(); e.MoveNext();)
            {
                var timer = e.Current as IMyTimerBlock;
                if (timer != null && timer.Enabled && !timer.IsCountingDown)
                {
                    timer.GetActionWithName("Start").Apply(timer);
                }
            }
        }
    }

    public static void ForEachBlockOfType<T>(IEnumerable<IMyTerminalBlock> blocks, Action<T> action)
    {
        for (var e = blocks.GetEnumerator(); e.MoveNext();)
        {
            var block = e.Current;
            if (block is T)
            {
                action((T)block);
            }
        }
    }
}
