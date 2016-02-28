public class SolarGyroController
{
    private const double RunDelay = 1.0;

    public struct SolarPanelDetails
    {
        public float MaxPowerOutput;
        public float DefinedPowerOutput;

        public SolarPanelDetails(IEnumerable<IMyTerminalBlock> blocks)
        {
            MaxPowerOutput = 0.0f;
            DefinedPowerOutput = 0.0f;

            for (var e = ZACommons.GetBlocksOfType<IMySolarPanel>(blocks).GetEnumerator(); e.MoveNext();)
            {
                var panel = (IMySolarPanel)e.Current;

                if (panel.IsFunctional && panel.IsWorking)
                {
                    var output = SolarHack.GetSolarPanelMaxOutput(panel);
                    MaxPowerOutput += output != null ? (float)output : 0.0f;
                    DefinedPowerOutput += panel.CubeGrid.GridSize == 2.5f ? 0.120f : 0.030f;
                }
            }
        }
    }

    private readonly int[] AllowedAxes;
    private readonly float[] LastVelocities;

    private readonly TimeSpan AxisTimeout = TimeSpan.FromSeconds(SOLAR_GYRO_AXIS_TIMEOUT);

    private float? MaxPower = null;
    private int AxisIndex = 0;
    private bool Active = false;
    private DateTime TimeOnAxis;

    public SolarGyroController(params int[] allowedAxes)
    {
        // Weird things happening with array constants
        AllowedAxes = (int[])allowedAxes.Clone();
        LastVelocities = new float[AllowedAxes.Length];
        for (int i = 0; i < LastVelocities.Length; i++)
        {
            LastVelocities[i] = SOLAR_GYRO_VELOCITY;
        }
    }

    public void Init(ZACommons commons, EventDriver eventDriver)
    {
        var shipControl = (ShipControlCommons)commons;
        var gyroControl = shipControl.GyroControl;
        gyroControl.Reset();
        gyroControl.EnableOverride(true);

        Active = true;
        MaxPower = null; // Use first-run initialization
        eventDriver.Schedule(0.0, Run);
    }

    public void Run(ZACommons commons, EventDriver eventDriver)
    {
        if (!Active)
        {
            commons.Echo("Solar Max Power: Paused");
            return;
        }

        var shipControl = (ShipControlCommons)commons;
        var gyroControl = shipControl.GyroControl;
        var currentAxis = AllowedAxes[AxisIndex];

        if (MaxPower == null)
        {
            MaxPower = -100.0f; // Start with something absurdly low to kick things off
            gyroControl.Reset();
            gyroControl.EnableOverride(true);
            gyroControl.SetAxisVelocity(currentAxis, LastVelocities[AxisIndex]);
            TimeOnAxis = commons.Now;
        }

        var solarPanelDetails = new SolarPanelDetails(commons.Blocks);
        var currentMaxPower = solarPanelDetails.MaxPowerOutput;

        var minError = solarPanelDetails.DefinedPowerOutput * 0.005f; // From experimentation
        var delta = currentMaxPower - MaxPower;
        MaxPower = currentMaxPower;

        if (delta > minError)
        {
            // Keep going
            gyroControl.EnableOverride(true);
        }
        else if (delta < -minError)
        {
            // Back up
            gyroControl.EnableOverride(true);
            LastVelocities[AxisIndex] = -LastVelocities[AxisIndex];
            gyroControl.SetAxisVelocity(currentAxis, LastVelocities[AxisIndex]);
        }
        else
        {
            // Hold still
            gyroControl.EnableOverride(false);
        }

        var changeTime = commons.Now - AxisTimeout;

        if (TimeOnAxis < changeTime)
        {
            // Time out, try next axis
            AxisIndex++;
            AxisIndex %= AllowedAxes.Length;

            gyroControl.Reset();
            gyroControl.EnableOverride(true);
            gyroControl.SetAxisVelocity(AllowedAxes[AxisIndex], LastVelocities[AxisIndex]);
            TimeOnAxis = commons.Now;
        }

        commons.Echo(string.Format("Solar Max Power: {0}", ZACommons.FormatPower(currentMaxPower)));
        
        eventDriver.Schedule(RunDelay, Run);
    }

    public void HandleCommand(ZACommons commons, EventDriver eventDriver,
                              string argument)
    {
        // Handle commands
        argument = argument.Trim().ToLower();
        if (argument == "pause")
        {
            Active = false;
            var shipControl = (ShipControlCommons)commons;
            var gyroControl = shipControl.GyroControl;
            gyroControl.Reset();
            gyroControl.EnableOverride(false);
        }
        else if (argument == "resume")
        {
            if (!Active) Init(commons, eventDriver);
        }
    }
}
