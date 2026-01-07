using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Scene graph system for hierarchical scene organization.
    /// 
    /// Scene graphs provide efficient hierarchical culling, transformation,
    /// and update propagation, essential for large complex scenes.
    /// 
    /// Features:
    /// - Hierarchical transformations
    /// - Dirty flag propagation
    /// - Efficient culling
    /// - Update batching
    /// </summary>
    /// <remarks>
    /// Scene Graph System:
    /// - Based on swkotor2.exe rendering system (scene organization)
    /// - Located via string references: Scene hierarchy used for room and object organization
    /// - Original implementation: KOTOR organizes scenes hierarchically (rooms, objects, attached items)
    /// - Hierarchical transforms: Parent-child relationships for attached objects (weapons, shields on creatures)
    /// - Culling: Efficient hierarchical culling using bounding boxes and dirty flags
    /// - Scene nodes: Contain local/world transforms, bounds, parent/child relationships
    /// - Used for: Room hierarchy, entity attachment (equipped items), efficient transform updates
    /// </remarks>
    public class SceneGraph
    {
        /// <summary>
        /// Scene node in the graph.
        /// </summary>
        public class SceneNode
        {
            /// <summary>
            /// Local transformation matrix.
            /// </summary>
            public Matrix LocalTransform;

            /// <summary>
            /// World transformation matrix (cached).
            /// </summary>
            public Matrix WorldTransform;

            /// <summary>
            /// Parent node.
            /// </summary>
            public SceneNode Parent;

            /// <summary>
            /// Child nodes.
            /// </summary>
            public List<SceneNode> Children;

            /// <summary>
            /// Bounding box in local space.
            /// </summary>
            public BoundingBox LocalBounds;

            /// <summary>
            /// Bounding box in world space (cached).
            /// </summary>
            public BoundingBox WorldBounds;

            /// <summary>
            /// Dirty flag for transformation.
            /// </summary>
            public bool TransformDirty;

            /// <summary>
            /// Dirty flag for bounds.
            /// </summary>
            public bool BoundsDirty;

            /// <summary>
            /// User data attached to node.
            /// </summary>
            public object UserData;

            public SceneNode()
            {
                LocalTransform = Matrix.Identity;
                WorldTransform = Matrix.Identity;
                Children = new List<SceneNode>();
                TransformDirty = true;
                BoundsDirty = true;
            }

            /// <summary>
            /// Adds a child node.
            /// </summary>
            public void AddChild(SceneNode child)
            {
                if (child == null)
                {
                    return;
                }

                if (child.Parent != null)
                {
                    child.Parent.RemoveChild(child);
                }

                child.Parent = this;
                Children.Add(child);
                child.MarkDirty();
            }

            /// <summary>
            /// Removes a child node.
            /// </summary>
            public void RemoveChild(SceneNode child)
            {
                if (Children.Remove(child))
                {
                    child.Parent = null;
                }
            }

            /// <summary>
            /// Marks node and children as dirty.
            /// </summary>
            public void MarkDirty()
            {
                TransformDirty = true;
                BoundsDirty = true;

                foreach (SceneNode child in Children)
                {
                    child.MarkDirty();
                }
            }

            /// <summary>
            /// Updates world transformation.
            /// </summary>
            public void UpdateTransform()
            {
                if (!TransformDirty)
                {
                    return;
                }

                if (Parent != null)
                {
                    Parent.UpdateTransform();
                    WorldTransform = LocalTransform * Parent.WorldTransform;
                }
                else
                {
                    WorldTransform = LocalTransform;
                }

                TransformDirty = false;
                BoundsDirty = true;
            }

            /// <summary>
            /// Updates world bounds.
            /// </summary>
            public void UpdateBounds()
            {
                UpdateTransform();

                if (!BoundsDirty)
                {
                    return;
                }

                // Transform local bounds to world space
                Vector3[] corners = LocalBounds.GetCorners();
                Vector3 min = new Vector3(float.MaxValue);
                Vector3 max = new Vector3(float.MinValue);

                for (int i = 0; i < corners.Length; i++)
                {
                    Vector3 worldCorner = Vector3.Transform(corners[i], WorldTransform);
                    min = Vector3.Min(min, worldCorner);
                    max = Vector3.Max(max, worldCorner);
                }

                WorldBounds = new BoundingBox(min, max);
                BoundsDirty = false;
            }
        }

        private SceneNode _root;

        /// <summary>
        /// Gets the root node.
        /// </summary>
        public SceneNode Root
        {
            get { return _root; }
        }

        /// <summary>
        /// Initializes a new scene graph.
        /// </summary>
        public SceneGraph()
        {
            _root = new SceneNode();
        }

        /// <summary>
        /// Updates all nodes in the graph.
        /// </summary>
        public void Update()
        {
            UpdateNode(_root);
        }

        /// <summary>
        /// Culls nodes outside the frustum.
        /// </summary>
        public void Cull(Culling.Frustum frustum, List<SceneNode> visibleNodes)
        {
            if (visibleNodes == null)
            {
                throw new ArgumentNullException("visibleNodes");
            }

            CullNode(_root, frustum, visibleNodes);
        }

        private void UpdateNode(SceneNode node)
        {
            node.UpdateBounds();

            foreach (SceneNode child in node.Children)
            {
                UpdateNode(child);
            }
        }

        private void CullNode(SceneNode node, Culling.Frustum frustum, List<SceneNode> visibleNodes)
        {
            node.UpdateBounds();

            // Test node bounds against frustum
            System.Numerics.Vector3 min = new System.Numerics.Vector3(
                node.WorldBounds.Min.X,
                node.WorldBounds.Min.Y,
                node.WorldBounds.Min.Z
            );
            System.Numerics.Vector3 max = new System.Numerics.Vector3(
                node.WorldBounds.Max.X,
                node.WorldBounds.Max.Y,
                node.WorldBounds.Max.Z
            );

            if (frustum.AabbInFrustum(min, max))
            {
                visibleNodes.Add(node);

                // Cull children
                foreach (SceneNode child in node.Children)
                {
                    CullNode(child, frustum, visibleNodes);
                }
            }
        }
    }

    /// <summary>
    /// Bounding box structure.
    /// </summary>
    public struct BoundingBox
    {
        public Vector3 Min;
        public Vector3 Max;

        public BoundingBox(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }

        public Vector3[] GetCorners()
        {
            return new Vector3[]
            {
                new Vector3(Min.X, Min.Y, Min.Z),
                new Vector3(Max.X, Min.Y, Min.Z),
                new Vector3(Min.X, Max.Y, Min.Z),
                new Vector3(Max.X, Max.Y, Min.Z),
                new Vector3(Min.X, Min.Y, Max.Z),
                new Vector3(Max.X, Min.Y, Max.Z),
                new Vector3(Min.X, Max.Y, Max.Z),
                new Vector3(Max.X, Max.Y, Max.Z)
            };
        }
    }
}

