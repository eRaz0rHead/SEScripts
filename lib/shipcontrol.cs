// Do not hold a reference to an instance of this class between runs
public class ShipControlCommons : ZACommons
{
    private readonly ShipOrientation shipOrientation;

    public Base6Directions.Direction ShipUp
    {
        get { return shipOrientation.ShipUp; }
    }

    public Base6Directions.Direction ShipForward
    {
        get { return shipOrientation.ShipForward; }
    }

    public ShipControlCommons(MyGridProgram program,
                              ShipOrientation shipOrientation,
                              string shipGroup = null)
        : base(program, shipGroup: shipGroup)
    {
        this.shipOrientation = shipOrientation;
        // Use own programmable block as reference point
        Reference = program.Me;
        ReferencePoint = Reference.GetPosition();
    }

    // GyroControl

    public GyroControl GyroControl
    {
        get
        {
            if (m_gyroControl == null)
            {
                m_gyroControl = new GyroControl();
                m_gyroControl.Init(Blocks,
                                   shipUp: shipOrientation.ShipUp,
                                   shipForward: shipOrientation.ShipForward);
            }
            return m_gyroControl;
        }
    }
    private GyroControl m_gyroControl = null;

    // ThrustControl

    public ThrustControl ThrustControl
    {
        get
        {
            if (m_thrustControl == null)
            {
                m_thrustControl = new ThrustControl();
                m_thrustControl.Init(Blocks,
                                     shipUp: shipOrientation.ShipUp,
                                     shipForward: shipOrientation.ShipForward);
            }
            return m_thrustControl;
        }
    }
    private ThrustControl m_thrustControl = null;

    // Reference vectors (i.e. orientation in world coordinates)

    public IMyCubeBlock Reference { get; private set; }

    public Vector3D ReferencePoint { get; private set; }

    public Vector3D ReferenceUp
    {
        get
        {
            if (m_referenceUp == null)
            {
                m_referenceUp = GetReferenceVector(shipOrientation.ShipUp);
            }
            return (Vector3D)m_referenceUp;
        }
    }
    private Vector3D? m_referenceUp = null;

    public Vector3D ReferenceForward
    {
        get
        {
            if (m_referenceForward == null)
            {
                m_referenceForward = GetReferenceVector(shipOrientation.ShipForward);
            }
            return (Vector3D)m_referenceForward;
        }
    }
    private Vector3D? m_referenceForward = null;

    public Vector3D ReferenceLeft
    {
        get
        {
            if (m_referenceLeft == null)
            {
                m_referenceLeft = GetReferenceVector(Base6Directions.GetLeft(shipOrientation.ShipUp, shipOrientation.ShipForward));
            }
            return (Vector3D)m_referenceLeft;
        }
    }
    private Vector3D? m_referenceLeft = null;

    private Vector3D GetReferenceVector(Base6Directions.Direction direction)
    {
        var offset = Reference.Position + Base6Directions.GetIntVector(direction);
        return Vector3D.Normalize(Reference.CubeGrid.GridIntegerToWorld(offset) - ReferencePoint);
    }
}
