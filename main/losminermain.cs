public class MySafeModeHandler : SafeModeHandler
{
    public void SafeMode(ZACommons commons, EventDriver eventDriver)
    {
        // Check after 1 second (let timer block's action take effect)
        eventDriver.Schedule(1.0, (c,ed) =>
                {
                    new EmergencyStop().SafeMode(c, ed);
                });
    }
}

public readonly EventDriver eventDriver = new EventDriver(timerName: STANDARD_LOOP_TIMER_BLOCK_NAME);
public readonly LOSMiner minerController = new LOSMiner();
public readonly DockingManager dockingManager = new DockingManager();
public readonly SafeMode safeMode = new SafeMode(new MySafeModeHandler());
public readonly SmartUndock smartUndock = new SmartUndock();
private readonly ZAStorage myStorage = new ZAStorage();

private readonly ShipOrientation shipOrientation = new ShipOrientation();

private bool FirstRun = true;

void Main(string argument)
{
    var commons = new ShipControlCommons(this, shipOrientation,
                                         storage: myStorage);

    if (FirstRun)
    {
        FirstRun = false;

        myStorage.Decode(Storage);

        shipOrientation.SetShipReference(commons, MINER_REFERENCE_GROUP);

        dockingManager.Init(commons, eventDriver, safeMode,
                            new BatteryMonitor(),
                            new RedundancyManager());
        smartUndock.Init(commons);
    }

    eventDriver.Tick(commons, preAction: () => {
            dockingManager.HandleCommand(commons, eventDriver, argument);
            safeMode.HandleCommand(commons, eventDriver, argument);
            smartUndock.HandleCommand(commons, eventDriver, argument, () =>
                    {
                        dockingManager.ManageShip(commons, eventDriver, false);
                    });
            minerController.HandleCommand(commons, eventDriver, argument);
        });

    if (commons.IsDirty) Storage = myStorage.Encode();
}
