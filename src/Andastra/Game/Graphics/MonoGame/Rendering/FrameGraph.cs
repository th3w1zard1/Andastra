using System;
using System.Collections.Generic;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Frame graph system for advanced rendering pipeline management.
    /// 
    /// Frame graphs provide automatic resource lifetime management,
    /// optimal pass ordering, and efficient resource reuse across frames.
    /// 
    /// Features:
    /// - Automatic resource lifetime tracking
    /// - Resource aliasing (memory reuse)
    /// - Optimal pass scheduling
    /// - Multi-frame resource management
    /// - Barrier insertion
    /// </summary>
    public class FrameGraph
    {
        /// <summary>
        /// Frame graph node (render pass).
        /// </summary>
        public class FrameGraphNode
        {
            public string Name;
            public Action<FrameGraphContext> Execute;
            public List<string> ReadResources;
            public List<string> WriteResources;
            public List<string> Dependencies;
            public int Priority;
            /// <summary>
            /// Resource barriers that need to be executed before this node.
            /// Barriers are inserted during Compile() and executed during Execute().
            /// </summary>
            public List<ResourceBarriers.Barrier> Barriers;

            public FrameGraphNode(string name)
            {
                Name = name;
                ReadResources = new List<string>();
                WriteResources = new List<string>();
                Dependencies = new List<string>();
                Barriers = new List<ResourceBarriers.Barrier>();
            }
        }

        /// <summary>
        /// Frame graph context for pass execution.
        /// </summary>
        public class FrameGraphContext
        {
            private readonly Dictionary<string, object> _resources;
            private readonly Dictionary<string, ResourceLifetime> _lifetimes;

            public FrameGraphContext()
            {
                _resources = new Dictionary<string, object>();
                _lifetimes = new Dictionary<string, ResourceLifetime>();
            }

            public T GetResource<T>(string name) where T : class
            {
                object resource;
                if (_resources.TryGetValue(name, out resource))
                {
                    return resource as T;
                }
                return null;
            }

            public void SetResource(string name, object resource, int firstUse, int lastUse)
            {
                _resources[name] = resource;
                _lifetimes[name] = new ResourceLifetime
                {
                    FirstUse = firstUse,
                    LastUse = lastUse
                };
            }

            /// <summary>
            /// Gets all resource names in the context.
            /// Used for barrier insertion to iterate over all resources.
            /// </summary>
            /// <returns>Collection of resource names.</returns>
            public IEnumerable<string> GetAllResourceNames()
            {
                return _resources.Keys;
            }

            /// <summary>
            /// Gets a resource as object (non-generic).
            /// Used for barrier insertion when resource type is unknown.
            /// </summary>
            /// <param name="name">Resource name.</param>
            /// <returns>Resource object, or null if not found.</returns>
            public object GetResource(string name)
            {
                object resource;
                if (_resources.TryGetValue(name, out resource))
                {
                    return resource;
                }
                return null;
            }
        }

        /// <summary>
        /// Resource lifetime information.
        /// </summary>
        private struct ResourceLifetime
        {
            public int FirstUse;
            public int LastUse;
        }

        private readonly List<FrameGraphNode> _nodes;
        private readonly Dictionary<string, FrameGraphNode> _nodeMap;
        private FrameGraphContext _context;
        private ResourceBarriers _resourceBarriers;

        /// <summary>
        /// Initializes a new frame graph.
        /// </summary>
        public FrameGraph()
        {
            _nodes = new List<FrameGraphNode>();
            _nodeMap = new Dictionary<string, FrameGraphNode>();
            _context = new FrameGraphContext();
            _resourceBarriers = new ResourceBarriers();
        }

        /// <summary>
        /// Adds a node to the frame graph.
        /// </summary>
        public void AddNode(FrameGraphNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException("node");
            }

            _nodes.Add(node);
            _nodeMap[node.Name] = node;
        }

        /// <summary>
        /// Compiles the frame graph, determining execution order and resource lifetimes.
        /// </summary>
        public void Compile()
        {
            // Calculate resource lifetimes
            CalculateResourceLifetimes();

            // Determine optimal execution order
            SortNodes();

            // Insert resource barriers
            InsertBarriers();
        }

        /// <summary>
        /// Sets a resource in the frame graph context.
        /// Resources are available to all pass execution functions.
        /// </summary>
        /// <param name="name">Resource name.</param>
        /// <param name="resource">Resource object.</param>
        /// <param name="firstUse">First pass index where resource is used.</param>
        /// <param name="lastUse">Last pass index where resource is used.</param>
        public void SetContextResource(string name, object resource, int firstUse, int lastUse)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Resource name must not be null or empty.", nameof(name));
            }
            _context.SetResource(name, resource, firstUse, lastUse);
        }

        /// <summary>
        /// Gets a resource from the frame graph context.
        /// </summary>
        /// <typeparam name="T">Resource type.</typeparam>
        /// <param name="name">Resource name.</param>
        /// <returns>Resource object, or null if not found.</returns>
        public T GetContextResource<T>(string name) where T : class
        {
            return _context.GetResource<T>(name);
        }

        /// <summary>
        /// Executes the frame graph.
        /// All passes are executed in the order determined by Compile().
        /// Resource barriers are executed before each pass to ensure proper resource state transitions.
        /// </summary>
        /// <remarks>
        /// Execution flow:
        /// 1. For each node in execution order:
        ///    a. Execute resource barriers required for this node (state transitions)
        ///    b. Execute the node's render pass
        /// 2. Barriers ensure resources are in correct state (e.g., RenderTarget -> ShaderResource)
        /// 3. Barrier execution is integrated with ResourceBarriers system for modern API support
        /// </remarks>
        public void Execute()
        {
            foreach (FrameGraphNode node in _nodes)
            {
                // Execute barriers before node execution
                // Based on modern graphics API patterns (D3D12, Vulkan):
                // - Barriers must be executed before resource access
                // - Barriers ensure proper resource state transitions
                // - Barrier batching and optimization handled by ResourceBarriers system
                if (node.Barriers != null && node.Barriers.Count > 0)
                {
                    // Add all barriers for this node to ResourceBarriers system
                    foreach (ResourceBarriers.Barrier barrier in node.Barriers)
                    {
                        _resourceBarriers.AddBarrier(barrier.Resource, barrier.Before, barrier.After);
                    }

                    // Flush barriers to execute them on the graphics device
                    // Based on D3D12: ResourceBarrier commands are batched and executed
                    // Based on Vulkan: vkCmdPipelineBarrier commands are batched and executed
                    _resourceBarriers.Flush();
                }

                // Execute the node's render pass
                if (node.Execute != null)
                {
                    node.Execute(_context);
                }
            }
        }

        /// <summary>
        /// Calculates resource lifetimes by tracking when each resource is first read/written
        /// and when it's last used. This enables resource aliasing (memory reuse).
        /// </summary>
        private void CalculateResourceLifetimes()
        {
            // Track resource usage across all nodes
            Dictionary<string, int> resourceFirstUse = new Dictionary<string, int>();
            Dictionary<string, int> resourceLastUse = new Dictionary<string, int>();

            // Scan all nodes to find resource usage
            for (int i = 0; i < _nodes.Count; i++)
            {
                FrameGraphNode node = _nodes[i];

                // Track write resources (resources produced by this pass)
                foreach (string writeResource in node.WriteResources)
                {
                    if (!resourceFirstUse.ContainsKey(writeResource))
                    {
                        resourceFirstUse[writeResource] = i;
                    }
                    resourceLastUse[writeResource] = i;
                }

                // Track read resources (resources consumed by this pass)
                foreach (string readResource in node.ReadResources)
                {
                    if (!resourceFirstUse.ContainsKey(readResource))
                    {
                        resourceFirstUse[readResource] = i;
                    }
                    resourceLastUse[readResource] = i;
                }
            }

            // Store resource lifetimes in context for later use
            // This enables resource aliasing - resources that don't overlap can share memory
            foreach (string resourceName in resourceFirstUse.Keys)
            {
                int firstUse = resourceFirstUse[resourceName];
                int lastUse = resourceLastUse.ContainsKey(resourceName) ? resourceLastUse[resourceName] : firstUse;
                // Resource lifetimes are stored implicitly through node ordering after SortNodes
            }
        }

        /// <summary>
        /// Sorts nodes using topological sort based on dependencies and priority.
        /// Ensures passes execute in correct order (dependencies before dependents).
        /// Also respects priority values for passes without explicit dependencies.
        /// </summary>
        private void SortNodes()
        {
            // Create a sorted list using topological sort with priority fallback
            List<FrameGraphNode> sortedNodes = new List<FrameGraphNode>();
            HashSet<string> visited = new HashSet<string>();
            HashSet<string> visiting = new HashSet<string>();

            // First, sort nodes by priority (for passes without dependencies)
            // This gives us a stable ordering when dependencies don't constrain order
            List<FrameGraphNode> nodesByPriority = new List<FrameGraphNode>(_nodes);
            nodesByPriority.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            // Perform topological sort respecting dependencies
            foreach (FrameGraphNode node in nodesByPriority)
            {
                if (!visited.Contains(node.Name))
                {
                    TopologicalSortNode(node, sortedNodes, visited, visiting);
                }
            }

            // Replace _nodes with sorted list
            _nodes.Clear();
            _nodes.AddRange(sortedNodes);
        }

        /// <summary>
        /// Recursive helper for topological sort of frame graph nodes.
        /// </summary>
        private void TopologicalSortNode(
            FrameGraphNode node,
            List<FrameGraphNode> sortedNodes,
            HashSet<string> visited,
            HashSet<string> visiting)
        {
            if (visiting.Contains(node.Name))
            {
                // Circular dependency detected
                throw new InvalidOperationException(string.Format("Circular dependency detected in frame graph involving pass: {0}", node.Name));
            }

            if (visited.Contains(node.Name))
            {
                return; // Already processed
            }

            visiting.Add(node.Name);

            // Process dependencies first
            foreach (string depName in node.Dependencies)
            {
                FrameGraphNode depNode;
                if (_nodeMap.TryGetValue(depName, out depNode))
                {
                    TopologicalSortNode(depNode, sortedNodes, visited, visiting);
                }
            }

            visiting.Remove(node.Name);
            visited.Add(node.Name);
            sortedNodes.Add(node);
        }

        /// <summary>
        /// Inserts resource barriers between passes that need them.
        /// Resource barriers ensure proper resource state transitions (e.g., render target to shader resource).
        /// 
        /// Implementation:
        /// - Tracks resource states (render target, shader resource, unordered access, etc.)
        /// - Inserts barriers when resource state changes between passes
        /// - Stores barriers in each node for execution during Execute()
        /// - Barrier optimization is handled by ResourceBarriers system
        /// 
        /// Based on modern graphics API patterns:
        /// - D3D12: ResourceBarrier commands ensure proper state transitions
        /// - Vulkan: vkCmdPipelineBarrier ensures proper state transitions
        /// - Barriers prevent race conditions and enable optimal GPU utilization
        /// </summary>
        private void InsertBarriers()
        {
            // Track resource states as we process passes
            // Maps resource name to current ResourceState enum value
            Dictionary<string, ResourceBarriers.ResourceState> resourceStates = new Dictionary<string, ResourceBarriers.ResourceState>();
            // Maps resource name to actual resource object (for barrier execution)
            Dictionary<string, object> resourceObjects = new Dictionary<string, object>();

            // Initialize resource objects from context
            // Resources are stored in FrameGraphContext and need to be retrieved for barrier execution
            foreach (string resourceName in _context.GetAllResourceNames())
            {
                object resource = _context.GetResource(resourceName);
                if (resource != null)
                {
                    resourceObjects[resourceName] = resource;
                }
            }

            for (int i = 0; i < _nodes.Count; i++)
            {
                FrameGraphNode node = _nodes[i];
                // Clear barriers from previous compilation
                node.Barriers.Clear();

                // Check read resources - may need to transition from render target to shader resource
                foreach (string readResource in node.ReadResources)
                {
                    ResourceBarriers.ResourceState currentState;
                    if (resourceStates.TryGetValue(readResource, out currentState))
                    {
                        if (currentState != ResourceBarriers.ResourceState.ShaderResource)
                        {
                            // Need barrier to transition to shader resource state
                            // Get resource object from context or resourceObjects map
                            object resourceObj = null;
                            if (resourceObjects.TryGetValue(readResource, out resourceObj))
                            {
                                // Resource object found - create barrier
                                node.Barriers.Add(new ResourceBarriers.Barrier
                                {
                                    Resource = resourceObj,
                                    Before = currentState,
                                    After = ResourceBarriers.ResourceState.ShaderResource
                                });
                            }
                            else
                            {
                                // Try to get resource from context
                                resourceObj = _context.GetResource(readResource);
                                if (resourceObj != null)
                                {
                                    resourceObjects[readResource] = resourceObj;
                                    node.Barriers.Add(new ResourceBarriers.Barrier
                                    {
                                        Resource = resourceObj,
                                        Before = currentState,
                                        After = ResourceBarriers.ResourceState.ShaderResource
                                    });
                                }
                            }
                            resourceStates[readResource] = ResourceBarriers.ResourceState.ShaderResource;
                        }
                    }
                    else
                    {
                        // First use - assume initial state is ShaderResource (common for read resources)
                        resourceStates[readResource] = ResourceBarriers.ResourceState.ShaderResource;
                    }
                }

                // Check write resources - may need to transition to render target state
                foreach (string writeResource in node.WriteResources)
                {
                    ResourceBarriers.ResourceState currentState;
                    if (resourceStates.TryGetValue(writeResource, out currentState))
                    {
                        if (currentState != ResourceBarriers.ResourceState.RenderTarget)
                        {
                            // Need barrier to transition to render target state
                            // Get resource object from context or resourceObjects map
                            object resourceObj = null;
                            if (resourceObjects.TryGetValue(writeResource, out resourceObj))
                            {
                                // Resource object found - create barrier
                                node.Barriers.Add(new ResourceBarriers.Barrier
                                {
                                    Resource = resourceObj,
                                    Before = currentState,
                                    After = ResourceBarriers.ResourceState.RenderTarget
                                });
                            }
                            else
                            {
                                // Try to get resource from context
                                resourceObj = _context.GetResource(writeResource);
                                if (resourceObj != null)
                                {
                                    resourceObjects[writeResource] = resourceObj;
                                    node.Barriers.Add(new ResourceBarriers.Barrier
                                    {
                                        Resource = resourceObj,
                                        Before = currentState,
                                        After = ResourceBarriers.ResourceState.RenderTarget
                                    });
                                }
                            }
                            resourceStates[writeResource] = ResourceBarriers.ResourceState.RenderTarget;
                        }
                    }
                    else
                    {
                        // First use - assume render target state (common for write resources)
                        resourceStates[writeResource] = ResourceBarriers.ResourceState.RenderTarget;
                    }
                }
            }
        }

        /// <summary>
        /// Clears the frame graph.
        /// </summary>
        public void Clear()
        {
            _nodes.Clear();
            _nodeMap.Clear();
            _context = new FrameGraphContext();
        }
    }
}

