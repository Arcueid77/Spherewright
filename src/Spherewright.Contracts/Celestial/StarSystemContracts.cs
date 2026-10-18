namespace Spherewright.Contracts.Celestial;

public sealed class LocalStarSystemSnapshot
{
    public string SessionId { get; set; } = string.Empty;

    public int LocalPlanetId { get; set; }

    public int StarId { get; set; }

    public string StarName { get; set; } = string.Empty;

    public long CapturedAtGameTick { get; set; }

    public string StateHash { get; set; } = string.Empty;

    public int StateHashVersion { get; set; } = 1;

    public List<PlanetSnapshot> Planets { get; set; } = new List<PlanetSnapshot>();
}

public sealed class PlanetSnapshot
{
    public int PlanetId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string PlanetType { get; set; } = string.Empty;

    public int ThemeId { get; set; }

    public string ThemeName { get; set; } = string.Empty;

    public bool IsCurrentPlanet { get; set; }

    public bool IsBirthPlanet { get; set; }

    public bool IsGasGiant { get; set; }

    public bool FactoryLoaded { get; set; }

    public float RealRadius { get; set; }

    public float OrbitRadius { get; set; }

    public double DistanceFromPlayer { get; set; }

    public UniversalPositionSnapshot UniversalPosition { get; set; } = new UniversalPositionSnapshot();

    public List<string> PotentialResourceTypes { get; set; } = new List<string>();

    // Theme possibilities are never surveyed vein evidence. On older Plugins this
    // field is absent; an empty legacy list must not be treated as absence proof.
    public bool PotentialResourceTypesVisible { get; set; }

    public int RequiredObserveLevel { get; set; }
}

public sealed class UniversalPositionSnapshot
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Z { get; set; }
}
