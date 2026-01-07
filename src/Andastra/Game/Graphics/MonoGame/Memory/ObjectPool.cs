using System;
using System.Collections.Generic;

namespace Andastra.Runtime.MonoGame.Memory
{
    /// <summary>
    /// Generic object pool for reducing allocations and GC pressure.
    /// 
    /// Object pooling reuses objects instead of allocating new ones,
    /// reducing garbage collection overhead in game loops.
    /// 
    /// Based on modern AAA game practices for memory management.
    /// </summary>
    /// <remarks>
    /// Object Pool:
    /// - Based on swkotor2.exe memory management system
    /// - Located via string references: "SpawnPoolActive" @ 0x007c0bdc (spawn pool active flag)
    /// - "PT_XP_POOL" @ 0x007c18a4 (XP pool in PARTYTABLE)
    /// - Memory allocation: wglAllocateMemoryNV, wglFreeMemoryNV (OpenGL memory management)
    /// - "GlobalMemoryStatus" @ 0x0080afa4 (Windows memory status API)
    /// - Original implementation: KOTOR uses object pooling for entities, effects, and render objects
    /// - Pool management: Objects are reused to reduce allocation overhead and GC pressure
    /// - Spawn pools: Active spawn pools track available objects for reuse
    /// </remarks>
    /// <typeparam name="T">Type of object to pool.</typeparam>
    public class ObjectPool<T> where T : class
    {
        private readonly Stack<T> _pool;
        private readonly Func<T> _factory;
        private readonly Action<T> _reset;
        private readonly int _maxSize;

        /// <summary>
        /// Gets the number of objects currently in the pool.
        /// </summary>
        public int PoolSize
        {
            get { return _pool.Count; }
        }

        /// <summary>
        /// Initializes a new object pool.
        /// </summary>
        /// <param name="factory">Factory function to create new objects.</param>
        /// <param name="reset">Optional function to reset objects when returned to pool.</param>
        /// <param name="maxSize">Maximum pool size (0 = unlimited).</param>
        /// <param name="initialSize">Initial number of objects to pre-allocate.</param>
        public ObjectPool(Func<T> factory, Action<T> reset = null, int maxSize = 0, int initialSize = 0)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            _factory = factory;
            _reset = reset;
            _maxSize = maxSize;
            _pool = new Stack<T>();

            // Pre-allocate initial objects
            for (int i = 0; i < initialSize; i++)
            {
                _pool.Push(_factory());
            }
        }

        /// <summary>
        /// Gets an object from the pool, or creates a new one if pool is empty.
        /// </summary>
        public T Get()
        {
            if (_pool.Count > 0)
            {
                return _pool.Pop();
            }
            return _factory();
        }

        /// <summary>
        /// Returns an object to the pool.
        /// </summary>
        /// <param name="obj">Object to return to the pool. Can be null (no-op).</param>
        public void Return(T obj)
        {
            if (obj == null)
            {
                return;
            }

            // Reset object if reset function provided
            _reset?.Invoke(obj);

            // Return to pool if not at max size
            if (_maxSize == 0 || _pool.Count < _maxSize)
            {
                _pool.Push(obj);
            }
        }

        /// <summary>
        /// Clears the pool, disposing of all pooled objects.
        /// </summary>
        public void Clear()
        {
            while (_pool.Count > 0)
            {
                T obj = _pool.Pop();
                IDisposable disposable = obj as IDisposable;
                if (disposable != null)
                {
                    disposable.Dispose();
                }
            }
        }
    }
}

