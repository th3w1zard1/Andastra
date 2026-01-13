using System;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.MDL
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_types.py:13-305
    // Original: enum and flag definitions for MDL
    public enum MDLGeometryType
    {
        GEOMETRY_UNKNOWN = 0,
        GEOMETRY_NORMAL = 1,
        GEOMETRY_SKINNED = 2,
        GEOMETRY_DANGLY = 3,
        GEOMETRY_SABER = 4
    }

    public enum MDLClassification
    {
        INVALID = 0,
        EFFECT = 1,
        TILE = 2,
        CHARACTER = 4,
        DOOR = 8,
        PLACEABLE = 16,
        OTHER = 32,
        GUI = 64,
        ITEM = 128,
        LIGHTSABER = 256,
        WAYPOINT = 512,
        WEAPON = 1024,
        FURNITURE = 2048
    }

    [Flags]
    public enum MDLNodeFlags
    {
        HEADER = 0x0001,
        LIGHT = 0x0002,
        EMITTER = 0x0004,
        CAMERA = 0x0008,
        REFERENCE = 0x0010,
        MESH = 0x0020,
        SKIN = 0x0040,
        ANIM = 0x0080,
        DANGLY = 0x0100,
        AABB = 0x0200,
        SABER = 0x0800
    }

    public enum MDLNodeType
    {
        DUMMY = 1,
        TRIMESH = 2,
        DANGLYMESH = 3,
        LIGHT = 4,
        EMITTER = 5,
        REFERENCE = 6,
        PATCH = 7,
        AABB = 8,
        CAMERA = 10,
        BINARY = 11,
        SABER = 12
    }

    public enum MDLControllerType
    {
        INVALID = -1,
        POSITION = 8,
        ORIENTATION = 20,
        SCALE = 36,
        ALPHA = 132,

        COLOR = 76,
        RADIUS = 88,
        SHADOWRADIUS = 96,
        VERTICALDISPLACEMENT = 100,
        MULTIPLIER = 140,

        ALPHAEND = 80,
        ALPHASTART = 84,
        BIRTHRATE = 88,
        BOUNCE_CO = 92,
        COMBINETIME = 96,
        DRAG = 100,
        FPS = 104,
        FRAMEEND = 108,
        FRAMESTART = 112,
        GRAV = 116,
        LIFEEXP = 120,
        MASS = 124,
        P2P_BEZIER2 = 128,
        P2P_BEZIER3 = 132,
        PARTICLEROT = 136,
        RANDVEL = 140,
        SIZESTART = 144,
        SIZEEND = 148,
        SIZESTART_Y = 152,
        SIZEEND_Y = 156,
        SPREAD = 160,
        THRESHOLD = 164,
        VELOCITY = 168,
        XSIZE = 172,
        YSIZE = 176,
        BLURLENGTH = 180,
        LIGHTNINGDELAY = 184,
        LIGHTNINGRADIUS = 188,
        LIGHTNINGSCALE = 192,
        LIGHTNINGSUBDIV = 196,
        LIGHTNINGZIGZAG = 200,
        ALPHAMID = 216,
        PERCENTSTART = 220,
        PERCENTMID = 224,
        PERCENTEND = 228,
        SIZEMID = 232,
        SIZEMID_Y = 236,
        RANDOMBIRTHRATE = 240,
        TARGETSIZE = 252,
        NUMCONTROLPTS = 256,
        CONTROLPTRADIUS = 260,
        CONTROLPTDELAY = 264,
        TANGENTSPREAD = 268,
        TANGENTLENGTH = 272,
        COLORMID = 284,
        COLOREND = 380,
        COLORSTART = 392,
        DETONATE = 502,

        SELFILLUMCOLOR = 100,
        ILLUM_COLOR = 100,
        SIZESTART_emitter = 144,
        SIZEEND_emitter = 148,
        LIGHTNINGDELAY_emitter = 184,
        LIGHTNINGRADIUS_emitter = 188,
        LIGHTNINGSCALE_emitter = 192,
        ALPHAMID_emitter = 216,
        SIZEMID_emitter = 232,
        DETONATE_emitter = 502
    }

    [Flags]
    public enum MDLTrimeshProps
    {
        NONE = 0x00,
        LIGHTMAP = 0x01,
        COMPRESSED = 0x02,
        UNKNOWN = 0x04,
        TANGENTS = 0x08,
        UNKNOWNA = 0x10,
        UNKNOWNB = 0x20,
        UNKNOWNC = 0x40,
        UNKNOWND = 0x80
    }

    public enum MDLEmitterType
    {
        STATIC = 0,
        FIRE = 1,
        FOUNTAIN = 2,
        LIGHTNING = 3
    }

    public enum MDLRenderType
    {
        NORMAL = 0,
        LINKED = 1,
        BILLBOARD_TO_LOCAL_Z = 2,
        BILLBOARD_TO_WORLD_Z = 3,
        ALIGNED_TO_WORLD_Z = 4,
        ALIGNED_TO_PARTICLE_DIR = 5,
        MOTION_BLUR = 6
    }

    public enum MDLBlendType
    {
        NORMAL = 0,
        PUNCH = 1,
        LIGHTEN = 2,
        MULTIPLY = 3
    }

    public enum MDLUpdateType
    {
        FOUNTAIN = 0,
        SINGLE = 1,
        EXPLOSION = 2,
        LIGHTNING = 3
    }

    [Flags]
    public enum MDLTrimeshFlags
    {
        TILEFADE = 0x0001,
        HEAD = 0x0002,
        RENDER = 0x0004,
        SHADOW = 0x0008,
        BEAMING = 0x0010,
        RENDER_ENV_MAP = 0x0020,
        LIGHTMAP = 0x0040,
        SKIN = 0x0080
    }

    [Flags]
    public enum MDLLightFlags
    {
        ENABLED = 0x0001,
        SHADOW = 0x0002,
        FLARE = 0x0004,
        FADING = 0x0008,
        AMBIENT = 0x0010
    }

    [Flags]
    public enum MDLEmitterFlags
    {
        P2P = 0x0001,
        P2P_SEL = 0x0002,
        P2P_BEZIER = 0x0002,
        AFFECTED_WIND = 0x0004,
        TINTED = 0x0008,
        BOUNCE = 0x0010,
        RANDOM = 0x0020,
        INHERIT = 0x0040,
        INHERIT_VEL = 0x0080,
        INHERIT_LOCAL = 0x0100,
        SPLAT = 0x0200,
        INHERIT_PART = 0x0400,
        DEPTH_TEXTURE = 0x0800,
        FLAG_13 = 0x1000,
        LOOP = P2P
    }

    [Flags]
    public enum MDLSaberFlags
    {
        FLARE = 0x0001,
        DYNAMIC = 0x0002,
        TRAIL = 0x0004
    }
}
