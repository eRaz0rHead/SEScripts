public class SafeMode
{
    private bool? IsControlled = null;

    private bool PreviouslyDocked = false;
    private readonly TimeSpan AbandonmentTimeout = TimeSpan.Parse(ABANDONMENT_TIMEOUT);
    private TimeSpan LastControlled = TimeSpan.FromSeconds(0); // Hmm, TimeSpan.Zero doesn't work?
    private bool Abandoned = false;

    private bool IsShipControlled(IEnumerable<IMyShipController> controllers)
    {
        for (var e = controllers.GetEnumerator(); e.MoveNext();)
        {
            if (e.Current.IsUnderControl)
            {
                return true;
            }
        }
        return false;
    }

    private void ResetAbandonment()
    {
        LastControlled = TimeSpan.FromSeconds(0);
        Abandoned = false;
    }

    public void Run(ZACommons commons, bool? isConnected = null)
    {
        var connected = isConnected != null ? (bool)isConnected :
            ZACommons.IsConnectedAnywhere(commons.Blocks);
        if (connected)
        {
            PreviouslyDocked = true;
            return; // Don't bother if we're docked
        }

        var controllers = ZACommons.GetBlocksOfType<IMyShipController>(commons.Blocks, controller => controller.IsFunctional);
        var currentState = IsShipControlled(controllers);

        if (IsControlled == null)
        {
            IsControlled = currentState;
            return;
        }

        // Flight safety stuff, only check on state change
        if (IsControlled != currentState)
        {
            IsControlled = currentState;

            if (!(bool)IsControlled)
            {
                var dampenersChanged = false;

                // Enable dampeners
                for (var e = controllers.GetEnumerator(); e.MoveNext();)
                {
                    var controller = e.Current;
                    if (!controller.DampenersOverride)
                    {
                        controller.GetActionWithName("DampenersOverride").Apply(controller);
                        dampenersChanged = true;
                    }
                }

                // Only trigger timer block if dampeners were actually engaged
                if (dampenersChanged) ZACommons.StartTimerBlockWithName(commons.Blocks, EMERGENCY_STOP_NAME);
            }
        }

        // Reset abandonment stuff if we just undocked
        if (PreviouslyDocked)
        {
            PreviouslyDocked = false;
            ResetAbandonment();
        }

        if (ABANDONMENT_ENABLED)
        {
            // Abandonment check
            if (!(bool)IsControlled)
            {
                LastControlled += commons.Program.ElapsedTime;

                if (!Abandoned && LastControlled >= AbandonmentTimeout)
                {
                    Abandoned = true;
                    ZACommons.StartTimerBlockWithName(commons.Blocks, SAFE_MODE_NAME);
                }
            }
            else
            {
                ResetAbandonment();
            }
            // commons.Echo("Timeout: " + AbandonmentTimeout);
            // commons.Echo("Last Controlled: " + LastControlled);
        }
    }
}