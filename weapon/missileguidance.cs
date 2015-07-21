public class MissileGuidance
{
    public static Vector3I Forward3I = new Vector3I(0, 0, -1);
    public static Vector3I Up3I = new Vector3I(0, 1, 0);
    public static Vector3I Left3I = new Vector3I(-1, 0, 0);

    public static Vector3D Zero3D = new Vector3D();
    public static Vector3D Forward3D = new Vector3D(0.0, 0.0, 1.0);

    public struct Orientation
    {
        public Vector3D Point;
        public Vector3D Forward;
        public Vector3D Up;
        public Vector3D Left;

        public Orientation(IMyCubeBlock reference)
        {
            Point = reference.GetPosition();
            var forward3I = reference.Position + Forward3I;
            Forward = Vector3D.Normalize(reference.CubeGrid.GridIntegerToWorld(forward3I) - Point);
            var up3I = reference.Position + Up3I;
            Up = Vector3D.Normalize(reference.CubeGrid.GridIntegerToWorld(up3I) - Point);
            var left3I = reference.Position + Left3I;
            Left = Vector3D.Normalize(reference.CubeGrid.GridIntegerToWorld(left3I) - Point);
        }
    }

    private const uint FramesPerRun = 1;
    private const double RunsPerSecond = 60.0 / FramesPerRun;

    private const double GyroMaxRadiansPerSecond = Math.PI; // Really pi*2, but something's odd...

    private Vector3D Target;
    private double RandomOffset;
    private GyroControl gyroControl;

    private const bool PerturbTarget = true;
    private const double PerturbAmplitude = 5000.0;
    private const double PerturbPitchScale = 1.0;
    private const double PerturbYawScale = 1.0;
    private const double PerturbScale = 3.0;
    private const double PerturbOffset = 200.0;
    private const double FinalApproachDistance = 200.0;
    private const float FinalApproachRoll = MathHelper.Pi;
    private const double DetonationDistance = 30.0;

    private const double GyroKp = 250.0; // Proportional constant
    private const double GyroKi = 0.0; // Integral constant
    private const double GyroKd = 200.0; // Derivative constant
    private readonly PIDController yawPID = new PIDController(1.0 / RunsPerSecond);
    private readonly PIDController pitchPID = new PIDController(1.0 / RunsPerSecond);

    private double ScaleAmplitude(double distance)
    {
        distance -= PerturbOffset;
        distance = Math.Max(distance, 0.0);
        distance = Math.Min(distance, PerturbAmplitude);
        // Try linear for now
        return PerturbAmplitude * distance / PerturbAmplitude;
    }

    private double Perturb(TimeSpan timeSinceStart, Orientation orientation, out Vector3D targetVector)
    {
        targetVector = Target - orientation.Point;
        var distance = targetVector.Normalize(); // Original distance
        var amp = ScaleAmplitude(distance);
        var newTarget = Target;
        newTarget += orientation.Up * amp * Math.Cos(PerturbScale * timeSinceStart.TotalSeconds + RandomOffset) * PerturbPitchScale;
        newTarget += orientation.Left * amp * Math.Sin(PerturbScale * timeSinceStart.TotalSeconds + RandomOffset) * PerturbYawScale;
        targetVector = Vector3D.Normalize(newTarget - orientation.Point);
        return distance;
    }

    public void AcquireTarget(MyGridProgram program)
    {
        // Find the sole text panel
        var panelGroup = ZALibrary.GetBlockGroupWithName(program, "CM Target");
        if (panelGroup == null)
        {
            throw new Exception("Missing group: CM Target");
        }

        var panels = ZALibrary.GetBlocksOfType<IMyTextPanel>(panelGroup.Blocks);
        if (panels.Count == 0)
        {
            throw new Exception("Expecting at least 1 text panel");
        }
        var panel = panels[0] as IMyTextPanel; // Just use the first one
        var targetString = panel.GetPublicText();

        // Parse target info
        var parts = targetString.Split(';');
        if (parts.Length != 3)
        {
            throw new Exception("Expecting exactly 3 parts to target info");
        }
        Target = new Vector3D();
        for (int i = 0; i < 3; i++)
        {
            Target.SetDim(i, double.Parse(parts[i]));
        }
    }

    public void Init(MyGridProgram program, EventDriver eventDriver,
                     GyroControl gyroControl)
    {
        // Randomize in case of simultaneous launch with other missiles
        Random random = new Random(this.GetHashCode());
        RandomOffset = 1000.0 * random.NextDouble();

        this.gyroControl = gyroControl;

        yawPID.Kp = GyroKp;
        yawPID.Ki = GyroKi;
        yawPID.Kd = GyroKd;

        pitchPID.Kp = GyroKp;
        pitchPID.Ki = GyroKi;
        pitchPID.Kd = GyroKd;

        eventDriver.Schedule(0, Run);
    }

    public void Run(MyGridProgram program, EventDriver eventDriver)
    {
        var orientation = new Orientation(program.Me);

        Vector3D targetVector;
        double distance;
        if (PerturbTarget)
        {
            distance = Perturb(eventDriver.TimeSinceStart, orientation, out targetVector);
        }
        else
        {
            targetVector = Target - orientation.Point;
            distance = targetVector.Normalize();
        }

        // Transform relative to our forward vector
        targetVector = Vector3D.Transform(targetVector, MatrixD.CreateLookAt(Zero3D, -orientation.Forward, orientation.Up));

        var yawVector = new Vector3D(targetVector.GetDim(0), 0, targetVector.GetDim(2));
        var pitchVector = new Vector3D(0, targetVector.GetDim(1), targetVector.GetDim(2));
        yawVector.Normalize();
        pitchVector.Normalize();

        var yawError = Math.Acos(Vector3D.Dot(yawVector, Forward3D)) * Math.Sign(targetVector.GetDim(0));
        var pitchError = -Math.Acos(Vector3D.Dot(pitchVector, Forward3D)) * Math.Sign(targetVector.GetDim(1));

        var gyroYaw = yawPID.Compute(yawError);
        var gyroPitch = pitchPID.Compute(pitchError);

        if (Math.Abs(gyroYaw) + Math.Abs(gyroPitch) > GyroMaxRadiansPerSecond)
        {
            var adjust = GyroMaxRadiansPerSecond / (Math.Abs(gyroYaw) + Math.Abs(gyroPitch));
            gyroYaw *= adjust;
            gyroPitch *= adjust;
        }

        gyroControl.SetAxisVelocity(GyroControl.Yaw, (float)gyroYaw);
        gyroControl.SetAxisVelocity(GyroControl.Pitch, (float)gyroPitch);

        if (distance < FinalApproachDistance)
        {
            gyroControl.SetAxisVelocity(GyroControl.Roll, FinalApproachRoll);
        }
        if (distance < DetonationDistance)
        {
            // Sensor should have triggered already, just detonate/self-destruct
            var ship = new List<IMyTerminalBlock>();
            program.GridTerminalSystem.GetBlocks(ship);

            for (var e = ship.GetEnumerator(); e.MoveNext();)
            {
                var warhead = e.Current as IMyWarhead;
                if (warhead != null)
                {
                    warhead.GetActionWithName("Detonate").Apply(warhead);
                }
            }
        }

        eventDriver.Schedule(FramesPerRun, Run);
    }
}
