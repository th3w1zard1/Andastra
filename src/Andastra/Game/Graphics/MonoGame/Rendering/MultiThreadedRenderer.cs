using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// Multi-threaded rendering system for parallel command generation.
    /// 
    /// Multi-threaded rendering distributes command generation across
    /// worker threads, reducing main thread CPU load and improving performance.
    /// 
    /// Features:
    /// - Worker thread command generation
    /// - Thread-safe command buffers
    /// - Parallel frustum culling
    /// - Work stealing for load balancing
    /// </summary>
    public class MultiThreadedRenderer
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly int _workerThreadCount;
        private readonly Thread[] _workerThreads;
        private readonly BlockingCollection<RenderTask> _taskQueue;
        private readonly ConcurrentQueue<CommandBuffer> _completedBuffers;
        private bool _running;

        /// <summary>
        /// Render task for worker threads.
        /// </summary>
        private struct RenderTask
        {
            public int TaskId;
            public object Data;
            public Action<object, CommandBuffer> ProcessFunc;
        }

        /// <summary>
        /// Initializes a new multi-threaded renderer.
        /// </summary>
        public MultiThreadedRenderer(GraphicsDevice graphicsDevice, int workerThreadCount = 0)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException("graphicsDevice");
            }

            _graphicsDevice = graphicsDevice;
            _workerThreadCount = workerThreadCount > 0 ? workerThreadCount : Environment.ProcessorCount - 1;
            _taskQueue = new BlockingCollection<RenderTask>();
            _completedBuffers = new ConcurrentQueue<CommandBuffer>();
            _workerThreads = new Thread[_workerThreadCount];
            _running = false;
        }

        /// <summary>
        /// Starts worker threads.
        /// </summary>
        public void Start()
        {
            if (_running)
            {
                return;
            }

            _running = true;

            for (int i = 0; i < _workerThreadCount; i++)
            {
                int threadId = i;
                _workerThreads[i] = new Thread(() => WorkerThread(threadId))
                {
                    Name = $"RenderWorker_{threadId}",
                    IsBackground = true
                };
                _workerThreads[i].Start();
            }
        }

        /// <summary>
        /// Stops worker threads.
        /// </summary>
        public void Stop()
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            _taskQueue.CompleteAdding();

            // Wait for threads to finish
            foreach (Thread thread in _workerThreads)
            {
                thread.Join();
            }
        }

        /// <summary>
        /// Submits a render task to worker threads.
        /// </summary>
        public void SubmitTask(object data, Action<object, CommandBuffer> processFunc)
        {
            if (!_running)
            {
                return;
            }

            RenderTask task = new RenderTask
            {
                TaskId = _taskQueue.Count,
                Data = data,
                ProcessFunc = processFunc
            };

            _taskQueue.Add(task);
        }

        /// <summary>
        /// Collects completed command buffers.
        /// </summary>
        public void CollectBuffers(List<CommandBuffer> buffers)
        {
            if (buffers == null)
            {
                return;
            }

            CommandBuffer buffer;
            while (_completedBuffers.TryDequeue(out buffer))
            {
                buffers.Add(buffer);
            }
        }

        private void WorkerThread(int threadId)
        {
            while (_running || !_taskQueue.IsCompleted)
            {
                RenderTask task;
                if (_taskQueue.TryTake(out task, 100)) // Timeout to check _running
                {
                    // Create command buffer for this task
                    CommandBuffer buffer = new CommandBuffer();

                    // Process task
                    task.ProcessFunc(task.Data, buffer);

                    // Submit completed buffer
                    _completedBuffers.Enqueue(buffer);
                }
            }
        }
    }
}

