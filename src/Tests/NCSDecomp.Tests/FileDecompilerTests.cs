using System;
using System.IO;
using Andastra.Parsing.Formats.NCS.NCSDecomp;
using FluentAssertions;
using NCSDecomp.Tests.TestHelpers;
using Xunit;

namespace NCSDecomp.Tests
{
    public class FileDecompilerTests : IDisposable
    {
        private readonly FileDecompiler _decompiler;
        private readonly Settings _settings;
        private readonly string _tempDir;

        public FileDecompilerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);

            _settings = new Settings();
            _settings.SetProperty("Output Directory", _tempDir);
            _settings.SetProperty("Game Type", "K1");

            // Try to find a real nwscript.nss file first using NWScriptLocator
            NcsFile foundNwscript = NWScriptLocator.FindNWScriptFile(NWScriptLocator.GameType.K1, _settings);
            string nwscriptPath: string | null = null;

            if (foundNwscript != null && foundNwscript.IsFile())
            {
                // Use the found nwscript.nss file
                nwscriptPath = foundNwscript.GetAbsolutePath();
            }
            else
            {
                // Create a comprehensive dummy nwscript.nss for testing
                nwscriptPath = Path.Combine(_tempDir, "nwscript.nss");
                string dummyContent = CreateDummyNwscriptForK1();
                System.IO.File.WriteAllText(nwscriptPath, dummyContent);
            }

            // Set the nwscript path in settings so FileDecompiler can find it
            _settings.SetProperty("NWScript Path", nwscriptPath);
            _settings.SetProperty("K1 nwscript Path", nwscriptPath);

