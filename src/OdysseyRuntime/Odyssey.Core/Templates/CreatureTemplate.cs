using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Entities;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Templates
{
    /// <summary>
    /// Creature template implementation for spawning creatures from UTC data.
    /// </summary>
    public class CreatureTemplate : ICreatureTemplate
    {
        #region Properties

        public string ResRef { get; set; }
        public string Tag { get; set; }
        public ObjectType ObjectType { get { return ObjectType.Creature; } }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int AppearanceId { get; set; }
        public int FactionId { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public string Conversation { get; set; }
        public bool IsPlot { get; set; }

        // Additional UTC properties
        public int PortraitId { get; set; }
        public int RaceId { get; set; }
        public int GenderId { get; set; }
        public int SubraceId { get; set; }
        public int PerceptionId { get; set; }
        public int WalkrateId { get; set; }
        public int SoundsetId { get; set; }
        public int BodyVariation { get; set; }
        public int TextureVariation { get; set; }
        public int Alignment { get; set; }
        public float ChallengeRating { get; set; }
        public int NaturalAc { get; set; }
        public int FortitudeBonus { get; set; }
        public int ReflexBonus { get; set; }
        public int WillBonus { get; set; }
        public int MaxFp { get; set; }
        public int CurrentFp { get; set; }

        // Ability scores
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Constitution { get; set; }
        public int Intelligence { get; set; }
        public int Wisdom { get; set; }
        public int Charisma { get; set; }

        // Skills
        public int ComputerUse { get; set; }
        public int Demolitions { get; set; }
        public int Stealth { get; set; }
        public int Awareness { get; set; }
        public int Persuade { get; set; }
        public int Repair { get; set; }
        public int Security { get; set; }
        public int TreatInjury { get; set; }

        // Script hooks
        public string OnSpawn { get; set; }
        public string OnHeartbeat { get; set; }
        public string OnNotice { get; set; }
        public string OnDialog { get; set; }
        public string OnAttacked { get; set; }
        public string OnDamaged { get; set; }
        public string OnDeath { get; set; }
        public string OnEndRound { get; set; }
        public string OnBlocked { get; set; }
        public string OnUserDefined { get; set; }

        // Classes, feats, powers
        public List<ClassInfo> Classes { get; set; }
        public List<int> Feats { get; set; }

        // Flags
        public bool Interruptable { get; set; }
        public bool NoPermDeath { get; set; }
        public bool Min1Hp { get; set; }
        public bool PartyInteract { get; set; }
        public bool Disarmable { get; set; }
        public bool NotReorienting { get; set; }
        public bool IsPc { get; set; }
        public bool Hologram { get; set; }
        public bool IgnoreCrePath { get; set; }

        #endregion

        public CreatureTemplate()
        {
            ResRef = string.Empty;
            Tag = string.Empty;
            FirstName = string.Empty;
            LastName = string.Empty;
            Conversation = string.Empty;
            Classes = new List<ClassInfo>();
            Feats = new List<int>();

            OnSpawn = string.Empty;
            OnHeartbeat = string.Empty;
            OnNotice = string.Empty;
            OnDialog = string.Empty;
            OnAttacked = string.Empty;
            OnDamaged = string.Empty;
            OnDeath = string.Empty;
            OnEndRound = string.Empty;
            OnBlocked = string.Empty;
            OnUserDefined = string.Empty;
        }

        public IEntity Spawn(IWorld world, Vector3 position, float facing)
        {
            if (world == null)
            {
                throw new ArgumentNullException("world");
            }

            // Create the entity
            var entity = new Entity(ObjectType.Creature, (World)world);
            entity.Tag = Tag;
            entity.TemplateResRef = ResRef;

            // Apply position and facing
            var transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                transform.Position = position;
                transform.Facing = facing;
            }

            // Apply stats
            var stats = entity.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats != null)
            {
                stats.CurrentHP = CurrentHp > 0 ? CurrentHp : MaxHp;
                stats.MaxHP = MaxHp;
            }

            // Apply faction
            var faction = entity.GetComponent<Interfaces.Components.IFactionComponent>();
            if (faction != null)
            {
                faction.FactionId = FactionId;
            }

            // Apply script hooks
            var scripts = entity.GetComponent<Interfaces.Components.IScriptHooksComponent>();
            if (scripts != null)
            {
                if (!string.IsNullOrEmpty(OnSpawn))
                    scripts.SetScript(ScriptEvent.OnSpawn, OnSpawn);
                if (!string.IsNullOrEmpty(OnHeartbeat))
                    scripts.SetScript(ScriptEvent.OnHeartbeat, OnHeartbeat);
                if (!string.IsNullOrEmpty(OnNotice))
                    scripts.SetScript(ScriptEvent.OnPerception, OnNotice);
                if (!string.IsNullOrEmpty(OnDialog))
                    scripts.SetScript(ScriptEvent.OnConversation, OnDialog);
                if (!string.IsNullOrEmpty(OnAttacked))
                    scripts.SetScript(ScriptEvent.OnAttacked, OnAttacked);
                if (!string.IsNullOrEmpty(OnDamaged))
                    scripts.SetScript(ScriptEvent.OnDamaged, OnDamaged);
                if (!string.IsNullOrEmpty(OnDeath))
                    scripts.SetScript(ScriptEvent.OnDeath, OnDeath);
                if (!string.IsNullOrEmpty(OnUserDefined))
                    scripts.SetScript(ScriptEvent.OnUserDefined, OnUserDefined);
            }

            // Register in world
            world.RegisterEntity(entity);

            return entity;
        }
    }

    /// <summary>
    /// Class information for creature templates.
    /// </summary>
    public class ClassInfo
    {
        public int ClassId { get; set; }
        public int Level { get; set; }
        public List<int> Powers { get; set; }

        public ClassInfo()
        {
            Powers = new List<int>();
        }
    }
}
