namespace AuroraEngine.Common.Formats.BWM
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1534-1541
    // Original: class BWMMostSignificantPlane(IntEnum)
    public enum BWMMostSignificantPlane
    {
        NegativeZ = -3,
        NegativeY = -2,
        NegativeX = -1,
        None = 0,
        PositiveX = 1,
        PositiveY = 2,
        PositiveZ = 3
    }
}

