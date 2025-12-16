using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Odyssey.Core.Interfaces;

namespace Odyssey.Engines.Common
{
    /// <summary>
    /// Abstract base class for game session management across all engines.
    /// </summary>
    /// <remarks>
    /// Base Engine Game:
    /// - Based on swkotor2.exe game session management
    /// - Located via string references: "ModuleLoaded" @ 0x007bdd70, "ModuleRunning" @ 0x007bdd58, module loading functions
    /// - Module loading: FUN_006caab0 @ 0x006caab0 sets module state flags, manages module transitions
    /// - Game session: Manages current module, player entity, world state
    /// - Original implementation: Game session loads modules, manages player entity, handles module transitions
    /// - Note: This is an abstraction layer for multiple BioWare engines (Odyssey, Aurora, Eclipse)
    /// </remarks>
    public abstract class BaseEngineGame : IEngineGame
    {
        protected readonly IEngine _engine;
        protected readonly IWorld _world;
        protected string _currentModuleName;
        protected IEntity _playerEntity;

        protected BaseEngineGame(IEngine engine)
        {
            if (engine == null)
            {
                throw new ArgumentNullException(nameof(engine));
            }

            _engine = engine;
            _world = engine.World;
        }

        [CanBeNull]
        public string CurrentModuleName
        {
            get { return _currentModuleName; }
            protected set { _currentModuleName = value; }
        }

        [CanBeNull]
        public IEntity PlayerEntity
        {
            get { return _playerEntity; }
            protected set { _playerEntity = value; }
        }

        public IWorld World
        {
            get { return _world; }
        }

        public abstract Task LoadModuleAsync(string moduleName, [CanBeNull] Action<float> progressCallback = null);

        public virtual void UnloadModule()
        {
            if (_currentModuleName != null)
            {
                OnUnloadModule();
                _currentModuleName = null;
                _playerEntity = null;
            }
        }

        public virtual void Update(float deltaTime)
        {
            if (_world != null)
            {
                _world.Update(deltaTime);
            }
        }

        public virtual void Shutdown()
        {
            UnloadModule();
        }

        protected abstract void OnUnloadModule();
    }
}


