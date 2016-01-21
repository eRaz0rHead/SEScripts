public class MyEmergencyStopHandler : SafeMode.EmergencyStopHandler
{
    public void EmergencyStop(ZACommons commons, EventDriver eventDriver)
    {
        // Check after 1 second (let timer block's action take effect)
        eventDriver.Schedule(1.0, (c,ed) =>
                {
                    SafetyStop.ThrusterCheck(c, ed);
                });
    }
}

public readonly EventDriver eventDriver = new EventDriver(timerName: STANDARD_LOOP_TIMER_BLOCK_NAME);
public readonly SafeMode safeMode = new SafeMode(new MyEmergencyStopHandler());
private readonly RedundancyManager redundancyManager = new RedundancyManager();
public readonly CruiseControl cruiseControl = new CruiseControl();
private readonly DoorAutoCloser doorAutoCloser = new DoorAutoCloser();
private readonly SimpleAirlock simpleAirlock = new SimpleAirlock();
public readonly ZAStorage myStorage = new ZAStorage();

private readonly ShipOrientation shipOrientation = new ShipOrientation();

private bool FirstRun = true;

void Main(string argument)
{
    var commons = new ShipControlCommons(this, shipOrientation,
                                         shipGroup: SHIP_NAME,
                                         storage: myStorage);

    if (FirstRun)
    {
        FirstRun = false;

        myStorage.Decode(Storage);

        shipOrientation.SetShipReference(commons, "Autopilot Reference");

        eventDriver.Schedule(0.0);
        redundancyManager.Init(commons, eventDriver);
    }

    eventDriver.Tick(commons, mainAction: () => {
            safeMode.Run(commons, eventDriver, false);
            doorAutoCloser.Run(commons);
            simpleAirlock.Run(commons);

            eventDriver.Schedule(1.0);
        }, preAction: () => {
            cruiseControl.HandleCommand(commons, eventDriver, argument);
        });

    if (commons.IsDirty) Storage = myStorage.Encode();
}
