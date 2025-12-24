using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Installation;
using Andastra.Runtime.Core;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Engines.Odyssey.Components;
using Andastra.Runtime.Engines.Odyssey.Data;
using Andastra.Runtime.Engines.Odyssey.Systems;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.Games.Odyssey.Components;
using Andastra.Runtime.Games.Odyssey.Systems;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common;
using JetBrains.Annotations;
using GraphicsColor = Andastra.Runtime.Graphics.Color;
using GraphicsVector2 = Andastra.Runtime.Graphics.Vector2;
using ParsingColor = Andastra.Parsing.Common.Color;
using ParsingObjectType = Andastra.Parsing.Common.ParsingObjectType;
using RuntimeObjectType = Andastra.Runtime.Core.Enums.ObjectType;

namespace Andastra.Runtime.Game.Core
{
    /// <summary>
    /// Character creation screen for KOTOR 1 and KOTOR 2.
    /// </summary>
    /// <remarks>
    /// Character Creation Screen:
    /// - Based on swkotor.exe and swkotor2.exe character generation system
    /// - GUI Panel: "maincg" (character generation)
    /// - K1 Music: "mus_theme_rep", K2 Music: "mus_main"
    /// - Load Screen: K1 uses "load_chargen", K2 uses "load_default"
    /// - Flow: Main Menu → Character Creation → Module Load
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Character generation functions
    /// - swkotor2.exe: Character generation functions
    /// - vendor/reone: CharacterGeneration class implementation
    ///
    /// Character Creation Steps:
    /// 1. Class Selection (Scout, Soldier, Scoundrel for K1; Jedi Guardian, Jedi Sentinel, Jedi Consular for K2)
    /// 2. Quick or Custom (Quick uses defaults, Custom allows full customization)
    /// 3. Attributes (STR, DEX, CON, INT, WIS, CHA)
    /// 4. Skills (based on class and INT)
    /// 5. Feats (based on class)
    /// 6. Portrait Selection
    /// 7. Name Entry
    /// 8. Finish → Create Player Entity → Load Module
    /// </remarks>
    public class CharacterCreationScreen
    {
        private readonly IGraphicsDevice _graphicsDevice;
        private readonly Installation _installation;
        private readonly BioWareGame _game;
        private readonly BaseGuiManager _guiManager;
        private readonly Action<CharacterCreationData> _onComplete;
        private readonly Action _onCancel;
        private readonly GameDataManager _gameDataManager;
        private readonly IGraphicsBackend _graphicsBackend;
        private CharacterCreationData _characterData;
        private CreationStep _currentStep = CreationStep.ClassSelection;
        private bool _isQuickMode = false;
        private int _selectedClassIndex = 0;
        private int _selectedAttributeIndex = 0;
        private int _previousAppearance = 1;
        private IKeyboardState _previousKeyboardState;
        private IMouseState _previousMouseState;
        private float _modelRotationAngle = 0f;
        private bool _needsModelUpdate = true;
        private bool _guiLoaded = false;
        private ITexture2D _pixelTexture;

        // Portrait texture cache
        private Dictionary<int, ITexture2D> _portraitTextureCache;

        // 3D model rendering
        private IEntityModelRenderer _entityModelRenderer;
        private Entity _previewEntity;
        private Matrix4x4 _previewViewMatrix;
        private Matrix4x4 _previewProjectionMatrix;
        private bool _modelRendererInitialized = false;
        private IRenderTarget _previewRenderTarget;
        private ITexture2D _previewTexture;

        // Feat selection state
        private List<int> _availableFeatIds = new List<int>();
        private int _selectedFeatIndex = 0;
        private int _featScrollOffset = 0;

        // Skill selection state
        private List<int> _availableSkillIds = new List<int>(); // Skill IDs 0-7
        private int _selectedSkillIndex = 0;
        private int _skillScrollOffset = 0;
        private int _availableSkillPoints = 0;

        /// <summary>
        /// Character creation steps.
        /// </summary>
        private enum CreationStep
        {
            ClassSelection,
            QuickOrCustom,
            Attributes,
            Skills,
            Feats,
            Portrait,
            Name,
            Summary
        }

        /// <summary>
        /// Available classes for the current game.
        /// </summary>
        private CharacterClass[] GetAvailableClasses()
        {
            if (_game == BioWareGame.K1)
            {
                return new CharacterClass[] { CharacterClass.Scout, CharacterClass.Soldier, CharacterClass.Scoundrel };
            }
            else
            {
                return new CharacterClass[] { CharacterClass.JediGuardian, CharacterClass.JediSentinel, CharacterClass.JediConsular };
            }
        }

        /// <summary>
        /// Creates a new character creation screen.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for rendering.</param>
        /// <param name="installation">Game installation for loading resources.</param>
        /// <param name="game">KOTOR game version (K1 or K2).</param>
        /// <param name="guiManager">GUI manager for loading and rendering GUI panels.</param>
        /// <param name="onComplete">Callback when character creation is complete.</param>
        /// <param name="onCancel">Callback when character creation is cancelled.</param>
        /// <param name="graphicsBackend">Graphics backend for creating 3D model renderer (optional).</param>
        public CharacterCreationScreen(
            IGraphicsDevice graphicsDevice,
            Installation installation,
            BioWareGame game,
            BaseGuiManager guiManager,
            Action<CharacterCreationData> onComplete,
            Action onCancel,
            [CanBeNull] IGraphicsBackend graphicsBackend = null)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _installation = installation ?? throw new ArgumentNullException(nameof(installation));
            _game = game;
            _guiManager = guiManager ?? throw new ArgumentNullException(nameof(guiManager));
            _onComplete = onComplete ?? throw new ArgumentNullException(nameof(onComplete));
            _onCancel = onCancel ?? throw new ArgumentNullException(nameof(onCancel));
            _graphicsBackend = graphicsBackend;
            _gameDataManager = new GameDataManager(installation);

            // Initialize portrait texture cache
            _portraitTextureCache = new Dictionary<int, ITexture2D>();

            // Initialize character data with defaults
            // Convert BioWareGame to KotorGame for character creation data
            KotorGame kotorGame = game.IsK1() ? KotorGame.K1 : KotorGame.K2;
            _characterData = new CharacterCreationData
            {
                Game = kotorGame,
                Class = kotorGame == KotorGame.K1 ? CharacterClass.Scout : CharacterClass.JediGuardian,
                Gender = Gender.Male,
                Appearance = 1,
                Portrait = 0,
                Name = string.Empty,
                Strength = 14,
                Dexterity = 12,
                Constitution = 12,
                Intelligence = 12,
                Wisdom = 12,
                Charisma = 12,
                SelectedFeats = new List<int>(),
                SkillRanks = new Dictionary<int, int>()
            };

            // Initialize all skills to 0 (untrained)
            for (int i = 0; i < 8; i++)
            {
                _characterData.SkillRanks[i] = 0;
            }

            // Initialize available skills (all 8 skills in KOTOR)
            _availableSkillIds = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };

            // Initialize available feats for the default class
            UpdateAvailableFeats();

            // Initialize skill points
            UpdateSkillPoints();

            // Load the maincg GUI panel
            // Based on swkotor.exe and swkotor2.exe: Character creation uses "maincg" GUI panel
            // Original implementation: GUI panel is loaded when character creation screen is initialized
            int screenWidth = _graphicsDevice.Viewport.Width > 0 ? _graphicsDevice.Viewport.Width : 800;
            int screenHeight = _graphicsDevice.Viewport.Height > 0 ? _graphicsDevice.Viewport.Height : 600;

            _guiLoaded = _guiManager.LoadGui("maincg", screenWidth, screenHeight);
            if (_guiLoaded)
            {
                _guiManager.SetCurrentGui("maincg");
            }
            else
            {
                System.Console.WriteLine("[CharacterCreationScreen] WARNING: Failed to load maincg GUI panel, character creation UI may not display correctly");
            }

            // Create a 1x1 pixel texture for drawing rectangles
            byte[] pixelData = new byte[] { 255, 255, 255, 255 }; // White pixel
            _pixelTexture = _graphicsDevice.CreateTexture2D(1, 1, pixelData);

