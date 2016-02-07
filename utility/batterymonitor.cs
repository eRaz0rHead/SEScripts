public class BatteryMonitor : DockingHandler
{
    public interface LowBatteryHandler
    {
        void LowBattery(ZACommons commons, EventDriver eventDriver,
                        bool started);
    }

    private const double RunDelay = 5.0;

    private readonly LowBatteryHandler lowBatteryHandler;

    private bool IsDocked = true;
    private bool Triggered = false;

    public BatteryMonitor(LowBatteryHandler lowBatteryHandler = null)
    {
        this.lowBatteryHandler = lowBatteryHandler;
    }

    public void Docked(ZACommons commons, EventDriver eventDriver)
    {
        IsDocked = true;
    }

    public void Undocked(ZACommons commons, EventDriver eventDriver)
    {
        if (IsDocked)
        {
            Triggered = false;

            IsDocked = false;
            eventDriver.Schedule(RunDelay, Run);
        }
    }

    public void Init(ZACommons commons, EventDriver eventDriver)
    {
        IsDocked = false;
        eventDriver.Schedule(0.0, Run);
    }

    public void Run(ZACommons commons, EventDriver eventDriver)
    {
        if (IsDocked) return;

        RunInternal(commons, eventDriver);

        eventDriver.Schedule(RunDelay, Run);
    }

    private void RunInternal(ZACommons commons, EventDriver eventDriver)
    {
        var lowBattery = ZACommons.GetBlockWithName<IMyTimerBlock>(commons.Blocks, LOW_BATTERY_NAME);
        // Don't bother if there's no timer block or handler
        if (lowBatteryHandler == null && lowBattery == null) return;

        var batteries = ZACommons.GetBlocksOfType<IMyBatteryBlock>(commons.Blocks, battery => battery.IsFunctional && ((IMyBatteryBlock)battery).Enabled);

        // Avoid divide-by-zero in case there are no batteries
        if (batteries.Count == 0) return;

        var currentStoredPower = 0.0f;
        var maxStoredPower = 0.0f;

        // Hmm, doesn't check battery recharge state...
        // With the "full-auto mode" (if it worked as advertised),
        // it probably doesn't make sense to check input/output state anyway
        for (var e = batteries.GetEnumerator(); e.MoveNext();)
        {
            var battery = e.Current as IMyBatteryBlock;

            currentStoredPower += battery.CurrentStoredPower;
            maxStoredPower += battery.MaxStoredPower;
        }

        var batteryPercent = currentStoredPower / maxStoredPower;

        if (!Triggered && batteryPercent < BATTERY_THRESHOLD)
        {
            Triggered = true;
            if (lowBatteryHandler != null) lowBatteryHandler.LowBattery(commons, eventDriver, true);
            if (lowBattery != null) lowBattery.GetActionWithName("Start").Apply(lowBattery);
        }
        else if (Triggered && batteryPercent >= BATTERY_THRESHOLD)
        {
            Triggered = false;
            if (lowBatteryHandler != null) lowBatteryHandler.LowBattery(commons, eventDriver, false);
        }
    }
}
