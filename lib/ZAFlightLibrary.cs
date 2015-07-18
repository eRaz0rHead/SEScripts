public static class ZAFlightLibrary
{
    // Why no enums, Keen?!
    public const int GyroAxisYaw = 0;
    public const int GyroAxisPitch = 1;
    public const int GyroAxisRoll = 2;

    public static bool GetAutoPilotState(IMyRemoteControl remote)
    {
        StringBuilder builder = new StringBuilder();
        remote.GetActionWithName("AutoPilot").WriteValue(remote, builder);
        return builder.ToString() == "On";
    }

    public static void SetThrusterOverride(List<IMyTerminalBlock> thrusters, float force)
    {
        for (var e = thrusters.GetEnumerator(); e.MoveNext();)
        {
            var thruster = e.Current as IMyThrust;
            if (thruster != null) thruster.SetValue<float>("Override", force);
        }
    }

    public static void EnableGyroOverride(IMyGyro gyro, bool enable)
    {
        if ((gyro.GyroOverride && !enable) ||
            (!gyro.GyroOverride && enable))
        {
            gyro.GetActionWithName("Override").Apply(gyro);
        }
    }

    public static void SetAxisVelocity(IMyGyro gyro, int axis, float velocity)
    {
        switch (axis)
        {
            case GyroAxisYaw:
                gyro.SetValue<float>("Yaw", velocity);
                break;
            case GyroAxisPitch:
                gyro.SetValue<float>("Pitch", velocity);
                break;
            case GyroAxisRoll:
                gyro.SetValue<float>("Roll", velocity);
                break;
        }
    }

    public static float GetAxisVelocity(IMyGyro gyro, int axis)
    {
        switch (axis)
        {
            case GyroAxisYaw:
                return gyro.Yaw;
            case GyroAxisPitch:
                return gyro.Pitch;
            case GyroAxisRoll:
                return gyro.Roll;
        }
        return default(float);
    }

    public static void ReverseAxisVelocity(IMyGyro gyro, int axis)
    {
        float? velocity = null;

        switch (axis)
        {
            case GyroAxisYaw:
                velocity = -gyro.Yaw;
                break;
            case GyroAxisPitch:
                velocity = -gyro.Pitch;
                break;
            case GyroAxisRoll:
                velocity = -gyro.Roll;
                break;
        }

        if (velocity != null) SetAxisVelocity(gyro, axis, (float)velocity);
    }

    public static void ResetGyro(IMyGyro gyro)
    {
        SetAxisVelocity(gyro, GyroAxisYaw, 0.0f);
        SetAxisVelocity(gyro, GyroAxisPitch, 0.0f);
        SetAxisVelocity(gyro, GyroAxisRoll, 0.0f);
    }
}