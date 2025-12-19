using System;
using System.IO;
using Andastra.Runtime.Core.Save;
using Andastra.Runtime.Engines.Eclipse.Save;

namespace Andastra.Runtime.Engines.Eclipse.MassEffect2.Save
{
    /// <summary>
    /// Save serializer for Mass Effect 2 (.pcsave save files).
    /// </summary>
    /// <remarks>
    /// Mass Effect 2 Save Format:
    /// - Based on MassEffect2.exe: Save system (similar to ME1 but with differences)
    /// - Located via string references: Save system functions
    /// - Save file format: Binary format with signature "MES2" (Mass Effect Save 2)
    /// - Version: 1 (int32)
    /// - Structure: Signature (4 bytes) -> Version (4 bytes) -> Metadata -> Game State
    /// - Inheritance: Base class EclipseSaveSerializer (Runtime.Engines.Eclipse.Save) - abstract save serializer, MassEffect2 override - ME2 save format
    /// - Original implementation: UnrealScript message-based save system, binary serialization
    /// - Note: Mass Effect 2 uses .pcsave file extension, format may differ from ME1
    /// </remarks>
    public class MassEffect2SaveSerializer : EclipseSaveSerializer
    {
        private const string SaveSignature = "MES2";
        private const int SaveVersion = 1;

        /// <summary>
        /// Serializes save metadata to NFO format (Mass Effect 2-specific).
        /// </summary>
        public override byte[] SerializeSaveNfo(SaveGameData saveData)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                // Write signature
                writer.Write(System.Text.Encoding.UTF8.GetBytes(SaveSignature));

                // Write version
                writer.Write(SaveVersion);

                // Write common metadata
                WriteCommonMetadata(writer, saveData);

                // Mass Effect 2-specific metadata fields
                // Based on MassEffect2.exe: Save system structure
                // Serializes metadata for save list display and quick loading
                
                // Player character name
                WriteString(writer, saveData.PlayerName ?? "");
                
                // Current area name
                WriteString(writer, saveData.CurrentAreaName ?? "");
                
                // Player level (from PartyState.PlayerCharacter)
                int playerLevel = saveData.PartyState?.PlayerCharacter?.Level ?? 0;
                writer.Write(playerLevel);
                
                // Squad member count (Mass Effect 2 uses "squad" instead of "party")
                int squadMemberCount = saveData.PartyState?.SelectedParty?.Count ?? 0;
                writer.Write(squadMemberCount);
                
                // Character class (Mass Effect 2 classes: Soldier, Adept, Engineer, Sentinel, Infiltrator, Vanguard)
                // Extract from PartyState.PlayerCharacter.ClassLevels - get the primary class (highest level)
                string characterClass = GetCharacterClassName(saveData.PartyState?.PlayerCharacter);
                WriteString(writer, characterClass);
                
                // Mission count (number of active missions/quests)
                int missionCount = saveData.JournalEntries != null ? saveData.JournalEntries.Count : 0;
                writer.Write(missionCount);
                
                // Difficulty level
                // Mass Effect 2 has difficulty settings: 0=Casual, 1=Normal, 2=Veteran, 3=Hardcore, 4=Insanity
                // Note: Difficulty is not currently stored in SaveGameData structure, so we write 0 as default
                // If difficulty tracking is needed, add DifficultyLevel field to SaveGameData
                int difficultyLevel = 0;
                writer.Write(difficultyLevel);

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes save metadata from NFO format (Mass Effect 2-specific).
        /// </summary>
        public override SaveGameData DeserializeSaveNfo(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Save data cannot be null or empty", nameof(data));
            }

            var saveData = new SaveGameData();

            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                // Validate signature
                ValidateSignature(reader, SaveSignature);

                // Validate version
                ValidateVersion(reader, SaveVersion, "Mass Effect 2");

                // Read common metadata
                ReadCommonMetadata(reader, saveData);

                // Mass Effect 2-specific metadata fields
                // Based on MassEffect2.exe: Save system structure
                // Deserializes metadata for save list display and quick loading
                
                // Player character name
                saveData.PlayerName = ReadString(reader);
                
                // Current area name
                saveData.CurrentAreaName = ReadString(reader);
                
                // Player level
                int playerLevel = reader.ReadInt32();
                
                // Squad member count
                int squadMemberCount = reader.ReadInt32();
                
