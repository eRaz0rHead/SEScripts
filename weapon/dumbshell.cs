public class DumbShell
{
    private const string BATTERY_GROUP = "Shell Batteries";
    private const string SYSTEMS_GROUP = "Shell Systems";
    private const string RELEASE_GROUP = "Shell Attach";
    private const string MASS_GROUP = "Shell Mass";

    private const uint TicksPerRun = 1;
    private const double RunsPerSecond = 60.0 / TicksPerRun;

    private Action<ZACommons, EventDriver> PostLaunch;
    public Vector3D InitialPosition { get; private set; }
    public TimeSpan InitialTime { get; private set; }
    public Vector3D LauncherVelocity { get; private set; }

    public void Init(ZACommons commons, EventDriver eventDriver,
                     Action<ZACommons, EventDriver> postLaunch = null)
    {
        PostLaunch = postLaunch;
        InitialPosition = ((ShipControlCommons)commons).ReferencePoint;
        InitialTime = eventDriver.TimeSinceStart;
        eventDriver.Schedule(0.0, Prime);
    }

    public void Prime(ZACommons commons, EventDriver eventDriver)
    {
        var batteryGroup = commons.GetBlockGroupWithName(BATTERY_GROUP);
        if (batteryGroup == null)
        {
            throw new Exception("Group missing: " + BATTERY_GROUP);
        }
        var systemsGroup = commons.GetBlockGroupWithName(SYSTEMS_GROUP);
        if (systemsGroup == null)
        {
            throw new Exception("Group missing: " + SYSTEMS_GROUP);
        }

        // Wake up batteries
        var batteries = ZACommons.GetBlocksOfType<IMyBatteryBlock>(batteryGroup.Blocks);
        batteries.ForEach(battery =>
                {
                    battery.SetValue<bool>("OnOff", true);
                    battery.SetValue<bool>("Recharge", false);
                    battery.SetValue<bool>("Discharge", true);
                });

        // Activate systems
        ZACommons.EnableBlocks(systemsGroup.Blocks, true);

        eventDriver.Schedule(1.0, Release);
    }

    public void Release(ZACommons commons, EventDriver eventDriver)
    {
        var releaseGroup = commons.GetBlockGroupWithName(RELEASE_GROUP);
        if (releaseGroup == null)
        {
            throw new Exception("Group missing: " + RELEASE_GROUP);
        }

        // Get one last reading from launcher and determine velocity
        var launcherDelta = ((ShipControlCommons)commons).ReferencePoint -
            InitialPosition;
        var deltaTime = (eventDriver.TimeSinceStart - InitialTime).TotalSeconds;
        LauncherVelocity = launcherDelta / deltaTime;

        // Turn release group off
        ZACommons.EnableBlocks(releaseGroup.Blocks, false);

        eventDriver.Schedule(1.0, Demass);
    }

    public void Demass(ZACommons commons, EventDriver eventDriver)
    {
        var shipControl = (ShipControlCommons)commons;

        var deltaTime = (eventDriver.TimeSinceStart - InitialTime).TotalSeconds;
        var launcherDelta = LauncherVelocity * deltaTime;
        var distanceFromLauncher = (shipControl.ReferencePoint -
                                    (InitialPosition + launcherDelta)).LengthSquared();

        if (distanceFromLauncher < DemassDistance * DemassDistance)
        {
            // Not yet
            eventDriver.Schedule(TicksPerRun, Demass);
            return;
        }

        // Disable mass
        var group = commons.GetBlockGroupWithName(MASS_GROUP);
        if (group != null)  ZACommons.EnableBlocks(group.Blocks, false);

        // Start roll
        shipControl.GyroControl.EnableOverride(true);
        shipControl.GyroControl.SetAxisVelocity(GyroControl.Roll,
                                                MathHelper.Pi);

        // All done
        if (PostLaunch != null) PostLaunch(commons, eventDriver);
    }

}