            _decompiler = new FileDecompiler(_settings, NWScriptLocator.GameType.K1);
        }

        /// <summary>
        /// Creates a comprehensive dummy nwscript.nss file for K1 testing.
        /// Includes essential constants and action functions that are commonly used in decompilation.
        /// Matching format from tools/k1_nwscript.nss (swkotor.exe: nwscript.nss format)
        /// </summary>
        private static string CreateDummyNwscriptForK1()
        {
            var sb = new System.Text.StringBuilder();

            // Header comment
            sb.AppendLine("////////////////////////////////////////////////////////");
            sb.AppendLine("//");
            sb.AppendLine("//  NWScript");
            sb.AppendLine("//");
            sb.AppendLine("//  The list of actions and pre-defined constants.");
            sb.AppendLine("//");
            sb.AppendLine("//  (c) BioWare Corp, 1999");
            sb.AppendLine("//");
            sb.AppendLine("//  Dummy file for testing purposes");
            sb.AppendLine("//");
            sb.AppendLine("////////////////////////////////////////////////////////");
            sb.AppendLine();

            // Engine structures
            sb.AppendLine("#define ENGINE_NUM_STRUCTURES   4");
            sb.AppendLine("#define ENGINE_STRUCTURE_0      effect");
            sb.AppendLine("#define ENGINE_STRUCTURE_1      event");
            sb.AppendLine("#define ENGINE_STRUCTURE_2      location");
            sb.AppendLine("#define ENGINE_STRUCTURE_3      talent");
            sb.AppendLine();

            // Essential constants
            sb.AppendLine("// Constants");
            sb.AppendLine();
            sb.AppendLine("int        NUM_INVENTORY_SLOTS          = 18;");
            sb.AppendLine();
            sb.AppendLine("int    TRUE                     = 1;");
            sb.AppendLine("int    FALSE                    = 0;");
            sb.AppendLine();
            sb.AppendLine("float  DIRECTION_EAST           = 0.0;");
            sb.AppendLine("float  DIRECTION_NORTH          = 90.0;");
            sb.AppendLine("float  DIRECTION_WEST           = 180.0;");
            sb.AppendLine("float  DIRECTION_SOUTH          = 270.0;");
            sb.AppendLine("float  PI                       = 3.141592;");
            sb.AppendLine();
            sb.AppendLine("int    OBJECT_SELF              = 0;");
            sb.AppendLine("int    OBJECT_INVALID           = 1;");
            sb.AppendLine();
            sb.AppendLine("int    ATTITUDE_NEUTRAL         = 0;");
            sb.AppendLine("int    ATTITUDE_AGGRESSIVE      = 1;");
            sb.AppendLine("int    ATTITUDE_DEFENSIVE       = 2;");
            sb.AppendLine("int    ATTITUDE_SPECIAL         = 3;");
            sb.AppendLine();
            sb.AppendLine("int    OBJECT_TYPE_CREATURE         = 1;");
            sb.AppendLine("int    OBJECT_TYPE_ITEM             = 2;");
            sb.AppendLine("int    OBJECT_TYPE_TRIGGER          = 4;");
            sb.AppendLine("int    OBJECT_TYPE_DOOR             = 8;");
            sb.AppendLine("int    OBJECT_TYPE_AREA_OF_EFFECT   = 16;");
            sb.AppendLine("int    OBJECT_TYPE_WAYPOINT         = 32;");
            sb.AppendLine("int    OBJECT_TYPE_PLACEABLE        = 64;");
            sb.AppendLine("int    OBJECT_TYPE_STORE            = 128;");
            sb.AppendLine("int    OBJECT_TYPE_ENCOUNTER        = 256;");
            sb.AppendLine("int    OBJECT_TYPE_SOUND            = 512;");
            sb.AppendLine("int    OBJECT_TYPE_ALL              = 32767;");
            sb.AppendLine();

            // Actions section - essential functions for decompilation
            sb.AppendLine("// Actions");
            sb.AppendLine();

            // Action 0: Random
            sb.AppendLine("// 0: Get an integer between 0 and nMaxInteger-1.");
            sb.AppendLine("int Random(int nMaxInteger);");
            sb.AppendLine();

            // Action 1: PrintString
            sb.AppendLine("// 1: Output sString to the log file.");
            sb.AppendLine("void PrintString(string sString);");
            sb.AppendLine();

            // Action 2: PrintFloat
            sb.AppendLine("// 2: Output a formatted float to the log file.");
            sb.AppendLine("void PrintFloat(float fFloat, int nWidth=18, int nDecimals=9);");
            sb.AppendLine();

            // Action 3: FloatToString
            sb.AppendLine("// 3: Convert fFloat into a string.");
            sb.AppendLine("string FloatToString(float fFloat, int nWidth=18, int nDecimals=9);");
            sb.AppendLine();

            // Action 4: PrintInteger
            sb.AppendLine("// 4: Output nInteger to the log file.");
            sb.AppendLine("void PrintInteger(int nInteger);");
            sb.AppendLine();

            // Action 5: PrintObject
            sb.AppendLine("// 5: Output oObject's ID to the log file.");
            sb.AppendLine("void PrintObject(object oObject);");
            sb.AppendLine();

            // Action 6: AssignCommand
            sb.AppendLine("// 6: Assign aActionToAssign to oActionSubject.");
            sb.AppendLine("void AssignCommand(object oActionSubject, action aActionToAssign);");
            sb.AppendLine();

            // Action 7: DelayCommand
            sb.AppendLine("// 7: Delay aActionToDelay by fSeconds.");
            sb.AppendLine("void DelayCommand(float fSeconds, action aActionToDelay);");
            sb.AppendLine();

            // Action 8: ExecuteScript
            sb.AppendLine("// 8: Make oTarget run sScript and then return execution to the calling script.");
            sb.AppendLine("void ExecuteScript(string sScript, object oTarget, int nScriptVar=-1);");
            sb.AppendLine();

            // Action 9: ClearAllActions
            sb.AppendLine("// 9: Clear all the actions of the caller.");
            sb.AppendLine("void ClearAllActions();");
            sb.AppendLine();

            // Action 10: SetFacing
            sb.AppendLine("// 10: Cause the caller to face fDirection.");
            sb.AppendLine("void SetFacing(float fDirection);");
            sb.AppendLine();

            // Action 11: SwitchPlayerCharacter
            sb.AppendLine("// 11: Switches the main character to a specified NPC");
            sb.AppendLine("int SwitchPlayerCharacter(int nNPC);");
            sb.AppendLine();

            // Action 12: SetTime
            sb.AppendLine("// 12: Set the time to the time specified.");
            sb.AppendLine("void SetTime(int nHour, int nMinute, int nSecond, int nMillisecond);");
            sb.AppendLine();

            // Action 13: SetPartyLeader
            sb.AppendLine("// 13: Sets which party member should be the controlled character");
            sb.AppendLine("int SetPartyLeader(int nNPC);");
            sb.AppendLine();

            // Action 14: SetAreaUnescapable
            sb.AppendLine("// 14: Sets whether the current area is escapable or not");
            sb.AppendLine("void SetAreaUnescapable(int bUnescapable);");
            sb.AppendLine();

            // Action 15: GetAreaUnescapable
            sb.AppendLine("// 15: Returns whether the current area is escapable or not");
            sb.AppendLine("int GetAreaUnescapable();");
            sb.AppendLine();

            // Action 16: GetTimeHour
            sb.AppendLine("// 16: Get the current hour.");
            sb.AppendLine("int GetTimeHour();");
            sb.AppendLine();

            // Action 17: GetTimeMinute
            sb.AppendLine("// 17: Get the current minute");
            sb.AppendLine("int GetTimeMinute();");
            sb.AppendLine();

            // Action 18: GetTimeSecond
            sb.AppendLine("// 18: Get the current second");
            sb.AppendLine("int GetTimeSecond();");
            sb.AppendLine();

            // Action 19: GetTimeMillisecond
            sb.AppendLine("// 19: Get the current millisecond");
            sb.AppendLine("int GetTimeMillisecond();");
            sb.AppendLine();

            // Action 20: RandomWalk
            sb.AppendLine("// 20: The action subject will generate a random location near its current location");
            sb.AppendLine("void RandomWalk();");
            sb.AppendLine();

            // Action 21: ActionMoveToLocation
            sb.AppendLine("// 21: The action subject will move to lDestination.");
            sb.AppendLine("void ActionMoveToLocation(location lDestination, int bRun=FALSE);");
            sb.AppendLine();

            // Action 22: ActionMoveToObject
            sb.AppendLine("// 22: Cause the action subject to move to a certain distance from oMoveTo.");
            sb.AppendLine("void ActionMoveToObject(object oMoveTo, int bRun=FALSE, float fRange=1.0);");
            sb.AppendLine();

            // Action 23: ActionMoveAwayFromObject
            sb.AppendLine("// 23: Cause the action subject to move to a certain distance away from oFleeFrom.");
            sb.AppendLine("void ActionMoveAwayFromObject(object oFleeFrom, int bRun=FALSE, float fMoveAwayRange=40.0);");
            sb.AppendLine();

            // Action 24: GetArea
            sb.AppendLine("// 24: Get the area that oTarget is currently in");
            sb.AppendLine("object GetArea(object oTarget=OBJECT_SELF);");
            sb.AppendLine();

            // Action 25: GetEnteringObject
            sb.AppendLine("// 25: Get the object that last entered the caller.");
            sb.AppendLine("object GetEnteringObject();");
            sb.AppendLine();

            // Action 26: GetExitingObject
            sb.AppendLine("// 26: Get the object that last left the caller.");
            sb.AppendLine("object GetExitingObject();");
            sb.AppendLine();

            // Action 27: GetPosition
            sb.AppendLine("// 27: Get the position of oTarget");
            sb.AppendLine("location GetPosition(object oTarget=OBJECT_SELF);");
            sb.AppendLine();

            // Action 28: GetFacing
            sb.AppendLine("// 28: Get the direction in which oTarget is facing");
            sb.AppendLine("float GetFacing(object oTarget=OBJECT_SELF);");
            sb.AppendLine();

            // Action 29: GetItemPossessor
            sb.AppendLine("// 29: Get the possessor of oItem");
            sb.AppendLine("object GetItemPossessor(object oItem);");
            sb.AppendLine();

            // Action 30: GetItemPossessedBy
            sb.AppendLine("// 30: Get the object possessed by oCreature with the tag sItemTag");
            sb.AppendLine("object GetItemPossessedBy(object oCreature, string sItemTag);");
            sb.AppendLine();

            // Action 31: CreateItemOnObject
            sb.AppendLine("// 31: Create an item with the template sItemTemplate in oTarget's inventory.");
            sb.AppendLine("object CreateItemOnObject(string sItemTemplate, object oTarget, int nStackSize=1);");
            sb.AppendLine();

            // Action 32: ActionEquipItem
            sb.AppendLine("// 32: Equip oItem into nInventorySlot.");
            sb.AppendLine("void ActionEquipItem(object oItem, int nInventorySlot);");
            sb.AppendLine();

            // Action 33: ActionUnequipItem
            sb.AppendLine("// 33: Unequip oItem from whatever slot it is currently in.");
            sb.AppendLine("void ActionUnequipItem(object oItem);");
            sb.AppendLine();

            // Action 34: ActionPickUpItem
            sb.AppendLine("// 34: Pick up oItem from the ground.");
            sb.AppendLine("void ActionPickUpItem(object oItem);");
            sb.AppendLine();

            // Action 35: ActionPutDownItem
            sb.AppendLine("// 35: Put down oItem on the ground.");
            sb.AppendLine("void ActionPutDownItem(object oItem);");
            sb.AppendLine();

            // Action 36: GetLastAttacker
            sb.AppendLine("// 36: Get the last attacker of oAttackee.");
            sb.AppendLine("object GetLastAttacker(object oAttackee=OBJECT_SELF);");
            sb.AppendLine();

            // Action 37: ActionAttack
            sb.AppendLine("// 37: Attack oAttackee.");
            sb.AppendLine("void ActionAttack(object oAttackee, int bPassive=FALSE);");
            sb.AppendLine();

            // Action 38: GetNearestCreature
            sb.AppendLine("// 38: Get the creature nearest to oTarget");
            sb.AppendLine("object GetNearestCreature(int nFirstCriteriaType, int nFirstCriteriaValue, object oTarget=OBJECT_SELF, int nNth=1, int nSecondCriteriaType=-1, int nSecondCriteriaValue=-1, float fRange=30.0);");
            sb.AppendLine();

            // Action 39: ActionSpeakString
            sb.AppendLine("// 39: Add a speak action to the action subject.");
            sb.AppendLine("void ActionSpeakString(string sStringToSpeak, int nTalkVolume=TALKVOLUME_TALK);");
            sb.AppendLine();

            // Action 40: ActionPlayAnimation
            sb.AppendLine("// 40: Cause the action subject to play an animation");
            sb.AppendLine("void ActionPlayAnimation(int nAnimation, float fSpeed=1.0, float fDurationSeconds=0.0);");
            sb.AppendLine();

            // Action 41: GetDistanceToObject
            sb.AppendLine("// 41: Get the distance from the caller to oObject in metres.");
            sb.AppendLine("float GetDistanceToObject(object oObject);");
            sb.AppendLine();

            // Action 42: GetIsObjectValid
            sb.AppendLine("// 42: Returns TRUE if oObject is a valid object.");
            sb.AppendLine("int GetIsObjectValid(object oObject);");
            sb.AppendLine();

            // Action 43: ActionOpenDoor
            sb.AppendLine("// 43: Cause the action subject to open oDoor");
            sb.AppendLine("void ActionOpenDoor(object oDoor);");
            sb.AppendLine();

            // Action 44: ActionCloseDoor
            sb.AppendLine("// 44: Cause the action subject to close oDoor");
            sb.AppendLine("void ActionCloseDoor(object oDoor);");
            sb.AppendLine();

            // Action 45: ActionUseObject
            sb.AppendLine("// 45: Cause the action subject to use oPlaceable");
            sb.AppendLine("void ActionUseObject(object oPlaceable);");
            sb.AppendLine();

            // Action 46: GetTag
            sb.AppendLine("// 46: Get the tag of oObject");
            sb.AppendLine("string GetTag(object oObject);");
            sb.AppendLine();

            // Action 47: GetName
            sb.AppendLine("// 47: Get the name of oObject");
            sb.AppendLine("string GetName(object oObject);");
            sb.AppendLine();

            // Action 48: GetIsDead
            sb.AppendLine("// 48: Returns TRUE if oCreature is dead");
            sb.AppendLine("int GetIsDead(object oCreature);");
            sb.AppendLine();

            // Action 49: GetHitDice
            sb.AppendLine("// 49: Get the hit dice of oCreature");
            sb.AppendLine("int GetHitDice(object oCreature);");
            sb.AppendLine();

            // Action 50: GetLastPerceived
            sb.AppendLine("// 50: Get the last object that was perceived by the caller");
            sb.AppendLine("object GetLastPerceived();");
            sb.AppendLine();

            return sb.ToString();
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public void FileDecompiler_Constructor_ShouldNotThrow()
        {
            // Arrange & Act
            Action act = () => new FileDecompiler();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void FileDecompiler_Constructor_WithSettings_ShouldNotThrow()
        {
            // Arrange & Act
            Action act = () => new FileDecompiler(_settings, NWScriptLocator.GameType.K1);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Decompile_WithNonExistentFile_ShouldReturnFailure()
        {
            // Arrange
            NcsFile nonExistentFile = new NcsFile(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            int result = _decompiler.Decompile(nonExistentFile);

            // Assert
            result.Should().Be(FileDecompiler.FAILURE);
        }

        [Fact]
        public void GetVariableData_WithNonExistentFile_ShouldReturnNull()
        {
            // Arrange
            NcsFile nonExistentFile = new NcsFile(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            var result = _decompiler.GetVariableData(nonExistentFile);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetGeneratedCode_WithNonExistentFile_ShouldReturnNull()
        {
            // Arrange
            NcsFile nonExistentFile = new NcsFile(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            string result = _decompiler.GetGeneratedCode(nonExistentFile);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetOriginalByteCode_WithNonExistentFile_ShouldReturnNull()
        {
            // Arrange
            NcsFile nonExistentFile = new NcsFile(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            string result = _decompiler.GetOriginalByteCode(nonExistentFile);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetNewByteCode_WithNonExistentFile_ShouldReturnNull()
        {
            // Arrange
            NcsFile nonExistentFile = new NcsFile(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            string result = _decompiler.GetNewByteCode(nonExistentFile);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void UpdateSubName_WithNonExistentFile_ShouldReturnNull()
        {
            // Arrange
            NcsFile nonExistentFile = new NcsFile(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            var result = _decompiler.UpdateSubName(nonExistentFile, "old", "new");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void RegenerateCode_WithNonExistentFile_ShouldReturnNull()
        {
            // Arrange
            NcsFile nonExistentFile = new NcsFile(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            string result = _decompiler.RegenerateCode(nonExistentFile);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void CloseFile_WithNonExistentFile_ShouldNotThrow()
        {
            // Arrange
            NcsFile nonExistentFile = new NcsFile(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            Action act = () => _decompiler.CloseFile(nonExistentFile);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void CloseAllFiles_ShouldNotThrow()
        {
            // Act
            Action act = () => _decompiler.CloseAllFiles();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void CompileAndCompare_WithNonExistentFile_ShouldNotThrow()
        {
            // Arrange
            NcsFile nonExistentFile = new NcsFile(Path.Combine(_tempDir, "nonexistent.ncs"));
            NcsFile newFile = new NcsFile(Path.Combine(_tempDir, "new.nss"));

            // Act
            Action act = () => _decompiler.CompileAndCompare(nonExistentFile, newFile);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void CompileOnly_WithNonExistentFile_ShouldNotThrow()
        {
            // Arrange
            NcsFile nonExistentFile = new NcsFile(Path.Combine(_tempDir, "nonexistent.nss"));

            // Act
            Action act = () => _decompiler.CompileOnly(nonExistentFile);

            // Assert
            act.Should().NotThrow();
        }
    }
}