                // Character class
                string characterClass = ReadString(reader);
                
                // Mission count
                int missionCount = reader.ReadInt32();
                
                // Difficulty level
                int difficultyLevel = reader.ReadInt32();
                
                // Initialize PartyState if needed
                if (saveData.PartyState == null)
                {
                    saveData.PartyState = new Core.Save.PartyState();
                }
                
                // Set player level if PlayerCharacter exists
                if (saveData.PartyState.PlayerCharacter == null)
                {
                    saveData.PartyState.PlayerCharacter = new Core.Save.CreatureState();
                }
                saveData.PartyState.PlayerCharacter.Level = playerLevel;
                
                // Initialize SelectedParty if needed and set count (actual members loaded in full deserialization)
                if (saveData.PartyState.SelectedParty == null)
                {
                    saveData.PartyState.SelectedParty = new System.Collections.Generic.List<string>();
                }
                
                // Initialize JournalEntries if needed (count is known from metadata)
                if (saveData.JournalEntries == null)
                {
                    saveData.JournalEntries = new System.Collections.Generic.List<Core.Save.JournalEntry>();
                }
            }

            return saveData;
        }

        /// <summary>
        /// Serializes full save archive (Mass Effect 2-specific).
        /// </summary>
        public override byte[] SerializeSaveArchive(SaveGameData saveData)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                // Write signature
                writer.Write(System.Text.Encoding.UTF8.GetBytes(SaveSignature));

                // Write version
                writer.Write(SaveVersion);

                // Write common metadata
                WriteCommonMetadata(writer, saveData);

                // TODO: Serialize full game state
                // Based on MassEffect2.exe: Save system serialization
                // Includes: Squad state, inventory, missions, world state, etc.

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes full save archive (Mass Effect 2-specific).
        /// </summary>
        public override void DeserializeSaveArchive(byte[] data, SaveGameData saveData)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Save data cannot be null or empty", nameof(data));
            }

            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }

            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                // Validate signature
                ValidateSignature(reader, SaveSignature);

                // Validate version
                ValidateVersion(reader, SaveVersion, "Mass Effect 2");

                // Read common metadata
                ReadCommonMetadata(reader, saveData);

                // Deserialize full game state
                // Based on MassEffect2.exe: Save system deserialization
                // Includes: Squad state, inventory, missions, world state, etc.
                DeserializeFullGameState(reader, saveData);
            }
        }

        #region Full Game State Serialization

        /// <summary>
        /// Deserializes the complete game state from save data.
        /// Based on MassEffect2.exe: Save system deserialization
        /// </summary>
        /// <remarks>
        /// Mass Effect 2 save format structure:
        /// - Entry position and facing
        /// - Current area name
        /// - Game time
        /// - Game version
        /// - Save type and number
        /// - Flags (cheat, gameplay hint, story hints, live content)
        /// - Player name and screenshot
        /// - Global variables (with Location support like ME1)
        /// - Party/squad state (comprehensive like ME1)
        /// - Area states
        /// - Journal entries (with DateAdded timestamp like ME1)
        /// </remarks>
        private void DeserializeFullGameState(BinaryReader reader, SaveGameData saveData)
        {
            // Entry position and facing
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            saveData.EntryPosition = new System.Numerics.Vector3(x, y, z);
            saveData.EntryFacing = reader.ReadSingle();

            // Current area name
            saveData.CurrentAreaName = ReadString(reader);

            // Game time
            int year = reader.ReadInt32();
            int month = reader.ReadInt32();
            int day = reader.ReadInt32();
            int hour = reader.ReadInt32();
            int minute = reader.ReadInt32();
            if (saveData.GameTime == null)
            {
                saveData.GameTime = new Core.Save.GameTime();
            }
            saveData.GameTime.Year = year;
            saveData.GameTime.Month = month;
            saveData.GameTime.Day = day;
            saveData.GameTime.Hour = hour;
            saveData.GameTime.Minute = minute;

            // Game version
            saveData.GameVersion = ReadString(reader);

            // Save type
            saveData.SaveType = (Core.Save.SaveType)reader.ReadInt32();

            // Save number
            saveData.SaveNumber = reader.ReadInt32();

            // Cheat used flag
            saveData.CheatUsed = reader.ReadInt32() != 0;

            // Gameplay hint flag
            saveData.GameplayHint = reader.ReadInt32() != 0;

            // Story hints (10 flags)
            int storyHintsCount = reader.ReadInt32();
            if (saveData.StoryHints == null)
            {
                saveData.StoryHints = new System.Collections.Generic.List<bool>();
            }
            saveData.StoryHints.Clear();
            for (int i = 0; i < 10; i++)
            {
                bool hintValue = reader.ReadInt32() != 0;
                saveData.StoryHints.Add(hintValue);
            }

            // Live content flags
            int liveContentCount = reader.ReadInt32();
            if (saveData.LiveContent == null)
            {
                saveData.LiveContent = new System.Collections.Generic.List<bool>();
            }
            saveData.LiveContent.Clear();
            for (int i = 0; i < liveContentCount; i++)
            {
                bool flag = reader.ReadInt32() != 0;
                saveData.LiveContent.Add(flag);
            }

            // Player name
            saveData.PlayerName = ReadString(reader);

            // Screenshot data
            int screenshotLength = reader.ReadInt32();
            if (screenshotLength > 0)
            {
                saveData.Screenshot = reader.ReadBytes(screenshotLength);
            }
            else
            {
                saveData.Screenshot = null;
            }

            // Deserialize global variables (Mass Effect 2 uses same format as ME1: includes Locations, uses int32 for booleans)
            saveData.GlobalVariables = DeserializeGlobalVariables(reader);

            // Deserialize party/squad state
            saveData.PartyState = DeserializePartyState(reader);

            // Deserialize area states
            saveData.AreaStates = DeserializeAreaStates(reader);

            // Deserialize journal entries (Mass Effect 2 includes DateAdded timestamp like ME1)
            saveData.JournalEntries = DeserializeJournalEntries(reader);
        }

        /// <summary>
        /// Deserializes global variable state (Mass Effect 2-specific: includes Locations, uses int32 for booleans).
        /// Based on MassEffect2.exe: Global variable deserialization
        /// </summary>
        /// <remarks>
        /// Mass Effect 2 uses int32 for boolean deserialization (unlike Dragon Age which uses bool).
        /// Mass Effect 2 also includes Location globals which are not in the base class implementation.
        /// </remarks>
        private Core.Save.GlobalVariableState DeserializeGlobalVariables(BinaryReader reader)
        {
            var globals = new Core.Save.GlobalVariableState();

            // Deserialize boolean globals
            int boolCount = reader.ReadInt32();
            for (int i = 0; i < boolCount; i++)
            {
                string name = ReadString(reader);
                bool value = reader.ReadInt32() != 0; // Mass Effect 2 uses int32 for booleans
                globals.Booleans[name] = value;
            }

            // Deserialize numeric globals
            int numberCount = reader.ReadInt32();
            for (int i = 0; i < numberCount; i++)
            {
                string name = ReadString(reader);
                int value = reader.ReadInt32();
                globals.Numbers[name] = value;
            }

            // Deserialize string globals
            int stringCount = reader.ReadInt32();
            for (int i = 0; i < stringCount; i++)
            {
                string name = ReadString(reader);
                string value = ReadString(reader);
                globals.Strings[name] = value;
            }

            // Deserialize location globals
            int locationCount = reader.ReadInt32();
            for (int i = 0; i < locationCount; i++)
            {
                string name = ReadString(reader);
                Core.Save.SavedLocation location = DeserializeSavedLocation(reader);
                globals.Locations[name] = location;
            }

            return globals;
        }

        /// <summary>
        /// Deserializes party/squad state.
        /// Based on MassEffect2.exe: Squad state deserialization
        /// </summary>
        private Core.Save.PartyState DeserializePartyState(BinaryReader reader)
        {
            var partyState = new Core.Save.PartyState();

            // Deserialize player character
            bool hasPlayer = reader.ReadInt32() != 0;
            if (hasPlayer)
            {
                partyState.PlayerCharacter = DeserializeCreatureState(reader);
            }

            // Deserialize available party members
            int availableCount = reader.ReadInt32();
            for (int i = 0; i < availableCount; i++)
            {
                string templateResRef = ReadString(reader);
                bool isAvailable = reader.ReadInt32() != 0;
                bool isSelectable = reader.ReadInt32() != 0;
                bool hasState = reader.ReadInt32() != 0;
                Core.Save.CreatureState state = null;
                if (hasState)
                {
                    state = DeserializeCreatureState(reader);
                }

                var memberState = new Core.Save.PartyMemberState
                {
                    TemplateResRef = templateResRef,
                    IsAvailable = isAvailable,
                    IsSelectable = isSelectable,
                    State = state
                };
                partyState.AvailableMembers[templateResRef] = memberState;
            }

            // Deserialize selected party
            int selectedCount = reader.ReadInt32();
            for (int i = 0; i < selectedCount; i++)
            {
                string memberResRef = ReadString(reader);
                partyState.SelectedParty.Add(memberResRef);
            }

            // Deserialize party resources
            partyState.Gold = reader.ReadInt32();
            partyState.ExperiencePoints = reader.ReadInt32();

            // Deserialize influence values
            int influenceCount = reader.ReadInt32();
            for (int i = 0; i < influenceCount; i++)
            {
                partyState.Influence.Add(reader.ReadInt32());
            }

            // Deserialize other party state
            partyState.ItemComponent = reader.ReadInt32();
            partyState.ItemChemical = reader.ReadInt32();
            partyState.Swoop1 = reader.ReadInt32();
            partyState.Swoop2 = reader.ReadInt32();
            partyState.Swoop3 = reader.ReadInt32();
            partyState.PlayTime = System.TimeSpan.FromSeconds(reader.ReadInt32());
            partyState.ControlledNPC = reader.ReadInt32();
            partyState.SoloMode = reader.ReadInt32() != 0;
            partyState.CheatUsed = reader.ReadInt32() != 0;
            partyState.LeaderResRef = ReadString(reader);

            // Deserialize puppets
            int puppetCount = reader.ReadInt32();
            for (int i = 0; i < puppetCount; i++)
            {
                partyState.Puppets.Add(reader.ReadUInt32());
            }

            // Deserialize available puppets
            int availablePuppetCount = reader.ReadInt32();
            for (int i = 0; i < availablePuppetCount; i++)
            {
                partyState.AvailablePuppets.Add(reader.ReadInt32() != 0);
            }

            // Deserialize selectable puppets
            int selectablePuppetCount = reader.ReadInt32();
            for (int i = 0; i < selectablePuppetCount; i++)
            {
                partyState.SelectablePuppets.Add(reader.ReadInt32() != 0);
            }

            partyState.AIState = reader.ReadInt32();
            partyState.FollowState = reader.ReadInt32();
            partyState.GalaxyMapPlanetMask = reader.ReadInt32();
            partyState.GalaxyMapSelectedPoint = reader.ReadInt32();

            // Deserialize Pazaak cards
            int pazaakCardCount = reader.ReadInt32();
            for (int i = 0; i < pazaakCardCount; i++)
            {
                partyState.PazaakCards.Add(reader.ReadInt32());
            }

            // Deserialize Pazaak side list
            int pazaakSideCount = reader.ReadInt32();
            for (int i = 0; i < pazaakSideCount; i++)
            {
                partyState.PazaakSideList.Add(reader.ReadInt32());
            }

            // Deserialize tutorial windows shown
            int tutorialCount = reader.ReadInt32();
            for (int i = 0; i < tutorialCount; i++)
            {
                partyState.TutorialWindowsShown.Add(reader.ReadInt32() != 0);
            }

            partyState.LastGUIPanel = reader.ReadInt32();
            partyState.DisableMap = reader.ReadInt32() != 0;
            partyState.DisableRegen = reader.ReadInt32() != 0;

            // Deserialize feedback messages
            int feedbackCount = reader.ReadInt32();
            for (int i = 0; i < feedbackCount; i++)
            {
                partyState.FeedbackMessages.Add(new Core.Save.FeedbackMessage
                {
                    Message = ReadString(reader),
                    Type = reader.ReadInt32(),
                    Color = reader.ReadByte()
                });
            }

            // Deserialize dialogue messages
            int dialogueCount = reader.ReadInt32();
            for (int i = 0; i < dialogueCount; i++)
            {
                partyState.DialogueMessages.Add(new Core.Save.DialogueMessage
                {
                    Speaker = ReadString(reader),
                    Message = ReadString(reader)
                });
            }

            // Deserialize combat messages
            int combatCount = reader.ReadInt32();
            for (int i = 0; i < combatCount; i++)
            {
                partyState.CombatMessages.Add(new Core.Save.CombatMessage
                {
                    Message = ReadString(reader),
                    Type = reader.ReadInt32(),
                    Color = reader.ReadByte()
                });
            }

            // Deserialize cost multipliers
            int costMultiplierCount = reader.ReadInt32();
            for (int i = 0; i < costMultiplierCount; i++)
            {
                partyState.CostMultipliers.Add(reader.ReadSingle());
            }

            return partyState;
        }

        /// <summary>
        /// Deserializes creature state (player character or party member).
        /// Based on MassEffect2.exe: Creature state deserialization
        /// </summary>
        private Core.Save.CreatureState DeserializeCreatureState(BinaryReader reader)
        {
            bool hasCreature = reader.ReadInt32() != 0;
            if (!hasCreature)
            {
                return null;
            }

            var creature = new Core.Save.CreatureState();

            // Deserialize base entity state
            DeserializeEntityState(reader, creature);

            // Deserialize creature-specific fields
            creature.Level = reader.ReadInt32();
            creature.XP = reader.ReadInt32();
            creature.CurrentFP = reader.ReadInt32();
            creature.MaxFP = reader.ReadInt32();
            creature.Alignment = reader.ReadInt32();

            // Deserialize equipment
            creature.Equipment = DeserializeEquipmentState(reader);

            // Deserialize inventory
            int inventoryCount = reader.ReadInt32();
            for (int i = 0; i < inventoryCount; i++)
            {
                creature.Inventory.Add(DeserializeItemState(reader));
            }

            // Deserialize known powers
            int powersCount = reader.ReadInt32();
            for (int i = 0; i < powersCount; i++)
            {
                creature.KnownPowers.Add(ReadString(reader));
            }

            // Deserialize known feats
            int featsCount = reader.ReadInt32();
            for (int i = 0; i < featsCount; i++)
            {
                creature.KnownFeats.Add(ReadString(reader));
            }

            // Deserialize class levels
            int classLevelsCount = reader.ReadInt32();
            for (int i = 0; i < classLevelsCount; i++)
            {
                var classLevel = new Core.Save.ClassLevel
                {
                    ClassId = reader.ReadInt32(),
                    Level = reader.ReadInt32()
                };
                int powersGainedCount = reader.ReadInt32();
                for (int j = 0; j < powersGainedCount; j++)
                {
                    classLevel.PowersGained.Add(ReadString(reader));
                }
                creature.ClassLevels.Add(classLevel);
            }

            // Deserialize skills
            int skillsCount = reader.ReadInt32();
            for (int i = 0; i < skillsCount; i++)
            {
                string skillName = ReadString(reader);
                int skillValue = reader.ReadInt32();
                creature.Skills[skillName] = skillValue;
            }

            // Deserialize attributes
            bool hasAttributes = reader.ReadInt32() != 0;
            if (hasAttributes)
            {
                creature.Attributes = new Core.Save.AttributeSet
                {
                    Strength = reader.ReadInt32(),
                    Dexterity = reader.ReadInt32(),
                    Constitution = reader.ReadInt32(),
                    Intelligence = reader.ReadInt32(),
                    Wisdom = reader.ReadInt32(),
                    Charisma = reader.ReadInt32()
                };
            }

            return creature;
        }

        /// <summary>
        /// Deserializes entity state (base class for all entities).
        /// Based on MassEffect2.exe: Entity state deserialization
        /// </summary>
        private void DeserializeEntityState(BinaryReader reader, Core.Save.EntityState entity)
        {
            bool hasEntity = reader.ReadInt32() != 0;
            if (!hasEntity)
            {
                return;
            }

            entity.Tag = ReadString(reader);
            entity.ObjectId = reader.ReadUInt32();
            entity.ObjectType = (Core.Enums.ObjectType)reader.ReadInt32();
            entity.TemplateResRef = ReadString(reader);
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            entity.Position = new System.Numerics.Vector3(x, y, z);
            entity.Facing = reader.ReadSingle();
            entity.CurrentHP = reader.ReadInt32();
            entity.MaxHP = reader.ReadInt32();
            entity.IsDestroyed = reader.ReadInt32() != 0;
            entity.IsPlot = reader.ReadInt32() != 0;
            entity.IsOpen = reader.ReadInt32() != 0;
            entity.IsLocked = reader.ReadInt32() != 0;
            entity.AnimationState = reader.ReadInt32();

            // Deserialize local variables
            entity.LocalVariables = DeserializeLocalVariableSet(reader);

            // Deserialize active effects
            int effectsCount = reader.ReadInt32();
            for (int i = 0; i < effectsCount; i++)
            {
                entity.ActiveEffects.Add(DeserializeSavedEffect(reader));
            }
        }

        /// <summary>
        /// Deserializes equipment state.
        /// Based on MassEffect2.exe: Equipment state deserialization
        /// </summary>
        private Core.Save.EquipmentState DeserializeEquipmentState(BinaryReader reader)
        {
            bool hasEquipment = reader.ReadInt32() != 0;
            if (!hasEquipment)
            {
                return new Core.Save.EquipmentState();
            }

            return new Core.Save.EquipmentState
            {
                Head = DeserializeItemState(reader),
                Armor = DeserializeItemState(reader),
                Gloves = DeserializeItemState(reader),
                RightHand = DeserializeItemState(reader),
                LeftHand = DeserializeItemState(reader),
                Belt = DeserializeItemState(reader),
                Implant = DeserializeItemState(reader),
                RightArm = DeserializeItemState(reader),
                LeftArm = DeserializeItemState(reader)
            };
        }

        /// <summary>
        /// Deserializes item state.
        /// Based on MassEffect2.exe: Item state deserialization
        /// </summary>
        private Core.Save.ItemState DeserializeItemState(BinaryReader reader)
        {
            bool hasItem = reader.ReadInt32() != 0;
            if (!hasItem)
            {
                return null;
            }

            var item = new Core.Save.ItemState
            {
                TemplateResRef = ReadString(reader),
                StackSize = reader.ReadInt32(),
                Charges = reader.ReadInt32(),
                Identified = reader.ReadInt32() != 0
            };

            // Deserialize upgrades
            int upgradesCount = reader.ReadInt32();
            for (int i = 0; i < upgradesCount; i++)
            {
                item.Upgrades.Add(new Core.Save.ItemUpgrade
                {
                    UpgradeSlot = reader.ReadInt32(),
                    UpgradeResRef = ReadString(reader)
                });
            }

            return item;
        }

        /// <summary>
        /// Deserializes local variable set.
        /// Based on MassEffect2.exe: Local variable deserialization
        /// </summary>
        private Core.Save.LocalVariableSet DeserializeLocalVariableSet(BinaryReader reader)
        {
            bool hasLocalVars = reader.ReadInt32() != 0;
            if (!hasLocalVars)
            {
                return new Core.Save.LocalVariableSet();
            }

            var localVars = new Core.Save.LocalVariableSet();

            // Deserialize integer variables
            int intCount = reader.ReadInt32();
            for (int i = 0; i < intCount; i++)
            {
                string name = ReadString(reader);
                int value = reader.ReadInt32();
                localVars.Ints[name] = value;
            }

            // Deserialize float variables
            int floatCount = reader.ReadInt32();
            for (int i = 0; i < floatCount; i++)
            {
                string name = ReadString(reader);
                float value = reader.ReadSingle();
                localVars.Floats[name] = value;
            }

            // Deserialize string variables
            int stringCount = reader.ReadInt32();
            for (int i = 0; i < stringCount; i++)
            {
                string name = ReadString(reader);
                string value = ReadString(reader);
                localVars.Strings[name] = value;
            }

            // Deserialize object reference variables
            int objectCount = reader.ReadInt32();
            for (int i = 0; i < objectCount; i++)
            {
                string name = ReadString(reader);
                uint value = reader.ReadUInt32();
                localVars.Objects[name] = value;
            }

            // Deserialize location variables
            int locationCount = reader.ReadInt32();
            for (int i = 0; i < locationCount; i++)
            {
                string name = ReadString(reader);
                Core.Save.SavedLocation location = DeserializeSavedLocation(reader);
                localVars.Locations[name] = location;
            }

            return localVars;
        }

        /// <summary>
        /// Deserializes saved effect.
        /// Based on MassEffect2.exe: Effect deserialization
        /// </summary>
        private Core.Save.SavedEffect DeserializeSavedEffect(BinaryReader reader)
        {
            bool hasEffect = reader.ReadInt32() != 0;
            if (!hasEffect)
            {
                return null;
            }

            var effect = new Core.Save.SavedEffect
            {
                EffectType = reader.ReadInt32(),
                SubType = reader.ReadInt32(),
                DurationType = reader.ReadInt32(),
                RemainingDuration = reader.ReadSingle(),
                CreatorId = reader.ReadUInt32(),
                SpellId = reader.ReadInt32()
            };

            // Deserialize parameters
            int intParamCount = reader.ReadInt32();
            for (int i = 0; i < intParamCount; i++)
            {
                effect.IntParams.Add(reader.ReadInt32());
            }

            int floatParamCount = reader.ReadInt32();
            for (int i = 0; i < floatParamCount; i++)
            {
                effect.FloatParams.Add(reader.ReadSingle());
            }

            int stringParamCount = reader.ReadInt32();
            for (int i = 0; i < stringParamCount; i++)
            {
                effect.StringParams.Add(ReadString(reader));
            }

            int objectParamCount = reader.ReadInt32();
            for (int i = 0; i < objectParamCount; i++)
            {
                effect.ObjectParams.Add(reader.ReadUInt32());
            }

            return effect;
        }

        /// <summary>
        /// Deserializes saved location.
        /// Based on MassEffect2.exe: Location deserialization
        /// </summary>
        private Core.Save.SavedLocation DeserializeSavedLocation(BinaryReader reader)
        {
            bool hasLocation = reader.ReadInt32() != 0;
            if (!hasLocation)
            {
                return null;
            }

            var location = new Core.Save.SavedLocation
            {
                AreaResRef = ReadString(reader)
            };
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            location.Position = new System.Numerics.Vector3(x, y, z);
            location.Facing = reader.ReadSingle();

            return location;
        }

        /// <summary>
        /// Deserializes area states dictionary.
        /// Based on MassEffect2.exe: Area states deserialization
        /// </summary>
        private System.Collections.Generic.Dictionary<string, Core.Save.AreaState> DeserializeAreaStates(BinaryReader reader)
        {
            var areaStates = new System.Collections.Generic.Dictionary<string, Core.Save.AreaState>();

            int areaCount = reader.ReadInt32();
            for (int i = 0; i < areaCount; i++)
            {
                string areaResRef = ReadString(reader);
                Core.Save.AreaState areaState = DeserializeAreaState(reader);
                areaStates[areaResRef] = areaState;
            }

            return areaStates;
        }

        /// <summary>
        /// Deserializes a single area state.
        /// Based on MassEffect2.exe: Area state deserialization
        /// </summary>
        private Core.Save.AreaState DeserializeAreaState(BinaryReader reader)
        {
            bool hasAreaState = reader.ReadInt32() != 0;
            if (!hasAreaState)
            {
                return null;
            }

            var areaState = new Core.Save.AreaState
            {
                AreaResRef = ReadString(reader),
                Visited = reader.ReadInt32() != 0
            };

            // Deserialize entity state lists
            areaState.CreatureStates = DeserializeEntityStateList(reader);
            areaState.DoorStates = DeserializeEntityStateList(reader);
            areaState.PlaceableStates = DeserializeEntityStateList(reader);
            areaState.TriggerStates = DeserializeEntityStateList(reader);
            areaState.StoreStates = DeserializeEntityStateList(reader);
            areaState.SoundStates = DeserializeEntityStateList(reader);
            areaState.WaypointStates = DeserializeEntityStateList(reader);
            areaState.EncounterStates = DeserializeEntityStateList(reader);
            areaState.CameraStates = DeserializeEntityStateList(reader);

            // Deserialize destroyed entity IDs
            int destroyedCount = reader.ReadInt32();
            for (int i = 0; i < destroyedCount; i++)
            {
                areaState.DestroyedEntityIds.Add(reader.ReadUInt32());
            }

            // Deserialize spawned entities
            int spawnedCount = reader.ReadInt32();
            for (int i = 0; i < spawnedCount; i++)
            {
                var spawned = new Core.Save.SpawnedEntityState();
                DeserializeEntityState(reader, spawned);
                spawned.BlueprintResRef = ReadString(reader);
                spawned.SpawnedBy = ReadString(reader);
                areaState.SpawnedEntities.Add(spawned);
            }

            // Deserialize local variables
            int localVarCount = reader.ReadInt32();
            for (int i = 0; i < localVarCount; i++)
            {
                string key = ReadString(reader);
                object value = DeserializeObjectValue(reader);
                areaState.LocalVariables[key] = value;
            }

            return areaState;
        }

        /// <summary>
        /// Deserializes a list of entity states.
        /// Based on MassEffect2.exe: Entity state list deserialization
        /// </summary>
        private System.Collections.Generic.List<Core.Save.EntityState> DeserializeEntityStateList(BinaryReader reader)
        {
            var entityStates = new System.Collections.Generic.List<Core.Save.EntityState>();

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var entity = new Core.Save.EntityState();
                DeserializeEntityState(reader, entity);
                entityStates.Add(entity);
            }

            return entityStates;
        }

        /// <summary>
        /// Deserializes an object value (for local variables dictionary).
        /// Based on MassEffect2.exe: Object value deserialization
        /// </summary>
        private object DeserializeObjectValue(BinaryReader reader)
        {
            byte type = reader.ReadByte();
            switch (type)
            {
                case 0: // null
                    return null;
                case 1: // int
                    return reader.ReadInt32();
                case 2: // float
                    return reader.ReadSingle();
                case 3: // string
                    return ReadString(reader);
                case 4: // bool
                    return reader.ReadInt32() != 0;
                case 5: // uint
                    return reader.ReadUInt32();
                default:
                    throw new InvalidDataException($"Unknown object value type: {type}");
            }
        }

        /// <summary>
        /// Deserializes journal entries (Mass Effect 2-specific: includes DateAdded timestamp).
        /// Based on MassEffect2.exe: Journal entry deserialization
        /// </summary>
        /// <remarks>
        /// Mass Effect 2 includes DateAdded timestamp in journal entries, unlike the base class implementation.
        /// </remarks>
        private System.Collections.Generic.List<Core.Save.JournalEntry> DeserializeJournalEntries(BinaryReader reader)
        {
            var journalEntries = new System.Collections.Generic.List<Core.Save.JournalEntry>();

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                journalEntries.Add(new Core.Save.JournalEntry
                {
                    QuestTag = ReadString(reader),
                    State = reader.ReadInt32(),
                    DateAdded = System.DateTime.FromFileTime(reader.ReadInt64())
                });
            }

            return journalEntries;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the character class name from CreatureState.ClassLevels.
        /// Based on Mass Effect 2 class system: extracts primary class (highest level) from ClassLevels.
        /// </summary>
        /// <param name="creature">The creature state to extract class from.</param>
        /// <returns>Class name string, or empty string if no class information available.</returns>
        /// <remarks>
        /// Mass Effect 2 Class IDs:
        /// - 0 or 1: Soldier
        /// - 2: Adept
        /// - 3: Engineer
        /// - 4: Sentinel
        /// - 5: Infiltrator
        /// - 6: Vanguard
        /// </remarks>
        private string GetCharacterClassName(Core.Save.CreatureState creature)
        {
            if (creature == null || creature.ClassLevels == null || creature.ClassLevels.Count == 0)
            {
                return "";
            }

            // Find the class with the highest level (primary class)
            Core.Save.ClassLevel primaryClass = null;
            int maxLevel = -1;
            foreach (var classLevel in creature.ClassLevels)
            {
                if (classLevel.Level > maxLevel)
                {
                    maxLevel = classLevel.Level;
                    primaryClass = classLevel;
                }
            }

            if (primaryClass == null)
            {
                return "";
            }

            // Convert ClassId to class name
            return GetClassNameFromId(primaryClass.ClassId);
        }

        /// <summary>
        /// Converts a Mass Effect 2 class ID to class name string.
        /// Based on MassEffect2.exe: Class ID to name mapping.
        /// </summary>
        /// <param name="classId">The class ID to convert.</param>
        /// <returns>Class name string corresponding to the ID.</returns>
        /// <remarks>
        /// Mass Effect 2 Class ID mapping:
        /// - 0, 1: Soldier
        /// - 2: Adept
        /// - 3: Engineer
        /// - 4: Sentinel
        /// - 5: Infiltrator
        /// - 6: Vanguard
        /// </remarks>
        private string GetClassNameFromId(int classId)
        {
            switch (classId)
            {
                case 0:
                case 1:
                    return "Soldier";
                case 2:
                    return "Adept";
                case 3:
                    return "Engineer";
                case 4:
                    return "Sentinel";
                case 5:
                    return "Infiltrator";
                case 6:
                    return "Vanguard";
                default:
                    // Unknown class ID - return as string for extensibility
                    return $"Class_{classId}";
            }
        }

        #endregion
    }
}

