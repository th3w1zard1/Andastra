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
        private int _currentStep = 0;
        
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
        public void Update(float deltaTime, IKeyboardState keyboardState, IMouseState mouseState)
        {
            // TODO: Implement character creation UI updates
            // Handle input for current step
            // Update character model preview
            // Handle button clicks (Next, Back, Cancel, Finish)
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