            // Initialize 3D model rendering system for character preview
            // Based on swkotor.exe and swkotor2.exe: Character preview uses 3D model rendering
            // Original implementation: 3D model renderer is created when character creation screen initializes
            InitializeModelRenderer();
        }

        /// <summary>
        /// Initializes the 3D model rendering system for character preview.
        /// Based on swkotor.exe and swkotor2.exe: Character preview uses 3D model rendering
        /// - Original implementation: 3D model renderer is created when character creation screen initializes
        /// - Model renderer loads and renders character models from MDL files
        /// - Preview entity is created with initial character appearance data
        /// - Camera matrices are set up for preview viewport
        /// </summary>
        private void InitializeModelRenderer()
        {
            // Only initialize if graphics backend is available
            if (_graphicsBackend == null)
            {
                System.Console.WriteLine("[CharacterCreationScreen] INFO: Graphics backend not available, 3D model preview will be disabled");
                return;
            }

            try
            {
                // Create entity model renderer from graphics backend
                // Based on swkotor.exe and swkotor2.exe: Entity model renderer loads MDL models and renders entities
                // GameDataManager provides access to 2DA tables (appearance.2da) for model resolution
                // Installation provides access to resource system (MDL files, textures, etc.)
                _entityModelRenderer = _graphicsBackend.CreateEntityModelRenderer(_gameDataManager, _installation);

                if (_entityModelRenderer == null)
                {
                    System.Console.WriteLine("[CharacterCreationScreen] WARNING: Failed to create entity model renderer, 3D model preview will be disabled");
                    return;
                }

                // Mark renderer as initialized
                _modelRendererInitialized = true;

                // Initialize preview entity with initial character data
                InitializePreviewEntity();

                // Initialize preview camera matrices
                InitializePreviewCamera();

                System.Console.WriteLine("[CharacterCreationScreen] INFO: 3D model rendering system initialized successfully");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("[CharacterCreationScreen] WARNING: Failed to initialize 3D model rendering system: " + ex.Message);
                _modelRendererInitialized = false;
            }
        }

        /// <summary>
        /// Updates the character creation screen.
        /// </summary>
        /// <remarks>
        /// Character Creation Update Implementation:
        /// - Based on swkotor.exe and swkotor2.exe character generation update logic
        /// - Handles input for current step (keyboard and mouse)
        /// - Updates character model preview when appearance changes
        /// - Handles button clicks (Next, Back, Cancel, Finish)
        /// - Manages step navigation and validation
        /// - Processes step-specific input (class selection, attributes, skills, feats, portrait, name)
        ///
        /// Based on reverse engineering of:
        /// - swkotor.exe: Character generation input handling and update loop
        /// - swkotor2.exe: Character generation input handling and update loop
        /// - vendor/reone: CharacterGeneration::handle() and CharacterGeneration::update() methods
        /// </remarks>
        public void Update(float deltaTime, IKeyboardState keyboardState, IMouseState mouseState)
        {
            // Initialize previous states on first call
            if (_previousKeyboardState == null)
            {
                _previousKeyboardState = keyboardState;
            }
            if (_previousMouseState == null)
            {
                _previousMouseState = mouseState;
            }

            // Update model rotation for preview animation
            _modelRotationAngle += deltaTime * 0.5f; // Slow rotation
            if (_modelRotationAngle > 2.0f * (float)Math.PI)
            {
                _modelRotationAngle -= 2.0f * (float)Math.PI;
            }

            // Check for appearance changes and update model if needed
            if (_characterData.Appearance != _previousAppearance)
            {
                _previousAppearance = _characterData.Appearance;
                _needsModelUpdate = true;
            }

            // Update preview model if needed (appearance, gender, or class changes)
            if (_needsModelUpdate && _modelRendererInitialized)
            {
                UpdatePreviewModel();
            }

            // Handle global input (Cancel/Escape)
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Escape))
            {
                Cancel();
                return;
            }

            // Handle step-specific input
            switch (_currentStep)
            {
                case CreationStep.ClassSelection:
                    HandleClassSelectionInput(keyboardState, mouseState);
                    break;
                case CreationStep.QuickOrCustom:
                    HandleQuickOrCustomInput(keyboardState, mouseState);
                    break;
                case CreationStep.Attributes:
                    HandleAttributesInput(keyboardState, mouseState);
                    break;
                case CreationStep.Skills:
                    HandleSkillsInput(keyboardState, mouseState);
                    break;
                case CreationStep.Feats:
                    HandleFeatsInput(keyboardState, mouseState);
                    break;
                case CreationStep.Portrait:
                    HandlePortraitInput(keyboardState, mouseState);
                    break;
                case CreationStep.Name:
                    HandleNameInput(keyboardState, mouseState);
                    break;
                case CreationStep.Summary:
                    HandleSummaryInput(keyboardState, mouseState);
                    break;
            }

            // Update previous states for next frame
            _previousKeyboardState = keyboardState;
            _previousMouseState = mouseState;
        }

        /// <summary>
        /// Checks if a key was just pressed (not held).
        /// </summary>
        private bool IsKeyPressed(IKeyboardState current, IKeyboardState previous, Keys key)
        {
            return current.IsKeyDown(key) && previous.IsKeyUp(key);
        }

        /// <summary>
        /// Checks if a mouse button was just clicked (not held).
        /// </summary>
        private bool IsMouseButtonClicked(IMouseState current, IMouseState previous, MouseButton button)
        {
            return current.IsButtonDown(button) && previous.IsButtonUp(button);
        }

        /// <summary>
        /// Handles input for class selection step.
        /// </summary>
        private void HandleClassSelectionInput(IKeyboardState keyboardState, IMouseState mouseState)
        {
            CharacterClass[] availableClasses = GetAvailableClasses();

            // Keyboard navigation
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Up) || IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Left))
            {
                _selectedClassIndex = (_selectedClassIndex - 1 + availableClasses.Length) % availableClasses.Length;
                _characterData.Class = availableClasses[_selectedClassIndex];
                UpdateAvailableFeats();
                UpdateSkillPoints();
                _needsModelUpdate = true;
            }
            else if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Down) || IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Right))
            {
                _selectedClassIndex = (_selectedClassIndex + 1) % availableClasses.Length;
                _characterData.Class = availableClasses[_selectedClassIndex];
                UpdateAvailableFeats();
                UpdateSkillPoints();
                _needsModelUpdate = true;
            }

            // Confirm selection
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Enter) || IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Space))
            {
                GoToNextStep();
            }
        }

        /// <summary>
        /// Handles input for Quick or Custom selection step.
        /// </summary>
        private void HandleQuickOrCustomInput(IKeyboardState keyboardState, IMouseState mouseState)
        {
            // Keyboard navigation
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Left) || IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Up))
            {
                _isQuickMode = true;
            }
            else if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Right) || IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Down))
            {
                _isQuickMode = false;
            }

            // Confirm selection
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Enter) || IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Space))
            {
                if (_isQuickMode)
                {
                    // Quick mode: skip to portrait/name, use defaults for attributes/skills/feats
                    _currentStep = CreationStep.Portrait;
                }
                else
                {
                    // Custom mode: go to attributes
                    _currentStep = CreationStep.Attributes;
                }
            }

            // Back button
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Back))
            {
                GoToPreviousStep();
            }
        }

        /// <summary>
        /// Handles input for attributes step.
        /// </summary>
        private void HandleAttributesInput(IKeyboardState keyboardState, IMouseState mouseState)
        {
            // Attribute navigation (6 attributes: STR, DEX, CON, INT, WIS, CHA)
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Up))
            {
                _selectedAttributeIndex = (_selectedAttributeIndex - 1 + 6) % 6;
            }
            else if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Down))
            {
                _selectedAttributeIndex = (_selectedAttributeIndex + 1) % 6;
            }

            // Attribute adjustment
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Left))
            {
                AdjustAttribute(_selectedAttributeIndex, -1);
            }
            else if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Right))
            {
                AdjustAttribute(_selectedAttributeIndex, 1);
            }

            // Next/Back
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Enter) || IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Space))
            {
                GoToNextStep();
            }
            else if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Back))
            {
                GoToPreviousStep();
            }
        }

        /// <summary>
        /// Adjusts an attribute value with validation.
        /// </summary>
        private void AdjustAttribute(int attributeIndex, int delta)
        {
            // Calculate total points spent
            int totalPoints = _characterData.Strength + _characterData.Dexterity + _characterData.Constitution +
                            _characterData.Intelligence + _characterData.Wisdom + _characterData.Charisma;
            int basePoints = 6 * 8; // 6 attributes * 8 base = 48 points
            int pointsSpent = totalPoints - basePoints;
            int maxPoints = 30; // Maximum points that can be allocated

            // Get current attribute value
            int currentValue = GetAttributeValue(attributeIndex);
            int newValue = currentValue + delta;

            // Validate: attributes must be between 8 and 18 (or 20 with bonuses)
            if (newValue < 8 || newValue > 20)
            {
                return;
            }

            // Validate: check point allocation
            int newPointsSpent = pointsSpent + delta;
            if (newPointsSpent < 0 || newPointsSpent > maxPoints)
            {
                return;
            }

            // Apply change
            SetAttributeValue(attributeIndex, newValue);

            // Update skill points if Intelligence changed
            if (attributeIndex == 3) // Intelligence
            {
                UpdateSkillPoints();
            }
        }

        /// <summary>
        /// Gets the value of an attribute by index.
        /// </summary>
        private int GetAttributeValue(int index)
        {
            switch (index)
            {
                case 0: return _characterData.Strength;
                case 1: return _characterData.Dexterity;
                case 2: return _characterData.Constitution;
                case 3: return _characterData.Intelligence;
                case 4: return _characterData.Wisdom;
                case 5: return _characterData.Charisma;
                default: return 12;
            }
        }

        /// <summary>
        /// Sets the value of an attribute by index.
        /// </summary>
        private void SetAttributeValue(int index, int value)
        {
            switch (index)
            {
                case 0: _characterData.Strength = value; break;
                case 1: _characterData.Dexterity = value; break;
                case 2: _characterData.Constitution = value; break;
                case 3: _characterData.Intelligence = value; break;
                case 4: _characterData.Wisdom = value; break;
                case 5: _characterData.Charisma = value; break;
            }
        }

        /// <summary>
        /// Handles input for skills step.
        /// Based on swkotor.exe and swkotor2.exe: Skill selection allows adjusting skill ranks with available skill points
        /// - Original implementation: Up/Down arrows navigate skill list, Left/Right adjusts skill rank, Enter/Space continues
        /// - Skill points are calculated based on class and Intelligence modifier
        /// - Class skills cost 1 point per rank, cross-class skills cost 2 points per rank
        /// - Class skills can be raised to rank 4 at level 1, cross-class skills can be raised to rank 2
        /// </summary>
        private void HandleSkillsInput(IKeyboardState keyboardState, IMouseState mouseState)
        {
            // Skill list navigation
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Up))
            {
                if (_selectedSkillIndex > 0)
                {
                    _selectedSkillIndex--;
                    // Auto-scroll if selection is above visible area
                    if (_selectedSkillIndex < _skillScrollOffset)
                    {
                        _skillScrollOffset = _selectedSkillIndex;
                    }
                }
            }
            else if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Down))
            {
                if (_selectedSkillIndex < _availableSkillIds.Count - 1)
                {
                    _selectedSkillIndex++;
                    // Auto-scroll if selection is below visible area (assuming ~8 visible items)
                    int maxVisible = 8;
                    if (_selectedSkillIndex >= _skillScrollOffset + maxVisible)
                    {
                        _skillScrollOffset = _selectedSkillIndex - maxVisible + 1;
                    }
                }
            }

            // Skill rank adjustment
            if (_selectedSkillIndex >= 0 && _selectedSkillIndex < _availableSkillIds.Count)
            {
                int skillId = _availableSkillIds[_selectedSkillIndex];

                // Increase skill rank
                if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Right))
                {
                    AdjustSkillRank(skillId, 1);
                }
                // Decrease skill rank
                else if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Left))
                {
                    AdjustSkillRank(skillId, -1);
                }
            }

            // Next/Back
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Enter) || IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Space))
            {
                GoToNextStep();
            }
            else if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Back))
            {
                GoToPreviousStep();
            }
        }

        /// <summary>
        /// Adjusts a skill rank with validation.
        /// Based on swkotor.exe and swkotor2.exe: Skill rank adjustment validates point costs and maximum ranks
        /// - Original implementation: Checks if skill is class skill (1 point) or cross-class (2 points)
        /// - Validates maximum rank: class skills can go to 4, cross-class to 2 at level 1
        /// - Updates available skill points when rank changes
        /// </summary>
        private void AdjustSkillRank(int skillId, int delta)
        {
            if (skillId < 0 || skillId >= 8)
            {
                return;
            }

            int currentRank = _characterData.SkillRanks.ContainsKey(skillId) ? _characterData.SkillRanks[skillId] : 0;
            int newRank = currentRank + delta;

            // Validate: ranks cannot be negative
            if (newRank < 0)
            {
                return;
            }

            // Get class ID
            int classId = GetClassId(_characterData.Class);

            // Check if skill is class skill
            bool isClassSkill = _gameDataManager.IsClassSkill(skillId, classId);

            // Determine maximum rank and point cost
            int maxRank = isClassSkill ? 4 : 2; // Class skills: 4, cross-class: 2 at level 1
            int pointCost = isClassSkill ? 1 : 2; // Class skills: 1 point, cross-class: 2 points

            // Validate: check maximum rank
            if (newRank > maxRank)
            {
                return;
            }

            // Validate: check available points
            int pointsNeeded = delta > 0 ? pointCost : -pointCost;
            if (delta > 0 && _availableSkillPoints < pointCost)
            {
                return; // Not enough points to increase
            }

            // Apply change
            _characterData.SkillRanks[skillId] = newRank;
            _availableSkillPoints -= pointsNeeded;
        }

        /// <summary>
        /// Updates available skill points based on class and Intelligence.
        /// Based on swkotor.exe and swkotor2.exe: Skill points = (class skill point base + INT modifier) / 2, multiplied by 4 for level 1
        /// - Original implementation: Calculates skill points from class data (skillpointbase from classes.2da) and Intelligence modifier
        /// - Level 1 characters get 4x the normal skill points
        /// - Formula: points = max(1, (skillpointbase + INT_modifier) / 2) * 4
        /// </summary>
        private void UpdateSkillPoints()
        {
            // Get class data
            int classId = GetClassId(_characterData.Class);
            Andastra.Runtime.Engines.Odyssey.Data.GameDataManager.ClassData classData = _gameDataManager.GetClass(classId);
            if (classData == null)
            {
                _availableSkillPoints = 0;
                return;
            }

            // Calculate Intelligence modifier: (INT - 10) / 2
            int intModifier = (_characterData.Intelligence - 10) / 2;

            // Calculate base skill points: (class skill point base + INT modifier) / 2, minimum 1
            int baseSkillPoints = classData.SkillsPerLevel + intModifier;
            int skillPointsPerLevel = Math.Max(1, baseSkillPoints / 2);

            // Level 1 characters get 4x skill points
            int totalSkillPoints = skillPointsPerLevel * 4;

            // Calculate points already spent
            int pointsSpent = 0;
            for (int i = 0; i < 8; i++)
            {
                int rank = _characterData.SkillRanks.ContainsKey(i) ? _characterData.SkillRanks[i] : 0;
                if (rank > 0)
                {
                    bool isClassSkill = _gameDataManager.IsClassSkill(i, classId);
                    int pointCost = isClassSkill ? 1 : 2;
                    pointsSpent += rank * pointCost;
                }
            }

            _availableSkillPoints = totalSkillPoints - pointsSpent;
        }

        /// <summary>
        /// Handles input for feats step.
        /// Based on swkotor.exe and swkotor2.exe: Feat selection allows browsing available feats and selecting/deselecting them
        /// - Original implementation: Up/Down arrows navigate feat list, Enter/Space selects/deselects feat, Left/Right scrolls description
        /// - Feats are filtered by class and prerequisites
        /// </summary>
        private void HandleFeatsInput(IKeyboardState keyboardState, IMouseState mouseState)
        {
            // Feat list navigation
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Up))
            {
                if (_selectedFeatIndex > 0)
                {
                    _selectedFeatIndex--;
                    // Auto-scroll if selection is above visible area
                    if (_selectedFeatIndex < _featScrollOffset)
                    {
                        _featScrollOffset = _selectedFeatIndex;
                    }
                }
            }
            else if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Down))
            {
                if (_selectedFeatIndex < _availableFeatIds.Count - 1)
                {
                    _selectedFeatIndex++;
                    // Auto-scroll if selection is below visible area (assuming ~10 visible items)
                    int maxVisible = 10;
                    if (_selectedFeatIndex >= _featScrollOffset + maxVisible)
                    {
                        _featScrollOffset = _selectedFeatIndex - maxVisible + 1;
                    }
                }
            }

            // Select/Deselect feat
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Enter) || IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Space))
            {
                if (_selectedFeatIndex >= 0 && _selectedFeatIndex < _availableFeatIds.Count)
                {
                    int featId = _availableFeatIds[_selectedFeatIndex];
                    if (_characterData.SelectedFeats.Contains(featId))
                    {
                        _characterData.SelectedFeats.Remove(featId);
                    }
                    else
                    {
                        // Check if feat meets prerequisites
                        GameDataManager.FeatData featData = _gameDataManager.GetFeat(featId);
                        if (featData != null && MeetsFeatPrerequisites(featData))
                        {
                            _characterData.SelectedFeats.Add(featId);
                        }
                    }
                }
            }

            // Next/Back
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Tab))
            {
                GoToNextStep();
            }
            else if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Back))
            {
                GoToPreviousStep();
            }
        }

        /// <summary>
        /// Handles input for portrait selection step.
        /// </summary>
        private void HandlePortraitInput(IKeyboardState keyboardState, IMouseState mouseState)
        {
            // Portrait navigation
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Left) || IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Up))
            {
                _characterData.Portrait = Math.Max(0, _characterData.Portrait - 1);
                _needsModelUpdate = true;
            }
            else if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Right) || IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Down))
            {
                _characterData.Portrait = Math.Min(99, _characterData.Portrait + 1); // Assume max 100 portraits
                _needsModelUpdate = true;
            }

            // Next/Back
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Enter) || IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Space))
            {
                GoToNextStep();
            }
            else if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Back))
            {
                GoToPreviousStep();
            }
        }

        /// <summary>
        /// Handles input for name entry step.
        /// </summary>
        private void HandleNameInput(IKeyboardState keyboardState, IMouseState mouseState)
        {
            // Name entry: handle text input
            Keys[] pressedKeys = keyboardState.GetPressedKeys();
            foreach (Keys key in pressedKeys)
            {
                if (IsKeyPressed(keyboardState, _previousKeyboardState, key))
                {
                    // Handle backspace
                    if (key == Keys.Back && _characterData.Name.Length > 0)
                    {
                        _characterData.Name = _characterData.Name.Substring(0, _characterData.Name.Length - 1);
                    }
                    // Handle printable characters
                    else if (key >= Keys.A && key <= Keys.Z)
                    {
                        // Check for shift to get uppercase
                        bool isShift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
                        char c = (char)((int)'a' + (int)(key - Keys.A));
                        if (isShift)
                        {
                            c = char.ToUpper(c);
                        }
                        _characterData.Name += c;
                    }
                    else if (key >= Keys.D0 && key <= Keys.D9)
                    {
                        char c = (char)((int)'0' + (int)(key - Keys.D0));
                        _characterData.Name += c;
                    }
                    else if (key == Keys.Space)
                    {
                        _characterData.Name += " ";
                    }
                }
            }

            // Limit name length
            if (_characterData.Name.Length > 32)
            {
                _characterData.Name = _characterData.Name.Substring(0, 32);
            }

            // Next/Back/Finish
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Enter))
            {
                if (string.IsNullOrWhiteSpace(_characterData.Name))
                {
                    _characterData.Name = "Player"; // Default name
                }
                GoToNextStep();
            }
            else if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Back))
            {
                GoToPreviousStep();
            }
        }

        /// <summary>
        /// Handles input for summary step.
        /// </summary>
        private void HandleSummaryInput(IKeyboardState keyboardState, IMouseState mouseState)
        {
            // Finish or go back
            if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Enter) || IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Space))
            {
                Finish();
            }
            else if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Back))
            {
                GoToPreviousStep();
            }
        }

        /// <summary>
        /// Advances to the next step in character creation.
        /// </summary>
        private void GoToNextStep()
        {
            switch (_currentStep)
            {
                case CreationStep.ClassSelection:
                    _currentStep = CreationStep.QuickOrCustom;
                    break;
                case CreationStep.QuickOrCustom:
                    if (_isQuickMode)
                    {
                        _currentStep = CreationStep.Portrait;
                    }
                    else
                    {
                        _currentStep = CreationStep.Attributes;
                    }
                    break;
                case CreationStep.Attributes:
                    _currentStep = CreationStep.Skills;
                    break;
                case CreationStep.Skills:
                    _currentStep = CreationStep.Feats;
                    break;
                case CreationStep.Feats:
                    _currentStep = CreationStep.Portrait;
                    break;
                case CreationStep.Portrait:
                    _currentStep = CreationStep.Name;
                    break;
                case CreationStep.Name:
                    _currentStep = CreationStep.Summary;
                    break;
                case CreationStep.Summary:
                    Finish();
                    break;
            }
        }

        /// <summary>
        /// Returns to the previous step in character creation.
        /// </summary>
        private void GoToPreviousStep()
        {
            switch (_currentStep)
            {
                case CreationStep.QuickOrCustom:
                    _currentStep = CreationStep.ClassSelection;
                    break;
                case CreationStep.Attributes:
                    _currentStep = CreationStep.QuickOrCustom;
                    break;
                case CreationStep.Skills:
                    _currentStep = CreationStep.Attributes;
                    break;
                case CreationStep.Feats:
                    _currentStep = CreationStep.Skills;
                    break;
                case CreationStep.Portrait:
                    if (_isQuickMode)
                    {
                        _currentStep = CreationStep.QuickOrCustom;
                    }
                    else
                    {
                        _currentStep = CreationStep.Feats;
                    }
                    break;
                case CreationStep.Name:
                    _currentStep = CreationStep.Portrait;
                    break;
                case CreationStep.Summary:
                    _currentStep = CreationStep.Name;
                    break;
            }
        }

        /// <summary>
        /// Draws the character creation screen.
        /// </summary>
        /// <remarks>
        /// Character Creation Rendering Implementation:
        /// - Based on swkotor.exe and swkotor2.exe character generation rendering
        /// - Renders "maincg" GUI panel as the base UI
        /// - Renders character model preview in 3D viewport
        /// - Renders step-specific UI elements (class selection, attributes, skills, feats, portrait, name, summary)
        /// - Renders navigation buttons (Next, Back, Cancel, Finish)
        /// - Updates GUI control states based on current step and selections
        ///
        /// Based on reverse engineering of:
        /// - swkotor.exe: Character generation rendering functions
        /// - swkotor2.exe: Character generation rendering functions
        /// - vendor/reone: CharacterGeneration::draw() method
        /// </remarks>
        /// <param name="spriteBatch">Sprite batch for 2D rendering.</param>
        /// <param name="font">Font for text rendering.</param>
        public void Draw(ISpriteBatch spriteBatch, IFont font)
        {
            if (spriteBatch == null)
            {
                return;
            }

            // Render the maincg GUI panel
            // Based on swkotor.exe and swkotor2.exe: "maincg" GUI panel is the base UI for character creation
            // The GUI manager handles rendering of the GUI panel using its internal sprite batch
            // We render step-specific UI and buttons on top using the provided sprite batch
            if (_guiLoaded)
            {
                // Render GUI panel using GUI manager's Draw method
                // Note: GUI manager uses its own sprite batch, so we render it first
                // Then we render step-specific UI on top using the provided sprite batch
                _guiManager.Draw(null); // Pass null for gameTime as it's not used in base implementation
            }

            // Begin sprite batch for step-specific UI and buttons
            spriteBatch.Begin();

            try
            {
                // Render character model preview
                // Based on swkotor.exe and swkotor2.exe: Character model is rendered in a 3D viewport
                // Original implementation: 3D model is rendered with rotation animation
                // swkotor.exe: Character preview rendering uses 3D model viewport with camera rotation
                // swkotor2.exe: Character preview rendering uses 3D model viewport with camera rotation
                RenderCharacterModelPreview(spriteBatch);

                // Render step-specific UI based on current step
                // Based on swkotor.exe and swkotor2.exe: Each step has specific UI elements
                switch (_currentStep)
                {
                    case CreationStep.ClassSelection:
                        RenderClassSelectionUI(spriteBatch, font);
                        break;
                    case CreationStep.QuickOrCustom:
                        RenderQuickOrCustomUI(spriteBatch, font);
                        break;
                    case CreationStep.Attributes:
                        RenderAttributesUI(spriteBatch, font);
                        break;
                    case CreationStep.Skills:
                        RenderSkillsUI(spriteBatch, font);
                        break;
                    case CreationStep.Feats:
                        RenderFeatsUI(spriteBatch, font);
                        break;
                    case CreationStep.Portrait:
                        RenderPortraitUI(spriteBatch, font);
                        break;
                    case CreationStep.Name:
                        RenderNameUI(spriteBatch, font);
                        break;
                    case CreationStep.Summary:
                        RenderSummaryUI(spriteBatch, font);
                        break;
                }

                // Render navigation buttons (Next, Back, Cancel, Finish)
                // Based on swkotor.exe and swkotor2.exe: Navigation buttons are always visible
                RenderNavigationButtons(spriteBatch, font);
            }
            finally
            {
                spriteBatch.End();
            }
        }

        /// <summary>
        /// Initializes the preview entity for 3D character model rendering.
        /// Based on swkotor.exe and swkotor2.exe: Character preview entity is created with appearance data
        /// - Original implementation: Creates temporary creature entity with appearance type for preview
        /// - Entity is positioned at origin and rotated for preview display
        /// - swkotor.exe: Character preview entity created with appearance data from character creation
        /// - swkotor2.exe: Character preview entity created with appearance data from character creation
        /// </summary>
        private void InitializePreviewEntity()
        {
            if (!_modelRendererInitialized)
            {
                return;
            }

            // Create a temporary entity for preview
            // Based on swkotor.exe and swkotor2.exe: Character preview uses temporary creature entity
            _previewEntity = new Entity(RuntimeObjectType.Creature, null);
            _previewEntity.Tag = "CharacterPreview";
            _previewEntity.Position = Vector3.Zero;
            _previewEntity.Facing = 0f;

            // Add transform component (required by EntityModelRenderer for rendering)
            // Based on swkotor.exe and swkotor2.exe: Entity transform component provides position/rotation for rendering
            var transformComponent = new TransformComponent(Vector3.Zero, 0f);
            transformComponent.Owner = _previewEntity;
            _previewEntity.AddComponent<ITransformComponent>(transformComponent);

            // Add creature component with appearance data
            var creatureComponent = new CreatureComponent();
            creatureComponent.AppearanceType = _characterData.Appearance;
            creatureComponent.BodyVariation = 0; // Use ModelA (variation 0)
            creatureComponent.Gender = _characterData.Gender == Gender.Male ? 0 : 1;
            _previewEntity.AddComponent<CreatureComponent>(creatureComponent);

            // Add renderable component
            var renderableComponent = new OdysseyRenderableComponent();
            renderableComponent.Visible = true;
            // Model will be resolved by ModelResolver based on appearance
            _previewEntity.AddComponent<IRenderableComponent>(renderableComponent);

            // Update model when entity is created
            UpdatePreviewModel();
        }

        /// <summary>
        /// Initializes the preview camera matrices for 3D character model rendering.
        /// Based on swkotor.exe and swkotor2.exe: Character preview uses fixed camera position
        /// - Original implementation: Camera positioned to show character from front/side angle
        /// - Projection matrix set up for preview viewport aspect ratio
        /// </summary>
        private void InitializePreviewCamera()
        {
            if (!_modelRendererInitialized)
            {
                return;
            }

            // Preview viewport dimensions
            int previewX = _graphicsDevice.Viewport.Width - 350;
            int previewY = 100;
            int previewWidth = 300;
            int previewHeight = 400;

            // Calculate aspect ratio for preview viewport
            float aspectRatio = (float)previewWidth / (float)previewHeight;

            // Set up view matrix: Camera positioned to look at character from front/side
            // Based on swkotor.exe and swkotor2.exe: Character preview camera positioned at (0, 1.5, 3) looking at (0, 0.9, 0)
            Vector3 cameraPosition = new Vector3(0f, 1.5f, 3f);
            Vector3 cameraTarget = new Vector3(0f, 0.9f, 0f); // Character center (approximately eye level)
            Vector3 cameraUp = Vector3.UnitY;

            // Create view matrix using LookAt
            _previewViewMatrix = Matrix4x4.CreateLookAt(cameraPosition, cameraTarget, cameraUp);

            // Set up projection matrix: Perspective projection for 3D preview
            // Based on swkotor.exe and swkotor2.exe: Character preview uses ~45 degree FOV
            float fieldOfView = (float)Math.PI / 4f; // 45 degrees
            float nearPlane = 0.1f;
            float farPlane = 100f;

            _previewProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, aspectRatio, nearPlane, farPlane);
        }

        /// <summary>
        /// Updates the preview model when appearance, gender, or class changes.
        /// Based on swkotor.exe and swkotor2.exe: Model updates when character data changes
        /// - Original implementation: Reloads model from appearance.2da when appearance changes
        /// </summary>
        private void UpdatePreviewModel()
        {
            if (!_modelRendererInitialized || _previewEntity == null)
            {
                return;
            }

            // Update creature component appearance
            CreatureComponent creatureComp = _previewEntity.GetComponent<CreatureComponent>();
            if (creatureComp != null)
            {
                creatureComp.AppearanceType = _characterData.Appearance;
                creatureComp.Gender = _characterData.Gender == Gender.Male ? 0 : 1;
            }

            // Resolve model ResRef from appearance
            string modelResRef = ModelResolver.ResolveCreatureModel(_gameDataManager, _characterData.Appearance, 0);

            // Update renderable component with model ResRef
            IRenderableComponent renderableComp = _previewEntity.GetComponent<IRenderableComponent>();
            if (renderableComp != null && !string.IsNullOrEmpty(modelResRef))
            {
                renderableComp.ModelResRef = modelResRef;
            }

            _needsModelUpdate = false;
        }

        /// <summary>
        /// Renders the character model preview.
        /// Based on swkotor.exe and swkotor2.exe: Character model is rendered in a 3D viewport with rotation
        /// - Original implementation: 3D model is rendered using DirectX with camera positioned for character preview
        /// - Model rotates slowly to show character from different angles
        /// - Model updates when appearance, gender, or class changes
        /// - Preview viewport is typically positioned on the right side of the screen
        /// - swkotor.exe: Character preview rendering function handles 3D viewport setup
        /// - swkotor2.exe: Character preview rendering function handles 3D viewport setup
        /// </summary>
        private void RenderCharacterModelPreview(ISpriteBatch spriteBatch)
        {
            int previewX = _graphicsDevice.Viewport.Width - 350;
            int previewY = 100;
            int previewWidth = 300;
            int previewHeight = 400;

            // Draw preview background
            GraphicsColor previewBgColor = new GraphicsColor(30, 30, 30, 200);
            DrawRectangle(spriteBatch, new Rectangle(previewX, previewY, previewWidth, previewHeight), previewBgColor);

            // Draw preview border
            GraphicsColor previewBorderColor = new GraphicsColor(100, 100, 100, 255);
            DrawRectangleOutline(spriteBatch, new Rectangle(previewX, previewY, previewWidth, previewHeight), previewBorderColor, 2);

            // Render 3D model if renderer is initialized
            if (_modelRendererInitialized && _entityModelRenderer != null && _previewEntity != null)
            {
                try
                {
                    // Ensure preview render target is created
                    // Based on swkotor.exe and swkotor2.exe: Character preview uses render target for viewport rendering
                    if (_previewRenderTarget == null || _previewRenderTarget.Width != previewWidth || _previewRenderTarget.Height != previewHeight)
                    {
                        // Dispose old render target if it exists
                        if (_previewRenderTarget != null)
                        {
                            _previewRenderTarget.Dispose();
                            _previewRenderTarget = null;
                        }

                        // Create render target for preview region
                        // Based on swkotor.exe and swkotor2.exe: Render target created with depth buffer for 3D rendering
                        _previewRenderTarget = _graphicsDevice.CreateRenderTarget(previewWidth, previewHeight, true);
                        if (_previewRenderTarget != null)
                        {
                            _previewTexture = _previewRenderTarget.ColorTexture;
                        }
                    }

                    if (_previewRenderTarget != null && _previewTexture != null)
                    {
                        // Save current render target and viewport
                        IRenderTarget previousRenderTarget = _graphicsDevice.RenderTarget;
                        Viewport previousViewport = _graphicsDevice.Viewport;

                        try
                        {
                            // Set render target to preview render target
                            _graphicsDevice.RenderTarget = _previewRenderTarget;

                            // Set viewport to match preview render target size
                            Viewport previewViewport = new Viewport(0, 0, previewWidth, previewHeight, 0.0f, 1.0f);
                            // Note: Viewport setting is handled by graphics device implementation
                            // The render target automatically sets the appropriate viewport

                            // Clear render target with dark background
                            // Based on swkotor.exe and swkotor2.exe: Character preview uses dark background color
                            _graphicsDevice.Clear(new GraphicsColor(20, 20, 25, 255));
                            _graphicsDevice.ClearDepth(1.0f);

                            // Apply rotation to view matrix for animated rotation
                            // Based on swkotor.exe and swkotor2.exe: Character preview rotates slowly
                            // Rotate the camera around the character (orbit camera)
                            Vector3 cameraTarget = new Vector3(0f, 0.9f, 0f); // Character center (approximately eye level)

                            // Calculate rotated camera position (orbit around character)
                            float radius = 3f;
                            float height = 1.5f;
                            Vector3 rotatedCameraPos = new Vector3(
                                (float)(Math.Sin(_modelRotationAngle) * radius),
                                height,
                                (float)(Math.Cos(_modelRotationAngle) * radius)
                            );

                            // Create rotated view matrix
                            Matrix4x4 rotatedViewMatrix = Matrix4x4.CreateLookAt(rotatedCameraPos, cameraTarget, Vector3.UnitY);

                            // Update projection matrix based on preview viewport aspect ratio
                            float aspectRatio = (float)previewWidth / (float)previewHeight;
                            float fieldOfView = (float)Math.PI / 4f; // 45 degrees
                            float nearPlane = 0.1f;
                            float farPlane = 100f;
                            Matrix4x4 projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, aspectRatio, nearPlane, farPlane);

                            // Render the entity model with rotation
                            // Based on swkotor.exe and swkotor2.exe: Entity model rendered with view/projection matrices
                            _entityModelRenderer.RenderEntity(_previewEntity, rotatedViewMatrix, projectionMatrix);
                        }
                        finally
                        {
                            // Restore previous render target and viewport
                            _graphicsDevice.RenderTarget = previousRenderTarget;
                        }

                        // Draw the rendered preview texture as a sprite in the preview area
                        // Based on swkotor.exe and swkotor2.exe: Preview texture drawn to screen position
                        spriteBatch.Draw(_previewTexture, new Rectangle(previewX, previewY, previewWidth, previewHeight), GraphicsColor.White);
                    }
                }
                catch (Exception ex)
                {
                    // If 3D rendering fails, fall back to placeholder
                    System.Console.WriteLine("[CharacterCreationScreen] WARNING: 3D model rendering failed: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Renders the class selection UI.
        /// Based on swkotor.exe and swkotor2.exe: Class selection displays available classes with descriptions
        /// - K1: Scout, Soldier, Scoundrel
        /// - K2: Jedi Guardian, Jedi Sentinel, Jedi Consular
        /// </summary>
        private void RenderClassSelectionUI(ISpriteBatch spriteBatch, IFont font)
        {
            if (font == null)
            {
                return;
            }

            CharacterClass[] availableClasses = GetAvailableClasses();
            int startY = 150;
            int itemHeight = 40;
            int selectedY = startY + (_selectedClassIndex * itemHeight);

            // Render class options
            for (int i = 0; i < availableClasses.Length; i++)
            {
                CharacterClass characterClass = availableClasses[i];
                string className = GetClassName(characterClass);
                bool isSelected = (i == _selectedClassIndex);

                int y = startY + (i * itemHeight);
                GraphicsColor textColor = isSelected ? GraphicsColor.Yellow : GraphicsColor.White;

                // Draw selection indicator
                if (isSelected)
                {
                    DrawRectangle(spriteBatch, new Rectangle(50, y - 2, 400, itemHeight), new GraphicsColor(100, 100, 100, 100));
                }

                // Draw class name
                spriteBatch.DrawString(font, className, new GraphicsVector2(60, y), textColor);
            }

            // Render step title
            spriteBatch.DrawString(font, "Select Class", new GraphicsVector2(50, 100), GraphicsColor.White);
        }

        /// <summary>
        /// Renders the Quick or Custom selection UI.
        /// Based on swkotor.exe and swkotor2.exe: Player chooses between Quick (defaults) or Custom (full customization)
        /// </summary>
        private void RenderQuickOrCustomUI(ISpriteBatch spriteBatch, IFont font)
        {
            if (font == null)
            {
                return;
            }

            int startY = 200;
            int itemHeight = 50;

            // Render Quick option
            GraphicsColor quickColor = _isQuickMode ? GraphicsColor.Yellow : GraphicsColor.White;
            spriteBatch.DrawString(font, "Quick Character", new GraphicsVector2(50, startY), quickColor);
            if (_isQuickMode)
            {
                DrawRectangle(spriteBatch, new Rectangle(45, startY - 2, 300, itemHeight), new GraphicsColor(100, 100, 100, 100));
            }

            // Render Custom option
            GraphicsColor customColor = !_isQuickMode ? GraphicsColor.Yellow : GraphicsColor.White;
            spriteBatch.DrawString(font, "Custom Character", new GraphicsVector2(50, startY + itemHeight), customColor);
            if (!_isQuickMode)
            {
                DrawRectangle(spriteBatch, new Rectangle(45, startY + itemHeight - 2, 300, itemHeight), new GraphicsColor(100, 100, 100, 100));
            }

            // Render step title
            spriteBatch.DrawString(font, "Character Creation Mode", new GraphicsVector2(50, 100), GraphicsColor.White);
        }

        /// <summary>
        /// Renders the attributes UI.
        /// Based on swkotor.exe and swkotor2.exe: Attributes are displayed with current values and adjustment controls
        /// - Attributes: STR, DEX, CON, INT, WIS, CHA
        /// - Each attribute can be adjusted within valid ranges (8-20)
        /// - Total points available for allocation are displayed
        /// </summary>
        private void RenderAttributesUI(ISpriteBatch spriteBatch, IFont font)
        {
            if (font == null)
            {
                return;
            }

            string[] attributeNames = { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
            int[] attributeValues = {
                _characterData.Strength,
                _characterData.Dexterity,
                _characterData.Constitution,
                _characterData.Intelligence,
                _characterData.Wisdom,
                _characterData.Charisma
            };

            int startY = 150;
            int itemHeight = 35;

            // Calculate points spent
            int totalPoints = _characterData.Strength + _characterData.Dexterity + _characterData.Constitution +
                            _characterData.Intelligence + _characterData.Wisdom + _characterData.Charisma;
            int basePoints = 6 * 8; // 6 attributes * 8 base = 48 points
            int pointsSpent = totalPoints - basePoints;
            int maxPoints = 30;
            int pointsRemaining = maxPoints - pointsSpent;

            // Render points remaining
            string pointsText = $"Points Remaining: {pointsRemaining} / {maxPoints}";
            spriteBatch.DrawString(font, pointsText, new GraphicsVector2(50, 100), GraphicsColor.Cyan);

            // Render attributes
            for (int i = 0; i < attributeNames.Length; i++)
            {
                bool isSelected = (i == _selectedAttributeIndex);
                int y = startY + (i * itemHeight);
                GraphicsColor textColor = isSelected ? GraphicsColor.Yellow : GraphicsColor.White;

                // Draw selection indicator
                if (isSelected)
                {
                    DrawRectangle(spriteBatch, new Rectangle(45, y - 2, 450, itemHeight), new GraphicsColor(100, 100, 100, 100));
                }

                // Draw attribute name and value
                string attributeText = $"{attributeNames[i]}: {attributeValues[i]}";
                spriteBatch.DrawString(font, attributeText, new GraphicsVector2(60, y), textColor);

                // Draw adjustment indicators
                if (isSelected)
                {
                    spriteBatch.DrawString(font, "< - >", new GraphicsVector2(350, y), GraphicsColor.Gray);
                }
            }

            // Render step title
            spriteBatch.DrawString(font, "Adjust Attributes", new GraphicsVector2(50, 70), GraphicsColor.White);
        }

        /// <summary>
        /// Renders the skills UI.
        /// Based on swkotor.exe and swkotor2.exe: Skills are displayed with current ranks and available points
        /// - Skills are based on class and Intelligence modifier
        /// - Available skill points are calculated and displayed
        /// - Each skill shows name, current rank, and adjustment controls
        /// - Class skills are highlighted differently from cross-class skills
        /// </summary>
        private void RenderSkillsUI(ISpriteBatch spriteBatch, IFont font)
        {
            if (font == null)
            {
                return;
            }

            // Render step title
            spriteBatch.DrawString(font, "Allocate Skill Points", new GraphicsVector2(50, 50), GraphicsColor.White);

            // Render instruction text
            string instructionText = "Use Up/Down to navigate, Left/Right to adjust ranks, Enter/Space to continue";
            spriteBatch.DrawString(font, instructionText, new GraphicsVector2(50, 80), GraphicsColor.Gray);

            // Render available skill points
            string pointsText = $"Available Skill Points: {_availableSkillPoints}";
            GraphicsColor pointsColor = _availableSkillPoints > 0 ? GraphicsColor.Cyan : GraphicsColor.Red;
            spriteBatch.DrawString(font, pointsText, new GraphicsVector2(50, 105), pointsColor);

            // Get class ID for class skill checking
            int classId = GetClassId(_characterData.Class);

            // Render skill list
            int listStartY = 135;
            int itemHeight = 30;
            int maxVisibleItems = 8;
            int listWidth = 500;

            // Calculate visible range
            int visibleStart = _skillScrollOffset;
            int visibleEnd = Math.Min(visibleStart + maxVisibleItems, _availableSkillIds.Count);

            // Render visible skills
            for (int i = visibleStart; i < visibleEnd; i++)
            {
                int skillId = _availableSkillIds[i];
                GameDataManager.SkillData skillData = _gameDataManager.GetSkill(skillId);
                if (skillData == null)
                {
                    continue;
                }

                bool isSelected = (i == _selectedSkillIndex);
                int currentRank = _characterData.SkillRanks.ContainsKey(skillId) ? _characterData.SkillRanks[skillId] : 0;
                bool isClassSkill = _gameDataManager.IsClassSkill(skillId, classId);
                int maxRank = isClassSkill ? 4 : 2;
                int pointCost = isClassSkill ? 1 : 2;
                bool canIncrease = _availableSkillPoints >= pointCost && currentRank < maxRank;
                bool canDecrease = currentRank > 0;

                int y = listStartY + ((i - visibleStart) * itemHeight);

                // Draw selection indicator background
                if (isSelected)
                {
                    DrawRectangle(spriteBatch, new Rectangle(45, y - 2, listWidth, itemHeight), new GraphicsColor(100, 100, 100, 150));
                }

                // Draw class skill indicator (different background for class skills)
                if (isClassSkill)
                {
                    DrawRectangle(spriteBatch, new Rectangle(45, y - 2, 20, itemHeight), new GraphicsColor(40, 80, 120, 200));
                }

                // Determine text color based on state
                GraphicsColor textColor = GraphicsColor.White;
                if (isSelected)
                {
                    textColor = GraphicsColor.Yellow;
                }
                else if (isClassSkill)
                {
                    textColor = new GraphicsColor(144, 200, 238, 255); // Light blue for class skills
                }

                // Render skill name
                string skillName = skillData.Name ?? $"Skill {skillId}";
                if (skillName.Length > 25)
                {
                    skillName = skillName.Substring(0, 22) + "...";
                }

                // Render class skill indicator
                string classSkillIndicator = isClassSkill ? "[C]" : "[X]";
                GraphicsColor indicatorColor = isClassSkill ? new GraphicsColor(144, 200, 238, 255) : GraphicsColor.Gray;
                spriteBatch.DrawString(font, classSkillIndicator, new GraphicsVector2(50, y), indicatorColor);

                // Render skill name
                spriteBatch.DrawString(font, skillName, new GraphicsVector2(90, y), textColor);

                // Render rank display
                string rankText = $"Rank: {currentRank}/{maxRank}";
                spriteBatch.DrawString(font, rankText, new GraphicsVector2(280, y), textColor);

                // Render adjustment controls
                if (isSelected)
                {
                    // Render decrease button indicator
                    if (canDecrease)
                    {
                        spriteBatch.DrawString(font, "<", new GraphicsVector2(380, y), GraphicsColor.White);
                    }
                    else
                    {
                        spriteBatch.DrawString(font, "<", new GraphicsVector2(380, y), new GraphicsColor(100, 100, 100, 255));
                    }

                    // Render increase button indicator
                    if (canIncrease)
                    {
                        spriteBatch.DrawString(font, ">", new GraphicsVector2(400, y), GraphicsColor.White);
                    }
                    else
                    {
                        spriteBatch.DrawString(font, ">", new GraphicsVector2(400, y), new GraphicsColor(100, 100, 100, 255));
                    }

                    // Render point cost
                    string costText = $"({pointCost} pt)";
                    spriteBatch.DrawString(font, costText, new GraphicsVector2(420, y), GraphicsColor.Gray);
                }
            }

            // Render detailed description for selected skill
            if (_selectedSkillIndex >= 0 && _selectedSkillIndex < _availableSkillIds.Count)
            {
                int selectedSkillId = _availableSkillIds[_selectedSkillIndex];
                GameDataManager.SkillData selectedSkillData = _gameDataManager.GetSkill(selectedSkillId);
                if (selectedSkillData != null)
                {
                    int descriptionX = 560;
                    int descriptionY = 135;
                    int descriptionWidth = 220;

                    // Draw description panel background
                    DrawRectangle(spriteBatch, new Rectangle(descriptionX - 5, descriptionY - 5, descriptionWidth + 10, 200), new GraphicsColor(30, 30, 30, 220));
                    DrawRectangleOutline(spriteBatch, new Rectangle(descriptionX - 5, descriptionY - 5, descriptionWidth + 10, 200), GraphicsColor.White, 2);

                    // Render skill name
                    string descSkillName = selectedSkillData.Name ?? $"Skill {selectedSkillId}";
                    spriteBatch.DrawString(font, descSkillName, new GraphicsVector2(descriptionX, descriptionY), GraphicsColor.White);
                    descriptionY += 25;

                    // Render description
                    string description = selectedSkillData.Description ?? "No description available.";
                    // Word wrap description (simple implementation)
                    string[] words = description.Split(' ');
                    string currentLine = string.Empty;
                    int lineHeight = 18;
                    int maxCharsPerLine = descriptionWidth / 7; // Rough estimate

                    foreach (string word in words)
                    {
                        string testLine = currentLine + (currentLine.Length > 0 ? " " : "") + word;
                        if (testLine.Length > maxCharsPerLine && currentLine.Length > 0)
                        {
                            spriteBatch.DrawString(font, currentLine, new GraphicsVector2(descriptionX, descriptionY), new GraphicsColor(211, 211, 211, 255)); // Light gray
                            descriptionY += lineHeight;
                            currentLine = word;
                        }
                        else
                        {
                            currentLine = testLine;
                        }

                        if (descriptionY > 135 + 170) // Stop if we run out of space
                        {
                            break;
                        }
                    }
                    if (currentLine.Length > 0 && descriptionY <= 135 + 170)
                    {
                        spriteBatch.DrawString(font, currentLine, new GraphicsVector2(descriptionX, descriptionY), new GraphicsColor(211, 211, 211, 255)); // Light gray
                    }

                    // Render class skill info
                    descriptionY += 20;
                    bool isClassSkill = _gameDataManager.IsClassSkill(selectedSkillId, classId);
                    string classSkillText = isClassSkill ? "Class Skill (1 point per rank)" : "Cross-Class Skill (2 points per rank)";
                    GraphicsColor classSkillColor = isClassSkill ? new GraphicsColor(144, 200, 238, 255) : GraphicsColor.Gray;
                    spriteBatch.DrawString(font, classSkillText, new GraphicsVector2(descriptionX, descriptionY), classSkillColor);
                }
            }
        }

        /// <summary>
        /// Renders the feats UI.
        /// Based on swkotor.exe and swkotor2.exe: Feats are displayed with available feats for selection
        /// - Feats are based on class (starting feats from featgain.2da)
        /// - Available feats are listed with names, descriptions, and selection status
        /// - Selected feats are highlighted
        /// - Original implementation: Feat list scrolls, descriptions shown in detail panel
        /// </summary>
        private void RenderFeatsUI(ISpriteBatch spriteBatch, IFont font)
        {
            if (font == null)
            {
                return;
            }

            // Render step title
            spriteBatch.DrawString(font, "Select Feats", new GraphicsVector2(50, 50), GraphicsColor.White);

            // Render instruction text
            string instructionText = "Use Up/Down to navigate, Enter/Space to select/deselect, Tab to continue";
            spriteBatch.DrawString(font, instructionText, new GraphicsVector2(50, 80), GraphicsColor.Gray);

            // Render selected feats count
            string selectedCountText = $"Selected: {_characterData.SelectedFeats.Count}";
            spriteBatch.DrawString(font, selectedCountText, new GraphicsVector2(50, 105), GraphicsColor.Cyan);

            // Render feat list
            int listStartY = 135;
            int itemHeight = 30;
            int maxVisibleItems = 12;
            int listWidth = 450;

            // Calculate visible range
            int visibleStart = _featScrollOffset;
            int visibleEnd = Math.Min(visibleStart + maxVisibleItems, _availableFeatIds.Count);

            // Render scrollbar hint if needed
            if (_availableFeatIds.Count > maxVisibleItems)
            {
                string scrollHint = $"Showing {visibleStart + 1}-{visibleEnd} of {_availableFeatIds.Count}";
                spriteBatch.DrawString(font, scrollHint, new GraphicsVector2(50, listStartY + (maxVisibleItems * itemHeight) + 5), new GraphicsColor(100, 100, 100, 255));
            }

            // Render visible feats
            for (int i = visibleStart; i < visibleEnd; i++)
            {
                int featId = _availableFeatIds[i];
                GameDataManager.FeatData featData = _gameDataManager.GetFeat(featId);
                if (featData == null)
                {
                    continue;
                }

                bool isSelected = (i == _selectedFeatIndex);
                bool isTaken = _characterData.SelectedFeats.Contains(featId);
                bool meetsPrereqs = MeetsFeatPrerequisites(featData);

                int y = listStartY + ((i - visibleStart) * itemHeight);

                // Draw selection indicator background
                if (isSelected)
                {
                    DrawRectangle(spriteBatch, new Rectangle(45, y - 2, listWidth, itemHeight), new GraphicsColor(100, 100, 100, 150));
                }

                // Draw taken indicator (different background for selected feats)
                if (isTaken)
                {
                    DrawRectangle(spriteBatch, new Rectangle(45, y - 2, 20, itemHeight), new GraphicsColor(40, 120, 40, 200));
                }

                // Determine text color based on state
                GraphicsColor textColor = GraphicsColor.White;
                if (!meetsPrereqs)
                {
                    textColor = new GraphicsColor(100, 100, 100, 255); // Gray out feats that don't meet prerequisites
                }
                else if (isTaken)
                {
                    textColor = new GraphicsColor(144, 238, 144, 255); // Light green for selected feats
                }
                else if (isSelected)
                {
                    textColor = GraphicsColor.Yellow; // Yellow for currently selected item
                }

                // Render feat name
                string featName = featData.Name ?? $"Feat {featId}";
                if (featName.Length > 40)
                {
                    featName = featName.Substring(0, 37) + "...";
                }

                // Render selection indicator
                string indicator = isTaken ? "[X]" : "[ ]";
                spriteBatch.DrawString(font, indicator, new GraphicsVector2(50, y), textColor);

                spriteBatch.DrawString(font, featName, new GraphicsVector2(90, y), textColor);

                // Render prerequisite warning if not met
                if (!meetsPrereqs && isSelected)
                {
                    spriteBatch.DrawString(font, " (Prerequisites not met)", new GraphicsVector2(90 + font.MeasureString(featName).X, y), GraphicsColor.Red);
                }
            }

            // Render detailed description for selected feat
            if (_selectedFeatIndex >= 0 && _selectedFeatIndex < _availableFeatIds.Count)
            {
                int selectedFeatId = _availableFeatIds[_selectedFeatIndex];
                GameDataManager.FeatData selectedFeatData = _gameDataManager.GetFeat(selectedFeatId);
                if (selectedFeatData != null)
                {
                    int descriptionX = 520;
                    int descriptionY = 135;
                    int descriptionWidth = 250;

                    // Draw description panel background
                    DrawRectangle(spriteBatch, new Rectangle(descriptionX - 5, descriptionY - 5, descriptionWidth + 10, 250), new GraphicsColor(30, 30, 30, 220));
                    DrawRectangleOutline(spriteBatch, new Rectangle(descriptionX - 5, descriptionY - 5, descriptionWidth + 10, 250), GraphicsColor.White, 2);

                    // Render feat name
                    string descFeatName = selectedFeatData.Name ?? $"Feat {selectedFeatId}";
                    spriteBatch.DrawString(font, descFeatName, new GraphicsVector2(descriptionX, descriptionY), GraphicsColor.White);
                    descriptionY += 25;

                    // Render description
                    string description = selectedFeatData.Description ?? "No description available.";
                    // Word wrap description (simple implementation)
                    string[] words = description.Split(' ');
                    string currentLine = string.Empty;
                    int lineHeight = 20;
                    int maxCharsPerLine = descriptionWidth / 8; // Rough estimate

                    foreach (string word in words)
                    {
                        string testLine = currentLine + (currentLine.Length > 0 ? " " : "") + word;
                        if (testLine.Length > maxCharsPerLine && currentLine.Length > 0)
                        {
                            spriteBatch.DrawString(font, currentLine, new GraphicsVector2(descriptionX, descriptionY), new GraphicsColor(211, 211, 211, 255)); // Light gray
                            descriptionY += lineHeight;
                            currentLine = word;
                        }
                        else
                        {
                            currentLine = testLine;
                        }

                        if (descriptionY > 135 + 220) // Stop if we run out of space
                        {
                            break;
                        }
                    }
                    if (currentLine.Length > 0 && descriptionY <= 135 + 220)
                    {
                        spriteBatch.DrawString(font, currentLine, new GraphicsVector2(descriptionX, descriptionY), new GraphicsColor(211, 211, 211, 255)); // Light gray
                    }

                    // Render prerequisites
                    descriptionY += 30;
                    if (selectedFeatData.PrereqFeat1 >= 0)
                    {
                        GameDataManager.FeatData prereqFeat = _gameDataManager.GetFeat(selectedFeatData.PrereqFeat1);
                        string prereqName = prereqFeat?.Name ?? $"Feat {selectedFeatData.PrereqFeat1}";
                        bool hasPrereq = _characterData.SelectedFeats.Contains(selectedFeatData.PrereqFeat1);
                        GraphicsColor prereqColor = hasPrereq ? GraphicsColor.Green : GraphicsColor.Red;
                        spriteBatch.DrawString(font, $"Requires: {prereqName}", new GraphicsVector2(descriptionX, descriptionY), prereqColor);
                    }
                }
            }
        }

        /// <summary>
        /// Renders the portrait selection UI.
        /// Based on swkotor.exe and swkotor2.exe: Portrait selection displays available portraits
        /// - Portraits are loaded from game resources
        /// - Current portrait is highlighted
        /// </summary>
        private void RenderPortraitUI(ISpriteBatch spriteBatch, IFont font)
        {
            if (font == null)
            {
                return;
            }

            // Render step title
            spriteBatch.DrawString(font, "Select Portrait", new GraphicsVector2(50, 100), GraphicsColor.White);

            // Render current portrait number
            string portraitText = $"Portrait: {_characterData.Portrait}";
            spriteBatch.DrawString(font, portraitText, new GraphicsVector2(50, 150), GraphicsColor.White);

            // Render navigation hints
            spriteBatch.DrawString(font, "Use Left/Right arrows to change portrait", new GraphicsVector2(50, 200), GraphicsColor.Gray);

            // Render portrait thumbnail
            RenderPortraitThumbnail(spriteBatch);
        }

        /// <summary>
        /// Renders the portrait thumbnail for the currently selected portrait.
        /// Based on swkotor.exe and swkotor2.exe: Portrait thumbnails are displayed in character creation
        /// - Original implementation: Loads portrait texture from portraits.2da baseresref column
        /// - Portrait textures are TPC or TGA files stored in game resources
        /// - Thumbnails are rendered at a fixed size (typically 128x128 or 256x256 pixels)
        /// - Current portrait is highlighted with a border or selection indicator
        /// </summary>
        private void RenderPortraitThumbnail(ISpriteBatch spriteBatch)
        {
            if (spriteBatch == null)
            {
                return;
            }

            // Render portrait thumbnail
            // Based on swkotor.exe and swkotor2.exe: Portrait thumbnails are typically 128x128 or 256x256 pixels
            // Original implementation: Portraits are rendered at fixed size with selection border
            int thumbnailX = 50;
            int thumbnailY = 250;
            int thumbnailSize = 128; // Standard portrait thumbnail size

            // Get portrait data from portraits.2da
            GameDataManager.PortraitData portraitData = _gameDataManager.GetPortrait(_characterData.Portrait);
            if (portraitData == null || string.IsNullOrEmpty(portraitData.BaseResRef))
            {
                // Render placeholder if portrait not found
                DrawRectangle(spriteBatch, new Rectangle(thumbnailX, thumbnailY, thumbnailSize, thumbnailSize), new GraphicsColor(50, 50, 50, 200));
                DrawRectangleOutline(spriteBatch, new Rectangle(thumbnailX, thumbnailY, thumbnailSize, thumbnailSize), GraphicsColor.Gray, 2);
                return;
            }

            // Load or get cached portrait texture
            ITexture2D portraitTexture = LoadPortraitTexture(_characterData.Portrait, portraitData.BaseResRef);
            if (portraitTexture == null)
            {
                // Render placeholder if texture failed to load
                DrawRectangle(spriteBatch, new Rectangle(thumbnailX, thumbnailY, thumbnailSize, thumbnailSize), new GraphicsColor(50, 50, 50, 200));
                DrawRectangleOutline(spriteBatch, new Rectangle(thumbnailX, thumbnailY, thumbnailSize, thumbnailSize), GraphicsColor.Gray, 2);
                return;
            }

            // Draw selection border (highlight current portrait)
            GraphicsColor selectionColor = GraphicsColor.Yellow;
            int borderThickness = 3;
            DrawRectangleOutline(spriteBatch, new Rectangle(thumbnailX - borderThickness, thumbnailY - borderThickness, thumbnailSize + (borderThickness * 2), thumbnailSize + (borderThickness * 2)), selectionColor, borderThickness);

            // Draw portrait texture
            Rectangle thumbnailRect = new Rectangle(thumbnailX, thumbnailY, thumbnailSize, thumbnailSize);
            spriteBatch.Draw(portraitTexture, thumbnailRect, GraphicsColor.White);
        }

        /// <summary>
        /// Loads a portrait texture from game resources and caches it.
        /// Based on swkotor.exe and swkotor2.exe: Portrait textures are loaded from TPC/TGA files
        /// - Original implementation: Loads portrait texture from installation using ResRef
        /// - Portraits are stored in PORTRAITS search location or CHITIN archives
        /// - Textures are cached to avoid redundant loading
        /// - TPC format is converted to RGBA byte array for rendering
        /// </summary>
        /// <param name="portraitId">Portrait ID for caching.</param>
        /// <param name="portraitResRef">Portrait resource reference (ResRef).</param>
        /// <returns>Loaded texture, or null if loading failed.</returns>
        private ITexture2D LoadPortraitTexture(int portraitId, string portraitResRef)
        {
            if (string.IsNullOrEmpty(portraitResRef))
            {
                return null;
            }

            // Check cache first
            if (_portraitTextureCache.TryGetValue(portraitId, out ITexture2D cached))
            {
                return cached;
            }

            try
            {
                // Load portrait texture from installation
                // Based on swkotor.exe and swkotor2.exe: Portraits are searched in PORTRAITS, OVERRIDE, and CHITIN locations
                // Original implementation: Searches for TPC first, then TGA, in multiple search locations
                TPC tpcTexture = _installation.Texture(
                    portraitResRef,
                    new[]
                    {
                        Andastra.Parsing.Installation.SearchLocation.OVERRIDE,
                        Andastra.Parsing.Installation.SearchLocation.CUSTOM_FOLDERS,
                        Andastra.Parsing.Installation.SearchLocation.CHITIN
                    }
                );

                if (tpcTexture == null)
                {
                    return null;
                }

                // Convert TPC to ITexture2D
                // Based on swkotor.exe and swkotor2.exe: TPC textures are converted to DirectX textures for rendering
                // Original implementation: TPC mipmaps are decompressed and uploaded to GPU as RGBA textures
                ITexture2D texture = ConvertTpcToTexture2D(tpcTexture);

                if (texture != null)
                {
                    // Cache the loaded texture
                    _portraitTextureCache[portraitId] = texture;
                }

                return texture;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[CharacterCreationScreen] Failed to load portrait texture '{portraitResRef}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Converts a TPC texture to ITexture2D for rendering.
        /// Based on swkotor.exe and swkotor2.exe: TPC textures are converted to DirectX textures
        /// - Original implementation: TPC mipmaps are decompressed (DXT1/DXT3/DXT5) or converted (BGR/BGRA to RGB/RGBA)
        /// - First mipmap (largest) is used for portrait thumbnails
        /// - Texture format is converted to RGBA for compatibility with rendering system
        /// </summary>
        /// <param name="tpc">TPC texture to convert.</param>
        /// <returns>Converted texture, or null if conversion failed.</returns>
        private ITexture2D ConvertTpcToTexture2D(TPC tpc)
        {
            if (tpc == null || tpc.Layers == null || tpc.Layers.Count == 0)
            {
                return null;
            }

            // Get first layer (largest mipmap)
            TPCLayer layer = tpc.Layers[0];
            if (layer == null || layer.Mipmaps == null || layer.Mipmaps.Count == 0)
            {
                return null;
            }

            // Get first mipmap (largest resolution)
            TPCMipmap mipmap = layer.Mipmaps[0];
            if (mipmap == null || mipmap.Data == null || mipmap.Data.Length == 0)
            {
                return null;
            }

            int width = mipmap.Width;
            int height = mipmap.Height;
            TPCTextureFormat format = mipmap.TpcFormat;

            // Convert TPC format to RGBA byte array
            byte[] rgbaData = ConvertTpcDataToRgba(mipmap.Data, width, height, format);
            if (rgbaData == null)
            {
                return null;
            }

            // Create texture from RGBA data
            return _graphicsDevice.CreateTexture2D(width, height, rgbaData);
        }

        /// <summary>
        /// Converts TPC pixel data to RGBA byte array.
        /// Based on swkotor.exe and swkotor2.exe: TPC formats are converted to RGBA for rendering
        /// - Original implementation: DXT formats are decompressed, BGR/BGRA are converted to RGB/RGBA
        /// - Greyscale is expanded to RGBA
        /// - RGB/RGBA formats are used directly (may need byte order conversion)
        /// </summary>
        /// <param name="tpcData">TPC pixel data.</param>
        /// <param name="width">Texture width.</param>
        /// <param name="height">Texture height.</param>
        /// <param name="format">TPC texture format.</param>
        /// <returns>RGBA byte array, or null if conversion failed.</returns>
        private byte[] ConvertTpcDataToRgba(byte[] tpcData, int width, int height, TPCTextureFormat format)
        {
            if (tpcData == null || tpcData.Length == 0 || width <= 0 || height <= 0)
            {
                return null;
            }

            int pixelCount = width * height;
            byte[] rgbaData = new byte[pixelCount * 4]; // RGBA = 4 bytes per pixel

            try
            {
                switch (format)
                {
                    case TPCTextureFormat.RGBA:
                        // Already RGBA, copy directly
                        if (tpcData.Length >= pixelCount * 4)
                        {
                            System.Buffer.BlockCopy(tpcData, 0, rgbaData, 0, pixelCount * 4);
                        }
                        break;

                    case TPCTextureFormat.RGB:
                        // Convert RGB to RGBA (add alpha = 255)
                        if (tpcData.Length >= pixelCount * 3)
                        {
                            for (int i = 0; i < pixelCount; i++)
                            {
                                rgbaData[i * 4] = tpcData[i * 3];         // R
                                rgbaData[i * 4 + 1] = tpcData[i * 3 + 1]; // G
                                rgbaData[i * 4 + 2] = tpcData[i * 3 + 2]; // B
                                rgbaData[i * 4 + 3] = 255;                 // A
                            }
                        }
                        break;

                    case TPCTextureFormat.BGRA:
                        // Convert BGRA to RGBA (swap R and B)
                        if (tpcData.Length >= pixelCount * 4)
                        {
                            for (int i = 0; i < pixelCount; i++)
                            {
                                rgbaData[i * 4] = tpcData[i * 4 + 2];     // R = B
                                rgbaData[i * 4 + 1] = tpcData[i * 4 + 1]; // G = G
                                rgbaData[i * 4 + 2] = tpcData[i * 4];     // B = R
                                rgbaData[i * 4 + 3] = tpcData[i * 4 + 3]; // A = A
                            }
                        }
                        break;

                    case TPCTextureFormat.BGR:
                        // Convert BGR to RGBA (swap R and B, add alpha = 255)
                        if (tpcData.Length >= pixelCount * 3)
                        {
                            for (int i = 0; i < pixelCount; i++)
                            {
                                rgbaData[i * 4] = tpcData[i * 3 + 2];     // R = B
                                rgbaData[i * 4 + 1] = tpcData[i * 3 + 1]; // G = G
                                rgbaData[i * 4 + 2] = tpcData[i * 3];     // B = R
                                rgbaData[i * 4 + 3] = 255;                 // A
                            }
                        }
                        break;

                    case TPCTextureFormat.Greyscale:
                        // Convert greyscale to RGBA (R=G=B=greyscale, A=255)
                        if (tpcData.Length >= pixelCount)
                        {
                            for (int i = 0; i < pixelCount; i++)
                            {
                                byte gray = tpcData[i];
                                rgbaData[i * 4] = gray;     // R
                                rgbaData[i * 4 + 1] = gray; // G
                                rgbaData[i * 4 + 2] = gray; // B
                                rgbaData[i * 4 + 3] = 255;   // A
                            }
                        }
                        break;

                    case TPCTextureFormat.DXT1:
                        // Decompress DXT1 to RGBA
                        // Based on swkotor.exe and swkotor2.exe: DXT formats are decompressed by DirectX
                        // Original implementation: DirectX handles DXT decompression automatically
                        // This implementation: Software decompression using DXT algorithm
                        try
                        {
                            DxtDecompression.DecompressDxt1(tpcData, width, height, rgbaData);
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine($"[CharacterCreationScreen] Error decompressing DXT1: {ex.Message}");
                            return null;
                        }
                        break;

                    case TPCTextureFormat.DXT3:
                        // Decompress DXT3 to RGBA
                        // Based on swkotor.exe and swkotor2.exe: DXT formats are decompressed by DirectX
                        // Original implementation: DirectX handles DXT decompression automatically
                        // This implementation: Software decompression using DXT algorithm
                        try
                        {
                            DxtDecompression.DecompressDxt3(tpcData, width, height, rgbaData);
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine($"[CharacterCreationScreen] Error decompressing DXT3: {ex.Message}");
                            return null;
                        }
                        break;

                    case TPCTextureFormat.DXT5:
                        // Decompress DXT5 to RGBA
                        // Based on swkotor.exe and swkotor2.exe: DXT formats are decompressed by DirectX
                        // Original implementation: DirectX handles DXT decompression automatically
                        // This implementation: Software decompression using DXT algorithm
                        try
                        {
                            DxtDecompression.DecompressDxt5(tpcData, width, height, rgbaData);
                        }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine($"[CharacterCreationScreen] Error decompressing DXT5: {ex.Message}");
                            return null;
                        }
                        break;

                    default:
                        System.Console.WriteLine($"[CharacterCreationScreen] Unsupported TPC format: {format}");
                        return null;
                }

                return rgbaData;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[CharacterCreationScreen] Error converting TPC data to RGBA: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Renders the name entry UI.
        /// Based on swkotor.exe and swkotor2.exe: Name entry displays text input field
        /// - Current name is displayed and can be edited
        /// - Text input is handled in Update method
        /// </summary>
        private void RenderNameUI(ISpriteBatch spriteBatch, IFont font)
        {
            if (font == null)
            {
                return;
            }

            // Render step title
            spriteBatch.DrawString(font, "Enter Character Name", new GraphicsVector2(50, 100), GraphicsColor.White);

            // Render name input field background
            int inputX = 50;
            int inputY = 150;
            int inputWidth = 400;
            int inputHeight = 40;
            DrawRectangle(spriteBatch, new Rectangle(inputX, inputY, inputWidth, inputHeight), new GraphicsColor(50, 50, 50, 200));
            DrawRectangleOutline(spriteBatch, new Rectangle(inputX, inputY, inputWidth, inputHeight), GraphicsColor.White, 2);

            // Render current name
            string displayName = string.IsNullOrEmpty(_characterData.Name) ? "Enter name..." : _characterData.Name;
            GraphicsColor nameColor = string.IsNullOrEmpty(_characterData.Name) ? GraphicsColor.Gray : GraphicsColor.White;
            spriteBatch.DrawString(font, displayName, new GraphicsVector2(inputX + 10, inputY + 10), nameColor);

            // Render cursor (blinking)
            float time = (float)(DateTime.Now.Millisecond % 1000) / 1000.0f;
            if (time < 0.5f)
            {
                int cursorX = inputX + 10 + (int)font.MeasureString(displayName).X;
                spriteBatch.DrawString(font, "|", new GraphicsVector2(cursorX, inputY + 10), GraphicsColor.White);
            }
        }

        /// <summary>
        /// Renders the summary UI.
        /// Based on swkotor.exe and swkotor2.exe: Summary displays all character creation choices
        /// - Shows class, attributes, skills, feats, portrait, and name
        /// - Allows final review before completing character creation
        /// </summary>
        private void RenderSummaryUI(ISpriteBatch spriteBatch, IFont font)
        {
            if (font == null)
            {
                return;
            }

            // Render step title
            spriteBatch.DrawString(font, "Character Summary", new GraphicsVector2(50, 50), GraphicsColor.White);

            int y = 100;
            int lineHeight = 25;

            // Render character information
            spriteBatch.DrawString(font, $"Name: {_characterData.Name}", new GraphicsVector2(50, y), GraphicsColor.White);
            y += lineHeight;

            spriteBatch.DrawString(font, $"Class: {GetClassName(_characterData.Class)}", new GraphicsVector2(50, y), GraphicsColor.White);
            y += lineHeight;

            spriteBatch.DrawString(font, $"Gender: {_characterData.Gender}", new GraphicsVector2(50, y), GraphicsColor.White);
            y += lineHeight;

            spriteBatch.DrawString(font, $"Portrait: {_characterData.Portrait}", new GraphicsVector2(50, y), GraphicsColor.White);
            y += lineHeight * 2;

            // Render attributes
            spriteBatch.DrawString(font, "Attributes:", new GraphicsVector2(50, y), GraphicsColor.Cyan);
            y += lineHeight;
            spriteBatch.DrawString(font, $"  STR: {_characterData.Strength}", new GraphicsVector2(70, y), GraphicsColor.White);
            y += lineHeight;
            spriteBatch.DrawString(font, $"  DEX: {_characterData.Dexterity}", new GraphicsVector2(70, y), GraphicsColor.White);
            y += lineHeight;
            spriteBatch.DrawString(font, $"  CON: {_characterData.Constitution}", new GraphicsVector2(70, y), GraphicsColor.White);
            y += lineHeight;
            spriteBatch.DrawString(font, $"  INT: {_characterData.Intelligence}", new GraphicsVector2(70, y), GraphicsColor.White);
            y += lineHeight;
            spriteBatch.DrawString(font, $"  WIS: {_characterData.Wisdom}", new GraphicsVector2(70, y), GraphicsColor.White);
            y += lineHeight;
            spriteBatch.DrawString(font, $"  CHA: {_characterData.Charisma}", new GraphicsVector2(70, y), GraphicsColor.White);
            y += lineHeight * 2;

            // Render skills
            spriteBatch.DrawString(font, "Skills:", new GraphicsVector2(50, y), GraphicsColor.Cyan);
            y += lineHeight;

            int classId = GetClassId(_characterData.Class);
            string[] skillNames = { "Computer Use", "Demolitions", "Stealth", "Awareness", "Persuade", "Repair", "Security", "Treat Injury" };
            for (int i = 0; i < 8; i++)
            {
                int rank = _characterData.SkillRanks.ContainsKey(i) ? _characterData.SkillRanks[i] : 0;
                bool isClassSkill = _gameDataManager.IsClassSkill(i, classId);
                string classSkillIndicator = isClassSkill ? " [C]" : " [X]";
                spriteBatch.DrawString(font, $"  {skillNames[i]}: {rank}{classSkillIndicator}", new GraphicsVector2(70, y), GraphicsColor.White);
                y += lineHeight;
            }
            y += lineHeight;

            // Render completion hint
            spriteBatch.DrawString(font, "Press Enter to finish character creation", new GraphicsVector2(50, y), GraphicsColor.Yellow);
        }

        /// <summary>
        /// Renders navigation buttons (Next, Back, Cancel, Finish).
        /// Based on swkotor.exe and swkotor2.exe: Navigation buttons are always visible at the bottom of the screen
        /// - Next: Advances to next step (or finishes on summary step)
        /// - Back: Returns to previous step
        /// - Cancel: Cancels character creation and returns to main menu
        /// - Finish: Completes character creation (only on summary step)
        /// </summary>
        private void RenderNavigationButtons(ISpriteBatch spriteBatch, IFont font)
        {
            if (font == null)
            {
                return;
            }

            int buttonY = _graphicsDevice.Viewport.Height - 60;
            int buttonWidth = 100;
            int buttonHeight = 40;
            int buttonSpacing = 20;

            // Render Back button (if not on first step)
            if (_currentStep != CreationStep.ClassSelection)
            {
                int backX = 50;
                DrawRectangle(spriteBatch, new Rectangle(backX, buttonY, buttonWidth, buttonHeight), new GraphicsColor(80, 80, 80, 200));
                DrawRectangleOutline(spriteBatch, new Rectangle(backX, buttonY, buttonWidth, buttonHeight), GraphicsColor.White, 2);
                spriteBatch.DrawString(font, "Back", new GraphicsVector2(backX + 25, buttonY + 10), GraphicsColor.White);
            }

            // Render Cancel button
            int cancelX = _currentStep != CreationStep.ClassSelection ? 170 : 50;
            DrawRectangle(spriteBatch, new Rectangle(cancelX, buttonY, buttonWidth, buttonHeight), new GraphicsColor(120, 40, 40, 200));
            DrawRectangleOutline(spriteBatch, new Rectangle(cancelX, buttonY, buttonWidth, buttonHeight), GraphicsColor.White, 2);
            spriteBatch.DrawString(font, "Cancel", new GraphicsVector2(cancelX + 15, buttonY + 10), GraphicsColor.White);

            // Render Next/Finish button
            string nextButtonText = (_currentStep == CreationStep.Summary) ? "Finish" : "Next";
            int nextX = _graphicsDevice.Viewport.Width - buttonWidth - 50;
            GraphicsColor nextButtonColor = (_currentStep == CreationStep.Summary) ? new GraphicsColor(40, 120, 40, 200) : new GraphicsColor(80, 80, 80, 200);
            DrawRectangle(spriteBatch, new Rectangle(nextX, buttonY, buttonWidth, buttonHeight), nextButtonColor);
            DrawRectangleOutline(spriteBatch, new Rectangle(nextX, buttonY, buttonWidth, buttonHeight), GraphicsColor.White, 2);
            spriteBatch.DrawString(font, nextButtonText, new GraphicsVector2(nextX + 20, buttonY + 10), GraphicsColor.White);
        }

        /// <summary>
        /// Draws a filled rectangle using a 1x1 pixel texture.
        /// </summary>
        private void DrawRectangle(ISpriteBatch spriteBatch, Rectangle rect, Andastra.Runtime.Graphics.Color color)
        {
            if (_pixelTexture == null)
            {
                return;
            }
            spriteBatch.Draw(_pixelTexture, rect, color);
        }

        /// <summary>
        /// Draws a rectangle outline using a 1x1 pixel texture.
        /// </summary>
        private void DrawRectangleOutline(ISpriteBatch spriteBatch, Rectangle rect, Andastra.Runtime.Graphics.Color color, int thickness)
        {
            if (_pixelTexture == null)
            {
                return;
            }
            // Draw top edge
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            // Draw bottom edge
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
            // Draw left edge
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            // Draw right edge
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
        }

        /// <summary>
        /// Converts CharacterClass enum to class ID used in classes.2da.
        /// Based on nwscript.nss constants: CLASS_TYPE_SOLDIER=0, CLASS_TYPE_SCOUT=1, etc.
        /// </summary>
        private int GetClassId(CharacterClass characterClass)
        {
            switch (characterClass)
            {
                case CharacterClass.Soldier:
                    return 0;
                case CharacterClass.Scout:
                    return 1;
                case CharacterClass.Scoundrel:
                    return 2;
                case CharacterClass.JediGuardian:
                    return 3;
                case CharacterClass.JediConsular:
                    return 4;
                case CharacterClass.JediSentinel:
                    return 5;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Updates the available feats list based on current class.
        /// Based on swkotor.exe and swkotor2.exe: FUN_0060d1d0 (LoadFeatGain) loads starting feats from featgain.2da
        /// - Original implementation: Gets starting feats from featgain.2da _REG and _BON columns
        /// - Feats are filtered to only include selectable feats that meet prerequisites
        /// </summary>
        private void UpdateAvailableFeats()
        {
            _availableFeatIds.Clear();

            // Get starting feats for the current class
            int classId = GetClassId(_characterData.Class);
            List<int> startingFeats = _gameDataManager.GetStartingFeats(classId);

            // Add all starting feats that are selectable
            foreach (int featId in startingFeats)
            {
                GameDataManager.FeatData featData = _gameDataManager.GetFeat(featId);
                if (featData != null && featData.Selectable)
                {
                    _availableFeatIds.Add(featId);
                }
            }

            // Sort by feat ID for consistent display
            _availableFeatIds.Sort();

            // Reset selection state
            _selectedFeatIndex = 0;
            _featScrollOffset = 0;
        }

        /// <summary>
        /// Checks if a feat's prerequisites are met.
        /// Based on swkotor.exe and swkotor2.exe: Feat prerequisite checking in character creation
        /// - Original implementation: Checks if prerequisite feats are in SelectedFeats list
        /// - Also checks attribute requirements (minlevel, minstr, mindex, etc.) based on current character attributes
        /// </summary>
        private bool MeetsFeatPrerequisites(GameDataManager.FeatData featData)
        {
            if (featData == null)
            {
                return false;
            }

            // Check prerequisite feats
            if (featData.PrereqFeat1 >= 0 && !_characterData.SelectedFeats.Contains(featData.PrereqFeat1))
            {
                return false;
            }

            if (featData.PrereqFeat2 >= 0 && !_characterData.SelectedFeats.Contains(featData.PrereqFeat2))
            {
                return false;
            }

            // Check minimum level (character is level 1 during creation, so minlevel should be <= 1)
            if (featData.MinLevel > 1)
            {
                return false;
            }

            // Check attribute requirements (minstr, mindex, minint, minwis, mincon, mincha)
            // Based on swkotor.exe and swkotor2.exe: Attribute requirements checked during feat selection
            // Original implementation: Compares character attributes against feat.2da minstr/mindex/etc. columns
            if (featData.MinStr > 0 && _characterData.Strength < featData.MinStr)
            {
                return false;
            }
            if (featData.MinDex > 0 && _characterData.Dexterity < featData.MinDex)
            {
                return false;
            }
            if (featData.MinInt > 0 && _characterData.Intelligence < featData.MinInt)
            {
                return false;
            }
            if (featData.MinWis > 0 && _characterData.Wisdom < featData.MinWis)
            {
                return false;
            }
            if (featData.MinCon > 0 && _characterData.Constitution < featData.MinCon)
            {
                return false;
            }
            if (featData.MinCha > 0 && _characterData.Charisma < featData.MinCha)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the display name for a character class.
        /// </summary>
        private string GetClassName(CharacterClass characterClass)
        {
            switch (characterClass)
            {
                case CharacterClass.Scout:
                    return "Scout";
                case CharacterClass.Soldier:
                    return "Soldier";
                case CharacterClass.Scoundrel:
                    return "Scoundrel";
                case CharacterClass.JediGuardian:
                    return "Jedi Guardian";
                case CharacterClass.JediSentinel:
                    return "Jedi Sentinel";
                case CharacterClass.JediConsular:
                    return "Jedi Consular";
                default:
                    return "Unknown";
            }
        }

        /// <summary>
        /// Completes character creation and calls the completion callback.
        /// </summary>
        private void Finish()
        {
            // Validate character data
            if (string.IsNullOrWhiteSpace(_characterData.Name))
            {
                _characterData.Name = "Player"; // Default name
            }

            // Call completion callback
            _onComplete(_characterData);
        }

        /// <summary>
        /// Cancels character creation and returns to main menu.
        /// </summary>
        private void Cancel()
        {
            _onCancel();
        }
    }

    /// <summary>
    /// Data structure for character creation.
    /// </summary>
    public class CharacterCreationData
    {
        public KotorGame Game { get; set; }
        public CharacterClass Class { get; set; }
        public Gender Gender { get; set; }
        public int Appearance { get; set; }
        public int Portrait { get; set; }
        public string Name { get; set; }
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Constitution { get; set; }
        public int Intelligence { get; set; }
        public int Wisdom { get; set; }
        public int Charisma { get; set; }
        /// <summary>
        /// List of selected feat IDs during character creation.
        /// Based on swkotor.exe and swkotor2.exe: Character creation stores selected feats in character data
        /// - Original implementation: Selected feats are stored as list of feat IDs from feat.2da
        /// - Feats are added to creature's FeatList when character is created
        /// </summary>
        public List<int> SelectedFeats { get; set; }
        /// <summary>
        /// Skill ranks allocated during character creation.
        /// Based on swkotor.exe and swkotor2.exe: Character creation stores skill ranks in character data
        /// - Original implementation: Skill ranks are stored as dictionary mapping skill ID (0-7) to rank value
        /// - Skills are set on creature's StatsComponent when character is created
        /// - Skill ranks: 0 = untrained, 1-4 for class skills, 1-2 for cross-class skills at level 1
        /// </summary>
        public Dictionary<int, int> SkillRanks { get; set; }
    }

    /// <summary>
    /// Character class enumeration.
    /// </summary>
    public enum CharacterClass
    {
        Scout,          // K1 only
        Soldier,        // K1 only
        Scoundrel,      // K1 only
        JediGuardian,   // K2 only
        JediSentinel,   // K2 only
        JediConsular    // K2 only
    }

    /// <summary>
    /// Gender enumeration.
    /// </summary>
    public enum Gender
    {
        Male,
        Female
    }
}

