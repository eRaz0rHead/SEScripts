public class LaunchController
{
    private const int STATE_PRIME = -1;
    private const int STATE_RELEASE = 0;
    private const int STATE_START_BURN = 1;
    private const int STATE_BURN = 2;
    private const int STATE_START_AUTOPILOT = 3;
    private const int STATE_AUTOPILOT = 4;
    private const int STATE_READY = 5;

    private readonly TimeSpan BurnTime = TimeSpan.FromSeconds(LAUNCH_BURN_DURATION);
    private TimeSpan CurrentBurnTime = TimeSpan.FromSeconds(0);

    private int? CurrentState = null;

    private bool IsInLauncher(MyGridProgram program)
    {
        var launcherRelease = ZALibrary.GetBlockGroupWithName(program, "Launcher Release");
        return launcherRelease != null;
    }

    public bool Run(MyGridProgram program, ZALibrary.Ship ship)
    {
        var relayBatteries = ZALibrary.GetBlockGroupWithName(program, "Relay Batteries");
        var relaySystems = ZALibrary.GetBlockGroupWithName(program, "Relay Systems");
        var relayRelease = ZALibrary.GetBlockGroupWithName(program, "Relay Release");
        var relayLaunchThruster = ZALibrary.GetBlockGroupWithName(program, "Relay Launch Thruster");
        var remoteGroup = ZALibrary.GetBlockGroupWithName(program, "Relay RC");
        var remotes = remoteGroup != null ? ZALibrary.GetBlocksOfType<IMyRemoteControl>(remoteGroup.Blocks) : null;

        if (relayBatteries == null ||
            relaySystems == null ||
            relayRelease == null ||
            relayLaunchThruster == null ||
            remotes == null || remotes.Count != 1)
        {
            throw new Exception("Missing launch groups and/or remote");
        }

        var remote = remotes[0];

        if (CurrentState == null)
        {
            // Determine current state
            if (IsInLauncher(program))
            {
                CurrentState = STATE_PRIME;
            }
            else if (ZAFlightLibrary.GetAutoPilotState(remote))
            {
                CurrentState = STATE_AUTOPILOT;
            }
            else
            {
                CurrentState = STATE_READY;
            }
        }
        // Very Bad if we were in the burn state...

        // State machine handling
        switch (CurrentState)
        {
            case STATE_PRIME:
                // Wake up batteries
                var batteries = ZALibrary.GetBlocksOfType<IMyBatteryBlock>(relayBatteries.Blocks);
                ZALibrary.EnableBlocks(batteries, true);
                ZALibrary.SetBatteryRecharge(batteries, false);
                // And activate flight systems
                ZALibrary.EnableBlocks(relaySystems.Blocks, true);
                CurrentState = STATE_RELEASE;
                break;
            case STATE_RELEASE:
                ZALibrary.EnableBlocks(relayRelease.Blocks, false);
                CurrentState = STATE_START_BURN;
                break;
            case STATE_START_BURN:
                ZAFlightLibrary.SetThrusterOverride(relayLaunchThruster.Blocks, LAUNCH_BURN_FORCE);
                CurrentBurnTime = TimeSpan.FromSeconds(0);
                CurrentState = STATE_BURN;
                break;
            case STATE_BURN:
                CurrentBurnTime += program.ElapsedTime;
                if (CurrentBurnTime > BurnTime)
                {
                    ZAFlightLibrary.SetThrusterOverride(relayLaunchThruster.Blocks, 0.0f);
                    CurrentState = STATE_START_AUTOPILOT;
                }
                break;
            case STATE_START_AUTOPILOT:
                remote.GetActionWithName("AutoPilot_On").Apply(remote);
                CurrentState = STATE_AUTOPILOT;
                break;
            case STATE_AUTOPILOT:
                if (!ZAFlightLibrary.GetAutoPilotState(remote)) CurrentState = STATE_READY;
                break;
            case STATE_READY:
                return true;
        }

        return false;
    }
}
