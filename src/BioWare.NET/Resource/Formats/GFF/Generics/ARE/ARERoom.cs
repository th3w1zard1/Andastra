using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.GFF.Generics.ARE
{
    /// <summary>
    /// Represents a room definition within an area.
    /// </summary>
    /// <remarks>
    /// WHAT IS AN AREROOM?
    ///
    /// An ARERoom represents a specific region within a game area. It's not the same as a
    /// LYT room (which is a 3D model piece). Instead, an ARERoom is an invisible region that
    /// defines special properties for that part of the area, like audio settings, weather
    /// behavior, and force rating modifiers.
    ///
    /// WHAT DATA DOES IT STORE?
    ///
    /// An ARERoom stores:
    ///
    /// 1. Name: A unique identifier for this room (referenced by VIS files)
    ///    - This name is used to identify the room in visibility calculations
    ///    - VIS files use room names to determine which rooms can see each other
    ///    - Example: "room_01", "hallway_main", "cave_entrance"
    ///
    /// 2. Weather: Whether weather effects are disabled in this room (KotOR 2 only)
    ///    - If true, rain, snow, and lightning don't appear in this room
    ///    - Used for indoor areas where weather shouldn't appear
    ///    - Only available in KotOR 2, not in KotOR 1
    ///
    /// 3. EnvAudio: The environment audio index for this room
    ///    - This is an index into the environment audio system
    ///    - Determines the acoustic properties (reverb, echo) of the room
    ///    - Different values create different sound effects (indoor, outdoor, cave, etc.)
    ///    - Example: 0 = outdoor, 1 = indoor, 2 = cave
    ///
    /// 4. ForceRating: The force rating modifier for this room (KotOR 2 only)
    ///    - This modifies the player's force rating when they're in this room
    ///    - Used for areas with special force properties (light side, dark side, etc.)
    ///    - Only available in KotOR 2, not in KotOR 1
    ///
    /// 5. AmbientScale: The ambient audio scaling factor for this room
    ///    - This multiplies the volume of ambient sounds in this room
    ///    - 1.0 = normal volume, 2.0 = twice as loud, 0.5 = half as loud
    ///    - Used to make some rooms louder or quieter than others
    ///
    /// HOW ARE ROOMS USED?
    ///
    /// STEP 1: Audio Zones
    /// - Each room defines its own audio properties (EnvAudio, AmbientScale)
    /// - When the player enters a room, the game switches to that room's audio settings
    /// - This creates different sound effects in different parts of the area
    ///
    /// STEP 2: Weather Control
    /// - Each room can disable weather effects (Weather = true)
    /// - Used for indoor areas where rain/snow shouldn't appear
    /// - Only works in KotOR 2
    ///
    /// STEP 3: Force Rating
    /// - Each room can modify the player's force rating (ForceRating)
    /// - Used for areas with special force properties
    /// - Only works in KotOR 2
    ///
    /// STEP 4: Visibility Calculations
    /// - VIS files use room names to determine which rooms can see each other
    /// - This affects which room models are rendered (culling)
    /// - Rooms that can't see each other don't render each other's models
    ///
    /// WHY ARE ROOMS NEEDED?
    ///
    /// Without rooms, the entire area would have the same audio properties, weather behavior,
    /// and force rating. Rooms allow different parts of the area to have different properties,
    /// creating more realistic and varied gameplay experiences.
    ///
    /// RELATIONSHIP TO OTHER FILES:
    ///
    /// - ARE files: Contain the list of rooms for the area
    /// - VIS files: Use room names to determine visibility between rooms
    /// - LYT files: Define the 3D model rooms (different from ARE rooms)
    ///
    /// Note: ARE rooms are NOT the same as LYT rooms. LYT rooms are 3D model pieces that
    /// make up the area's geometry. ARE rooms are invisible regions that define properties
    /// for parts of the area.
    ///
    /// ORIGINAL IMPLEMENTATION:
    ///
    /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Rooms are loaded when an area is initialized. The engine
    /// uses room names for visibility calculations and applies audio/weather/force properties
    /// based on which room the player is currently in.
    ///
    /// References:
    /// - vendor/reone/include/reone/resource/parser/gff/are.h:185-191 - ARE_Rooms struct
    /// - vendor/reone/src/libs/resource/parser/gff/are.cpp:244-251 - parseARE_Rooms function
    /// - vendor/Kotor.NET/Kotor.NET/Resources/KotorARE/ARE.cs:99-106 - ARERoom class
    /// - vendor/KotOR.js/src/module/ModuleRoom.ts - ModuleRoom class (runtime room handling)
    /// - Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/are.py:300-376
    /// </remarks>
    [PublicAPI]
    public sealed class ARERoom
    {
        /// <summary>
        /// Room name identifier - unique identifier for this room (referenced by VIS files).
        /// </summary>
        /// <remarks>
        /// WHAT IS THE ROOM NAME?
        ///
        /// The room name is a unique identifier that distinguishes this room from all other
        /// rooms in the area. It's used by VIS files to determine which rooms can see each
        /// other, and by scripts to reference specific rooms.
        ///
        /// HOW IS IT USED?
        ///
        /// - VIS files use room names to calculate visibility between rooms
        /// - Scripts can reference rooms by name to check which room the player is in
        /// - The game engine uses room names to switch audio/weather properties when the
        ///   player moves between rooms
        ///
        /// Reference: reone/are.cpp:250 (strct.RoomName = gff.getString("RoomName"))
        /// Reference: Kotor.NET/ARE.cs:105 (RoomName String property)
        /// Reference: KotOR.js/ModuleRoom.ts (room name)
        /// </remarks>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Disable weather flag for this room (KotOR 2 only).
        /// If true, weather effects (rain, snow, lightning) are disabled in this room.
        /// </summary>
        /// <remarks>
        /// WHAT IS THE WEATHER FLAG?
        ///
        /// The weather flag controls whether weather effects appear in this room. If the flag
        /// is true, rain, snow, and lightning don't appear, even if the area has weather enabled.
        /// This is used for indoor areas where weather shouldn't appear.
        ///
        /// HOW IS IT USED?
        ///
        /// When the player enters a room with Weather = true:
        /// - Rain stops (if it was raining)
        /// - Snow stops (if it was snowing)
        /// - Lightning stops (if there was lightning)
        /// - The weather effects resume when the player leaves the room
        ///
        /// This only works in KotOR 2. In KotOR 1, this field doesn't exist and is ignored.
        ///
        /// Reference: reone/are.cpp:247 (strct.DisableWeather = gff.getUint("DisableWeather"))
        /// Reference: Kotor.NET/ARE.cs:102 (DisableWeather Byte property)
        /// Reference: KotOR.js/ModuleArea.ts:463 (room_struct.set_uint8("DisableWeather", room.weather))
        /// </remarks>
        public bool Weather { get; set; }

        /// <summary>
        /// Environment audio index - index into environment audio system for room acoustics.
        /// </summary>
        /// <remarks>
        /// WHAT IS ENVAUDIO?
        ///
        /// EnvAudio is an index into the environment audio system. It determines the acoustic
        /// properties (reverb, echo) of the room. Different values create different sound effects.
        ///
        /// HOW IS IT USED?
        ///
        /// When the player enters a room, the game engine:
        /// 1. Reads the EnvAudio value for that room
        /// 2. Looks up the acoustic properties for that index
        /// 3. Applies those properties to all sounds in the room
        ///
        /// Common values:
        /// - 0 = Outdoor (no reverb, natural sound)
        /// - 1 = Indoor (slight reverb, enclosed sound)
        /// - 2 = Cave (strong reverb, echo effect)
        /// - 3 = Large hall (very strong reverb, long echo)
        ///
        /// This creates realistic sound effects that match the room's environment.
        ///
        /// Reference: reone/are.cpp:248 (strct.EnvAudio = gff.getInt("EnvAudio"))
        /// Reference: Kotor.NET/ARE.cs:103 (EnvAudio Int32 property)
        /// Reference: KotOR.js/ModuleArea.ts:138 (audio.environmentAudio = 0)
        /// </remarks>
        public int EnvAudio { get; set; }

        /// <summary>
        /// Force rating modifier for this room (KotOR 2 only).
        /// This modifies the player's force rating when they're in this room.
        /// </summary>
        /// <remarks>
        /// WHAT IS FORCE RATING?
        ///
        /// Force rating is a value that represents how strong the Force is in a particular
        /// location. In KotOR 2, some areas have special force properties (light side areas,
        /// dark side areas, etc.) that affect the player's force abilities.
        ///
        /// HOW IS IT USED?
        ///
        /// When the player enters a room, the game engine:
        /// 1. Reads the ForceRating value for that room
        /// 2. Adds it to the player's base force rating
        /// 3. Uses the modified force rating for force ability calculations
        ///
        /// Example:
        /// - Player's base force rating: 10
        /// - Room's ForceRating: +5
        /// - Effective force rating in that room: 15
        ///
        /// This allows certain areas to boost or reduce the player's force abilities.
        ///
        /// This only works in KotOR 2. In KotOR 1, this field doesn't exist and is ignored.
        ///
        /// Reference: reone/are.cpp:249 (strct.ForceRating = gff.getInt("ForceRating"))
        /// Reference: Kotor.NET/ARE.cs:104 (ForceRating Int32 property)
        /// Reference: KotOR.js/ModuleArea.ts:464 (room_struct.set_int32("ForceRating", room.force_rating))
        /// </remarks>
        public int ForceRating { get; set; }

        /// <summary>
        /// Ambient audio scaling factor - multiplies the volume of ambient sounds in this room.
        /// 1.0 = normal volume, 2.0 = twice as loud, 0.5 = half as loud.
        /// </summary>
        /// <remarks>
        /// WHAT IS AMBIENT SCALE?
        ///
        /// AmbientScale is a multiplier that affects the volume of ambient sounds (background
        /// sounds like wind, water, machinery) in this room. It allows some rooms to be louder
        /// or quieter than others.
        ///
        /// HOW IS IT USED?
        ///
        /// When the player enters a room, the game engine:
        /// 1. Reads the AmbientScale value for that room
        /// 2. Multiplies all ambient sound volumes by this value
        /// 3. Plays the adjusted sounds
        ///
        /// Example:
        /// - Base ambient sound volume: 50%
        /// - Room's AmbientScale: 2.0
        /// - Effective volume in that room: 100% (50% * 2.0)
        ///
        /// This allows some rooms to have louder ambient sounds (like noisy factories) or
        /// quieter ambient sounds (like quiet libraries).
        ///
        /// Reference: reone/are.cpp:246 (strct.AmbientScale = gff.getFloat("AmbientScale"))
        /// Reference: Kotor.NET/ARE.cs:101 (AmbientScale Single property)
        /// Reference: KotOR.js/ModuleArea.ts:459 (room_struct.set_single("AmbientScale", room.ambient_scale))
        /// </remarks>
        public float AmbientScale { get; set; }

        /// <summary>
        /// Initializes a new instance of the ARERoom class.
        /// </summary>
        /// <param name="name">Room name identifier.</param>
        /// <param name="weather">Disable weather flag (KotOR 2 only).</param>
        /// <param name="envAudio">Environment audio index.</param>
        /// <param name="forceRating">Force rating modifier (KotOR 2 only).</param>
        /// <param name="ambientScale">Ambient audio scaling factor.</param>
        public ARERoom(string name = "", bool weather = false, int envAudio = 0, int forceRating = 0, float ambientScale = 0.0f)
        {
            Name = name;
            Weather = weather;
            EnvAudio = envAudio;
            ForceRating = forceRating;
            AmbientScale = ambientScale;
        }
    }
}

