//@ commons eventdriver
public class ProductionManager
{
    private const double RunDelay = 1.0;
    private const string StateKey = "ProductionManager_State";

    private const char DelimiterStart = '{';
    private const char DelimiterEnd = '}';
    private const char DelimiterAmount = ':';

    public struct ItemStock
    {
        public string SubtypeName;
        public float Amount;

        public ItemStock(string subtypeName, float amount)
        {
            SubtypeName = subtypeName;
            Amount = amount * PRODUCTION_MANAGER_SETUP_AMOUNT_MULTIPLIER;
        }

        public override string ToString()
        {
            return SubtypeName + ":" + Amount;
        }

        public static bool TryParse(string str, out ItemStock result)
        {
            int start = str.IndexOf(DelimiterStart);
            int end = str.IndexOf(DelimiterEnd);

            if (start >= 0 && end >= 0 && end > start)
            {
                var data = str.Substring(start + 1, end - start - 1);
                var parts = data.Split(':');
                if (parts.Length == 2)
                {
                    var subtypeName = parts[0].Trim();
                    var amountString = parts[1].Trim();
                    float amount;
                    if (float.TryParse(amountString, out amount))
                    {
                        result = new ItemStock(subtypeName, amount);
                        return true;
                    }
                }
            }

            result = default(ItemStock);
            return false;
        }
    }

    public struct AssemblerTarget
    {
        public float Amount;
        public List<IMyAssembler> Assemblers;

        public AssemblerTarget(float amount)
        {
            Amount = amount;
            Assemblers = new List<IMyAssembler>();
        }

        public void EnableAssemblers(bool enable)
        {
            Assemblers.ForEach(assembler =>
                    {
                        if ((enable && !assembler.Enabled) ||
                            (!enable && assembler.Enabled))
                        {
                            assembler.SetValue<bool>("OnOff", enable);
                        }
                    });
        }
    }

    private ItemStock[] defaultItemStocks = new ItemStock[]
        {
            new ItemStock("BulletproofGlass", 12000),
            new ItemStock("Computer", 6500),
            new ItemStock("Construction", 50000),
            new ItemStock("Detector", 400),
            new ItemStock("Display", 500),
            new ItemStock("Explosives", 500),
            new ItemStock("Girder", 3500),
            new ItemStock("GravityGenerator", 250),
            new ItemStock("InteriorPlate", 55000),
            new ItemStock("LargeTube", 6000),
            new ItemStock("Medical", 120),
            new ItemStock("MetalGrid", 15500),
            new ItemStock("Motor", 16000),
            new ItemStock("PowerCell", 2800),
            new ItemStock("RadioCommunication", 250),
            new ItemStock("Reactor", 10000),
            new ItemStock("SmallTube", 26000),
            new ItemStock("SolarCell", 2800),
            new ItemStock("SteelPlate", 300000),
            new ItemStock("Superconductor", 3000),
            new ItemStock("Thrust", 16000),
        };

    private const int STATE_ACTIVE = 1;
    private const int STATE_INACTIVATING = -1;
    private const int STATE_INACTIVE = 0;

    private int CurrentState = STATE_ACTIVE;

    private Dictionary<string, VRage.MyFixedPoint> EnumerateItems(List<IMyTerminalBlock> blocks, HashSet<string> allowedSubtypes)
    {
        var result = new Dictionary<string, VRage.MyFixedPoint>();

        for (var e = blocks.GetEnumerator(); e.MoveNext();)
        {
            var owner = e.Current;
            if (owner != null)
            {
                for (int i = 0; i < owner.GetInventoryCount(); i++)
                {
                    var inventory = owner.GetInventory(i);
                    var items = inventory.GetItems();
                    for (var f = items.GetEnumerator(); f.MoveNext();)
                    {
                        var item = f.Current;
                        var subtypeName = item.Content.SubtypeName;
                        if (allowedSubtypes.Contains(subtypeName))
                        {
                            VRage.MyFixedPoint current;
                            if (!result.TryGetValue(subtypeName, out current)) current = (VRage.MyFixedPoint)0.0f;
                            result[subtypeName] = current + item.Amount;
                        }
                    }
                }
            }
        }

        return result;
    }

    public void Setup(List<IMyTerminalBlock> ship)
    {
        // First build map of defaults
        var defaultItemStocksMap = new Dictionary<string, ItemStock>();
        for (int i = 0; i < defaultItemStocks.Length; i++)
        {
            var itemStock = defaultItemStocks[i];
            defaultItemStocksMap.Add(itemStock.SubtypeName, itemStock);
        }

        // Get assemblers
        var assemblers = ZACommons.GetBlocksOfType<IMyAssembler>(ship);
        var candidates = new LinkedList<IMyAssembler>();

        // If anything has already been appropriately named, remove it from our map
        for (var e = assemblers.GetEnumerator(); e.MoveNext();)
        {
            var assembler = (IMyAssembler)e.Current;
            ItemStock target;
            if (ItemStock.TryParse(assembler.CustomName, out target))
            {
                defaultItemStocksMap.Remove(target.SubtypeName);
            }
            else
            {
                // Otherwise add assembler as candidate for renaming
                candidates.AddLast(assembler);
            }
        }

        for (var e = defaultItemStocksMap.Values.GetEnumerator(); e.MoveNext() && candidates.First != null;)
        {
            var itemStock = e.Current;

            // Get first candidate
            var candidate = candidates.First.Value;
            candidates.RemoveFirst();

            // Rename appropriately
            StringBuilder builder = new StringBuilder();
            builder.Append(candidate.CustomName);
            builder.Append(' ');
            builder.Append(DelimiterStart);
            builder.Append(itemStock.SubtypeName);
            builder.Append(DelimiterAmount);
            builder.Append(itemStock.Amount);
            builder.Append(DelimiterEnd);
            candidate.SetCustomName(builder);
        }
    }

