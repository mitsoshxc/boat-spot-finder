namespace BoatSpotFinder.Core.Entities;

[Flags]
public enum VesselType
{
    None = 0,
    SailBoat = 1,
    MotorBoat = 2,
    Catamaran = 4,
    RIB = 8,
    Yacht = 16,
    Other = 32
}
