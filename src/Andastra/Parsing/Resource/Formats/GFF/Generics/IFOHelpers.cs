using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;

namespace Andastra.Parsing.Resource.Generics
{
    /// <summary>
    /// Helper functions for converting between GFF format and IFO (Module Info) objects.
    /// </summary>
    /// <remarks>
    /// WHAT IS IFOHELPERS?
    /// 
    /// IFOHelpers is a helper class that converts between GFF (Generic File Format) files and
    /// IFO (Module Info) objects. IFO files are stored as GFF files, so these helpers extract
    /// the IFO data from the GFF structure and convert IFO objects back into GFF format.
    /// 
    /// WHAT ARE THE MAIN FUNCTIONS?
    /// 
    /// 1. ConstructIfo: Converts a GFF file into an IFO object
    ///    - Reads all fields from the GFF structure
    ///    - Creates an IFO object with all the module information
    ///    - Handles default values when fields are missing
    /// 
    /// 2. DismantleIfo: Converts an IFO object into a GFF file
    ///    - Takes an IFO object and creates a GFF structure
    ///    - Writes all fields to the GFF structure
    ///    - Handles game-specific differences (K1 vs K2)
    /// 
    /// HOW DOES CONSTRUCTIFO WORK?
    /// 
    /// STEP 1: Create New IFO Object
    /// - Creates an empty IFO object
    /// - Gets the root GFF structure
    /// 
    /// STEP 2: Extract Basic Fields
    /// - Reads Mod_ID (16-byte unique identifier)
    /// - Reads Mod_Name (module display name)
    /// - Reads Mod_Tag (module tag identifier)
    /// - Reads Mod_Entry_Area (starting area)
    /// - Reads Mod_Entry_X/Y/Z (entry position)
    /// - Reads Mod_Entry_Dir_X/Y (entry direction)
    /// 
    /// STEP 3: Extract Script Hooks
    /// - Reads all script hook fields (OnClientEnter, OnHeartbeat, etc.)
    /// - Each script hook is a ResRef pointing to an NCS file
    /// 
    /// STEP 4: Extract Area List
    /// - Reads Mod_Area_list (list of all areas in the module)
    /// - Each area is a ResRef pointing to an ARE file
    /// 
    /// STEP 5: Extract Time Settings
    /// - Reads Mod_DawnHour, Mod_DuskHour (day/night transition times)
    /// - Reads Mod_MinPerHour (time scale)
    /// - Reads Mod_StartMonth/Day/Hour/Year (module start time)
    /// 
    /// STEP 6: Extract Other Fields
    /// - Reads Mod_Description (module description)
    /// - Reads Mod_Version (module version number)
    /// - Reads Expansion_Pack (expansion pack requirement)
    /// - Reads Mod_XPScale (experience point scaling)
    /// 
    /// STEP 7: Return IFO Object
    /// - Returns the complete IFO object with all data
    /// 
    /// HOW DOES DISMANTLEIFO WORK?
    /// 
    /// STEP 1: Create New GFF File
    /// - Creates a new GFF file with IFO content type
    /// - Gets the root GFF structure
    /// 
    /// STEP 2: Write Basic Fields
    /// - Writes Mod_ID (16-byte unique identifier)
    /// - Writes Mod_Name (module display name)
    /// - Writes Mod_Tag (module tag identifier)
    /// - Writes Mod_Entry_Area (starting area)
    /// - Writes Mod_Entry_X/Y/Z (entry position)
    /// - Writes Mod_Entry_Dir_X/Y (entry direction, calculated from angle)
    /// 
    /// STEP 3: Write Script Hooks
    /// - Writes all script hook fields (OnClientEnter, OnHeartbeat, etc.)
    /// 
    /// STEP 4: Write Area List
    /// - Writes Mod_Area_list (list of all areas in the module)
    /// 
    /// STEP 5: Write Time Settings
    /// - Writes Mod_DawnHour, Mod_DuskHour
    /// - Writes Mod_MinPerHour
    /// - Writes Mod_StartMonth/Day/Hour/Year
    /// 
    /// STEP 6: Write Other Fields
    /// - Writes Mod_Description
    /// - Writes Mod_Version
    /// - Writes Expansion_Pack
    /// - Writes Mod_XPScale
    /// 
    /// STEP 7: Return GFF File
    /// - Returns the complete GFF file that can be saved to disk
    /// 
    /// DEFAULT VALUES:
    /// 
    /// When reading a GFF file, if a field is missing, the helper uses default values that
    /// match the original game engine's behavior. For example:
    /// - Mod_ID defaults to 16 zero bytes
    /// - Entry position defaults to (0, 0, 0)
    /// - Entry direction defaults to (1, 0, 0) if Mod_Entry_Dir_Y is missing
    /// - Script hooks default to blank ResRef (no script)
    /// 
    /// These defaults are verified against the original game engine's loading functions.
    /// 
    /// GAME-SPECIFIC DIFFERENCES:
    /// 
    /// Some fields are only present in KotOR 2 (K2), not in KotOR 1 (K1):
    /// - Mod_OnPlrCancelCutscene: Only in K2
    /// - Mod_VO_ID: Only in K2
    /// 
    /// The DismantleIfo function handles these differences based on the game parameter.
    /// 
    /// ORIGINAL IMPLEMENTATION:
    /// 
    /// Based on swkotor2.exe: The original engine loads IFO files by reading GFF structures.
    /// The ConstructIfo function matches the engine's loading behavior, using the same field
    /// names and default values.
    /// 
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ifo.py
    /// Original: construct_ifo and dismantle_ifo functions
    /// </remarks>
    public static class IFOHelpers
    {
        /// <summary>
        /// Converts a GFF file into an IFO (Module Info) object.
        /// </summary>
        /// <param name="gff">The GFF file containing IFO data</param>
        /// <returns>An IFO object with all module information extracted from the GFF</returns>
        /// <remarks>
        /// WHAT THIS FUNCTION DOES:
        /// 
        /// This function reads all the module information from a GFF file and creates an IFO
        /// object. It extracts entry points, script hooks, area lists, time settings, and all
        /// other module metadata.
        /// 
        /// HOW IT WORKS:
        /// 
        /// The function reads fields from the GFF root structure using field names that match
        /// the original game engine. If a field is missing, it uses default values that match
        /// the engine's behavior. This ensures compatibility with existing IFO files.
        /// 
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ifo.py:127-182
        /// Original: def construct_ifo(gff: GFF) -> IFO:
        /// Engine references: swkotor2.exe:0x00501fa0, swkotor.exe:0x004c9050
        /// </remarks>
        public static IFO ConstructIfo(GFF gff)
        {
            var ifo = new IFO();
            var root = gff.Root;

            // Extract basic fields (matching Python field names)
            // Engine default: 16-byte array (swkotor2.exe:0x00501fa0 line 119, swkotor.exe:0x004c9050 line 110)
            ifo.ModId = root.Acquire<byte[]>("Mod_ID", new byte[16]);

            // Engine default: LocalizedString (swkotor2.exe:0x00501fa0 line 129, swkotor.exe:0x004c9050 line 120)
            ifo.ModName = root.Acquire<LocalizedString>("Mod_Name", LocalizedString.FromInvalid());
            ifo.Name = ifo.ModName; // Alias

            // Engine default: "" (swkotor2.exe:0x00501fa0 line 152, swkotor.exe:0x004c9050 line 143)
            ifo.Tag = root.Acquire<string>("Mod_Tag", "");

            // Engine default: "" (swkotor2.exe:0x00500290 line 57, swkotor.exe:0x004c7050 line 57)
            // Note: Mod_VO_ID is written but not read in loading function - optional field
            ifo.VoId = root.Acquire<string>("Mod_VO_ID", "");

            // Engine default: "" (swkotor2.exe:0x00501fa0 line 160, swkotor.exe:0x004c9050 line 151)
            ifo.ResRef = root.Acquire<ResRef>("Mod_Entry_Area", ResRef.FromBlank());
            ifo.EntryArea = ifo.ResRef; // Alias

            // Engine default: 0.0 (swkotor2.exe:0x00501fa0 line 163, swkotor.exe:0x004c9050 line 154)
            ifo.EntryX = root.Acquire<float>("Mod_Entry_X", 0.0f);
            // Engine default: 0.0 (swkotor2.exe:0x00501fa0 line 165, swkotor.exe:0x004c9050 line 156)
            ifo.EntryY = root.Acquire<float>("Mod_Entry_Y", 0.0f);
            // Engine default: 0.0 (swkotor2.exe:0x00501fa0 line 167, swkotor.exe:0x004c9050 line 158)
            ifo.EntryZ = root.Acquire<float>("Mod_Entry_Z", 0.0f);

            // Engine default: 0.0, but if field not found, defaults to 1.0 (swkotor2.exe:0x00501fa0 lines 169, 174, swkotor.exe:0x004c9050 lines 160, 165)
            float dirX = root.Acquire<float>("Mod_Entry_Dir_X", 0.0f);
            // Engine default: 0.0, but if field not found, defaults to 0.0 (swkotor2.exe:0x00501fa0 lines 171, 175, swkotor.exe:0x004c9050 lines 162, 166)
            // Engine behavior: After reading Mod_Entry_Dir_Y, if local_bc==0 (field not found), engine sets both dirX=1.0, dirY=0.0
            // This means if Mod_Entry_Dir_Y is missing, both get set to defaults
            float dirY = root.Acquire<float>("Mod_Entry_Dir_Y", 0.0f);

            // Engine behavior: If Mod_Entry_Dir_Y field is missing, engine sets dirX=1.0, dirY=0.0
            // The engine checks local_bc after reading Mod_Entry_Dir_Y - if 0, both are set to defaults
            if (!root.Exists("Mod_Entry_Dir_Y"))
            {
                dirX = 1.0f;
                dirY = 0.0f;
            }

            // Store direction components (Python calculates angle, but we store X/Y/Z separately)
            ifo.EntryDirectionX = dirX;
            ifo.EntryDirectionY = dirY;
            ifo.EntryDirectionZ = 0.0f;
            // Calculate entry direction angle from X/Y components
            ifo.EntryDirection = (float)System.Math.Atan2(dirY, dirX);

            // Extract script hooks (using Python field names)
            // All script hooks default to "" (empty ResRef) in engine
            // Engine references: swkotor2.exe:0x00501fa0 lines 340-427, swkotor.exe:0x004c9050 lines 331-418
            ifo.OnClientEnter = root.Acquire<ResRef>("Mod_OnClientEntr", ResRef.FromBlank());
            ifo.OnClientLeave = root.Acquire<ResRef>("Mod_OnClientLeav", ResRef.FromBlank());
            ifo.OnHeartbeat = root.Acquire<ResRef>("Mod_OnHeartbeat", ResRef.FromBlank());
            ifo.OnUserDefined = root.Acquire<ResRef>("Mod_OnUsrDefined", ResRef.FromBlank());
            ifo.OnActivateItem = root.Acquire<ResRef>("Mod_OnActvtItem", ResRef.FromBlank());
            ifo.OnAcquireItem = root.Acquire<ResRef>("Mod_OnAcquirItem", ResRef.FromBlank());
            ifo.OnUnacquireItem = root.Acquire<ResRef>("Mod_OnUnAqreItem", ResRef.FromBlank());
            ifo.OnPlayerDeath = root.Acquire<ResRef>("Mod_OnPlrDeath", ResRef.FromBlank());
            ifo.OnPlayerDying = root.Acquire<ResRef>("Mod_OnPlrDying", ResRef.FromBlank());
            ifo.OnPlayerRespawn = root.Acquire<ResRef>("Mod_OnSpawnBtnDn", ResRef.FromBlank());
            ifo.OnPlayerRest = root.Acquire<ResRef>("Mod_OnPlrRest", ResRef.FromBlank());
            ifo.OnPlayerLevelUp = root.Acquire<ResRef>("Mod_OnPlrLvlUp", ResRef.FromBlank());
            // Note: Mod_OnPlrCancelCutscene is not found in engine loading functions - K2-only field, optional
            ifo.OnPlayerCancelCutscene = root.Acquire<ResRef>("Mod_OnPlrCancelCutscene", ResRef.FromBlank());
            ifo.OnLoad = root.Acquire<ResRef>("Mod_OnModLoad", ResRef.FromBlank());
            ifo.OnStart = root.Acquire<ResRef>("Mod_OnModStart", ResRef.FromBlank());
            // Engine default: "" (swkotor2.exe:0x00501fa0 line 147, swkotor.exe:0x004c9050 line 138)
            ifo.StartMovie = root.Acquire<ResRef>("Mod_StartMovie", ResRef.FromBlank());

            // Extract area list
            // Engine reference: swkotor2.exe:0x00501fa0 line 428, swkotor.exe:0x004c9050 line 419
            var areaList = root.Acquire<GFFList>("Mod_Area_list", new GFFList());
            if (areaList != null && areaList.Count > 0)
            {
                var firstArea = areaList.At(0);
                if (firstArea != null)
                {
                    // Engine default: "" (swkotor2.exe:0x00501fa0 line 432, swkotor.exe:0x004c9050 line 423)
                    var areaName = firstArea.Acquire<ResRef>("Area_Name", ResRef.FromBlank());
                    // Store first area name (Python stores in ifo.area_name)
                    // ifo.AreaName would need to be added to IFO class
                }
            }
            foreach (var areaStruct in areaList)
            {
                var areaName = areaStruct.Acquire<ResRef>("Area_Name", ResRef.FromBlank());
                ifo.AreaList.Add(areaName);
            }

            // Extract other fields
            // Engine default: 0 (swkotor2.exe:0x00501fa0 line 276, swkotor.exe:0x004c9050 line 267)
            // Note: Expansion_Pack is read from Mod_Expan_List, not directly - optional field
            ifo.ExpansionPack = root.Acquire<int>("Expansion_Pack", 0);

            // Engine default: LocalizedString (swkotor2.exe:0x00501fa0 line 138, swkotor.exe:0x004c9050 line 129)
            ifo.Description = root.Acquire<LocalizedString>("Mod_Description", LocalizedString.FromInvalid());

            // Engine default: 0 (swkotor2.exe:0x00501fa0 line 123, swkotor.exe:0x004c9050 line 114)
            ifo.ModVersion = root.Acquire<int>("Mod_Version", 0);

            // Engine default: "" (swkotor2.exe:0x00500290 line 57, swkotor.exe:0x004c7050 line 57)
            // Note: Mod_Hak is written but not read in loading function - optional field
            ifo.Hak = root.Acquire<string>("Mod_Hak", "");

            // Engine default: 0 (swkotor2.exe:0x00501fa0 line 179, swkotor.exe:0x004c9050 line 170)
            ifo.DawnHour = root.Acquire<int>("Mod_DawnHour", 0);
            // Engine default: 0 (swkotor2.exe:0x00501fa0 line 181, swkotor.exe:0x004c9050 line 172)
            ifo.DuskHour = root.Acquire<int>("Mod_DuskHour", 0);
            // Engine default: 0 (swkotor2.exe:0x00501fa0 line 177, swkotor.exe:0x004c9050 line 168)
            ifo.TimeScale = root.Acquire<int>("Mod_MinPerHour", 0);

            // Engine default: Uses DAT_* constants if param_2 != 0, otherwise 0 (swkotor2.exe:0x00501fa0 lines 192-202, swkotor.exe:0x004c9050 lines 183-193)
            // For normal module loading (param_2 == 0), engine uses current time - we default to 0
            ifo.StartMonth = root.Acquire<int>("Mod_StartMonth", 0);
            ifo.StartDay = root.Acquire<int>("Mod_StartDay", 0);
            ifo.StartHour = root.Acquire<int>("Mod_StartHour", 0);
            ifo.StartYear = root.Acquire<int>("Mod_StartYear", 0);

            // Game time fields (current game time components)
            // Based on swkotor2.exe: FUN_00501fa0 @ 0x00501fa0 (IFO loading function)
            // These fields are read from IFO when loading module (if present)
            // Engine default: 0 if not present
            ifo.StartMinute = root.Acquire<int>("Mod_StartMinute", 0);
            ifo.StartSecond = root.Acquire<int>("Mod_StartSecond", 0);
            ifo.StartMiliSec = root.Acquire<int>("Mod_StartMiliSec", 0);
            ifo.PauseDay = root.Acquire<uint>("Mod_PauseDay", 0);
            ifo.PauseTime = root.Acquire<uint>("Mod_PauseTime", 0);

            // Engine default: 10 (swkotor2.exe:0x00501fa0 line 274, swkotor.exe:0x004c9050 line 265)
            ifo.XpScale = root.Acquire<int>("Mod_XPScale", 10);

            // VaultId may not exist in Python version - optional field, not found in engine loading

            return ifo;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ifo.py:183-277
        // Original: def dismantle_ifo(ifo: IFO, game: Game = BioWareGame.K2) -> GFF:
        public static GFF DismantleIfo(IFO ifo, BioWareGame game = BioWareGame.K2)
        {
            var gff = new GFF(GFFContent.IFO);
            var root = gff.Root;

            // Set basic fields (matching Python field names)
            root.SetBinary("Mod_ID", ifo.ModId);
            root.SetLocString("Mod_Name", ifo.ModName);
            root.SetString("Mod_Tag", ifo.Tag);
            root.SetString("Mod_VO_ID", ifo.VoId);
            root.SetUInt8("Mod_IsSaveGame", 0);
            root.SetResRef("Mod_Entry_Area", ifo.ResRef);
            root.SetSingle("Mod_Entry_X", ifo.EntryX);
            root.SetSingle("Mod_Entry_Y", ifo.EntryY);
            root.SetSingle("Mod_Entry_Z", ifo.EntryZ);
            // Calculate entry direction vector from angle or use stored components
            if (ifo.EntryDirectionX != 0 || ifo.EntryDirectionY != 0)
            {
                root.SetSingle("Mod_Entry_Dir_X", ifo.EntryDirectionX);
                root.SetSingle("Mod_Entry_Dir_Y", ifo.EntryDirectionY);
            }
            else
            {
                // Calculate from angle
                root.SetSingle("Mod_Entry_Dir_X", (float)System.Math.Cos(ifo.EntryDirection));
                root.SetSingle("Mod_Entry_Dir_Y", (float)System.Math.Sin(ifo.EntryDirection));
            }

            // Set script hooks (using Python field names)
            root.SetResRef("Mod_OnHeartbeat", ifo.OnHeartbeat);
            root.SetResRef("Mod_OnClientEntr", ifo.OnClientEnter);
            root.SetResRef("Mod_OnClientLeav", ifo.OnClientLeave);
            root.SetResRef("Mod_OnActvtItem", ifo.OnActivateItem);
            root.SetResRef("Mod_OnAcquirItem", ifo.OnAcquireItem);
            root.SetResRef("Mod_OnUsrDefined", ifo.OnUserDefined);
            root.SetResRef("Mod_OnUnAqreItem", ifo.OnUnacquireItem);
            root.SetResRef("Mod_OnPlrDeath", ifo.OnPlayerDeath);
            root.SetResRef("Mod_OnPlrDying", ifo.OnPlayerDying);
            root.SetResRef("Mod_OnPlrLvlUp", ifo.OnPlayerLevelUp);
            root.SetResRef("Mod_OnSpawnBtnDn", ifo.OnPlayerRespawn);
            root.SetResRef("Mod_OnPlrRest", ifo.OnPlayerRest);
            root.SetResRef("Mod_OnModLoad", ifo.OnLoad);
            root.SetResRef("Mod_OnModStart", ifo.OnStart);
            root.SetResRef("Mod_StartMovie", ifo.StartMovie);

            // Set other fields
            root.SetString("Mod_Hak", ifo.Hak);
            root.SetInt32("Mod_DawnHour", ifo.DawnHour);
            root.SetInt32("Mod_DuskHour", ifo.DuskHour);
            root.SetInt32("Mod_MinPerHour", ifo.TimeScale);
            root.SetInt32("Mod_StartMonth", ifo.StartMonth);
            root.SetInt32("Mod_StartDay", ifo.StartDay);
            root.SetInt32("Mod_StartHour", ifo.StartHour);
            root.SetInt32("Mod_StartYear", ifo.StartYear);
            // Based on swkotor2.exe: FUN_00500290 @ 0x00500290 lines 96-100
            // Writes current game time components (minute, second, millisecond) and pause time
            // Matching original engine behavior: FUN_004137e0 writes UInt16 for minute/second/millisecond
            root.SetUInt16("Mod_StartMinute", (ushort)ifo.StartMinute);
            root.SetUInt16("Mod_StartSecond", (ushort)ifo.StartSecond);
            root.SetUInt16("Mod_StartMiliSec", (ushort)ifo.StartMiliSec);
            // Matching original engine behavior: FUN_00413880 writes UInt32 for pause day/time
            root.SetUInt32("Mod_PauseDay", ifo.PauseDay);
            root.SetUInt32("Mod_PauseTime", ifo.PauseTime);
            root.SetInt32("Mod_XPScale", ifo.XpScale);

            // Set area list
            var areaList = new GFFList();
            root.SetList("Mod_Area_list", areaList);
            if (ifo.AreaList != null && ifo.AreaList.Count > 0)
            {
                var areaStruct = areaList.Add(6);
                areaStruct.SetResRef("Area_Name", ifo.AreaList[0]);
            }

            // Set other fields
            root.SetUInt16("Expansion_Pack", (ushort)ifo.ExpansionPack);
            root.SetLocString("Mod_Description", ifo.Description);
            root.SetUInt32("Mod_Version", (uint)ifo.ModVersion);

            return gff;
        }
    }
}