    public void Init(ZACommons commons, EventDriver eventDriver)
    {
        var stateValue = commons.GetValue(StateKey);
        if (stateValue != null)
        {
            int state;
            if (int.TryParse(stateValue, out state))
            {
                // Use remembered state
                CurrentState = state;
                // Should really validate, but eh...
            }
        }
        else
        {
            CurrentState = STATE_ACTIVE;
        }

        if (PRODUCTION_MANAGER_SETUP)
        {
            Setup(LIMIT_PRODUCTION_MANAGER_SAME_GRID ? commons.Blocks : commons.AllBlocks);
        }
        else if (CurrentState != STATE_INACTIVE)
        {
            eventDriver.Schedule(0.0, Run);
        }
    }

    public void Run(ZACommons commons, EventDriver eventDriver)
    {
        if (CurrentState == STATE_INACTIVE) return;

        var ship = LIMIT_PRODUCTION_MANAGER_SAME_GRID ? commons.Blocks : commons.AllBlocks;

        var allowedSubtypes = new HashSet<string>();
        var assemblerTargets = new Dictionary<string, AssemblerTarget>();

        var assemblers = ZACommons.GetBlocksOfType<IMyAssembler>(ship);
        for (var e = assemblers.GetEnumerator(); e.MoveNext();)
        {
            var assembler = (IMyAssembler)e.Current;
            ItemStock target;
            if (ItemStock.TryParse(assembler.CustomName, out target))
            {
                var subtype = target.SubtypeName;

                // When we take inventory, filter to just these types
                allowedSubtypes.Add(subtype);

                AssemblerTarget assemblerTarget;
                if (!assemblerTargets.TryGetValue(subtype, out assemblerTarget))
                {
                    assemblerTarget = new AssemblerTarget(target.Amount);
                    assemblerTargets.Add(subtype, assemblerTarget);
                }

                // Remember this assembler for this subtype
                assemblerTarget.Assemblers.Add(assembler);

                // Adjust target amount, if necessary
                assemblerTarget.Amount = Math.Max(assemblerTarget.Amount, target.Amount);
            }
        }

        if (CurrentState == STATE_ACTIVE)
        {
            // Get current stocks
            var stocks = EnumerateItems(ship, allowedSubtypes);

            // Now we just enable/disable based on who's low
            for (var e = assemblerTargets.GetEnumerator(); e.MoveNext();)
            {
                var subtype = e.Current.Key;
                var target = e.Current.Value;
                VRage.MyFixedPoint currentStock;
                if (!stocks.TryGetValue(subtype, out currentStock)) currentStock = (VRage.MyFixedPoint)0.0f;

                // Enable or disable based on current stock
                target.EnableAssemblers((float)currentStock < target.Amount);
            }
        }
        else if (CurrentState == STATE_INACTIVATING)
        {
            // Shut down all known assemblers
            for (var e = assemblerTargets.Values.GetEnumerator(); e.MoveNext();)
            {
                e.Current.EnableAssemblers(false);
            }
            SetState(commons, STATE_INACTIVE);
        }

        eventDriver.Schedule(RunDelay, Run);
    }

    public void HandleCommand(ZACommons commons, EventDriver eventDriver,
                              string argument)
    {
        argument = argument.Trim().ToLower();
        switch (argument)
        {
            case "prodpause":
                // We need this intermediate step because we don't know
                // the assemblers here.
                SetState(commons, STATE_INACTIVATING);
                break;
            case "prodresume":
                if (CurrentState != STATE_INACTIVE)
                {
                    SetState(commons, STATE_ACTIVE);
                    if (!PRODUCTION_MANAGER_SETUP) eventDriver.Schedule(RunDelay, Run);
                }
                break;
        }
    }

    public void Display(ZACommons commons)
    {
        if (PRODUCTION_MANAGER_SETUP)
        {
            commons.Echo("Setup complete. Set PRODUCTION_MANAGER_SETUP to false to enable ProductionManager");
        }
        else
        {
            commons.Echo("Production Manager: " +
                         (CurrentState == STATE_INACTIVE ? "Paused" : "Active"));
        }
    }

    private void SetState(ZACommons commons, int newState)
    {
        CurrentState = newState;
        commons.SetValue(StateKey, CurrentState.ToString());
    }
}
