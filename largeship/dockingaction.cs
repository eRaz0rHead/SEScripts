//@ commons eventdriver
public class DockingAction
{
    private const double RunDelay = 3.0;
    private const char ACTION_DELIMETER = ':';

    public void Init(ZACommons commons, EventDriver eventDriver)
    {
        eventDriver.Schedule(0.0, Run);
    }

    public void Run(ZACommons commons, EventDriver eventDriver)
    {
        for (var e = commons.GetBlockGroupsWithPrefix(DOCKING_ACTION_PREFIX).GetEnumerator(); e.MoveNext();)
        {
            var group = e.Current;

            // Figure out action
            var parts = group.Name.Split(new char[] { ACTION_DELIMETER }, 2);
            string action = "on";
            if (parts.Length == 2)
            {
                action = parts[1];
            }

            // Determine state of first connector (should only have 1)
            bool connected = false;
            var connectors = ZACommons.GetBlocksOfType<IMyShipConnector>(group.Blocks);
            if (connectors.Count > 0)
            {
                var connector = (IMyShipConnector)connectors[0];
                connected = connector.IsLocked && connector.IsConnected;
            }

            if ("on".Equals(action, ZACommons.IGNORE_CASE) ||
                "off".Equals(action, ZACommons.IGNORE_CASE))
            {
                bool enable;
                if ("on".Equals(action, ZACommons.IGNORE_CASE))
                {
                    enable = connected;
                }
                else
                {
                    enable = !connected;
                }

                // Set state according to action
                group.Blocks.ForEach(block =>
                        {
                            if (!(block is IMyShipConnector)) // ignore connectors
                            {
                                block.SetValue<bool>("OnOff", enable);
                            }
                        });
            }
            // Ignore anything else for now
        }

        eventDriver.Schedule(RunDelay, Run);
    }
}
