using System;
using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Runtime.Core;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Graphics;

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
        private readonly KotorGame _game;
        private readonly Action<CharacterCreationData> _onComplete;
        private readonly Action _onCancel;
        
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
            if (_game == KotorGame.K1)
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
        public CharacterCreationScreen(
            IGraphicsDevice graphicsDevice,
            Installation installation,
            KotorGame game,
            Action<CharacterCreationData> onComplete,
            Action onCancel)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _installation = installation ?? throw new ArgumentNullException(nameof(installation));
            _game = game;
            _onComplete = onComplete ?? throw new ArgumentNullException(nameof(onComplete));
            _onCancel = onCancel ?? throw new ArgumentNullException(nameof(onCancel));
            
            // Initialize character data with defaults
            _characterData = new CharacterCreationData
            {
                Game = game,
                Class = game == KotorGame.K1 ? CharacterClass.Scout : CharacterClass.JediGuardian,
                Gender = Gender.Male,
                Appearance = 1,
                Portrait = 0,
                Name = string.Empty,
                Strength = 14,
                Dexterity = 12,
                Constitution = 12,
                Intelligence = 12,
                Wisdom = 12,
                Charisma = 12
            };
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
                _needsModelUpdate = true;
            }
            else if (IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Down) || IsKeyPressed(keyboardState, _previousKeyboardState, Keys.Right))
            {
                _selectedClassIndex = (_selectedClassIndex + 1) % availableClasses.Length;
                _characterData.Class = availableClasses[_selectedClassIndex];
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
        /// </summary>
        private void HandleSkillsInput(IKeyboardState keyboardState, IMouseState mouseState)
        {
            // Skills are typically auto-calculated based on class and INT, but allow manual adjustment
            // For now, just allow navigation
            
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
        /// Handles input for feats step.
        /// </summary>
        private void HandleFeatsInput(IKeyboardState keyboardState, IMouseState mouseState)
        {
            // Feats are typically based on class, but allow selection
            // For now, just allow navigation
            
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
        public void Draw(ISpriteBatch spriteBatch, IFont font)
        {
            // TODO: Implement character creation UI rendering
            // Draw GUI panel "maincg"
            // Draw character model preview
            // Draw current step UI (class selection, attributes, etc.)
            // Draw buttons (Next, Back, Cancel, Finish)
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

