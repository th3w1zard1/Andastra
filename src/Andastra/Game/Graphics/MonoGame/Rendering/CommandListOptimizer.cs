using System;
using System.Collections.Generic;
using System.Reflection;
using Andastra.Runtime.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XnaPrimitiveType = Microsoft.Xna.Framework.Graphics.PrimitiveType;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Command list optimizer for reducing draw call overhead.
    ///
    /// Optimizes command lists by merging compatible commands,
    /// reducing CPU overhead and improving GPU utilization.
    ///
    /// Features:
    /// - Command merging
    /// - Redundant call elimination
    /// - State change minimization
    /// - Draw call batching
    ///
    /// Based on industry-standard command buffer optimization techniques:
    /// - Draw call batching by combining compatible draws
    /// - State sorting to minimize state changes
    /// - Geometry merging for consecutive draws with same state
    /// - Instancing conversion when appropriate
    /// </summary>
    public class CommandListOptimizer
    {
        /// <summary>
        /// Draw command data for indexed draw calls.
        /// Contains information needed to execute a DrawIndexedPrimitives call.
        /// </summary>
        private struct DrawIndexedCommandData
        {
            public XnaPrimitiveType PrimitiveType;
            public int BaseVertex;
            public int MinVertexIndex;
            public int NumVertices;
            public int StartIndex;
            public int PrimitiveCount;
            public object VertexBuffer; // VertexBuffer or equivalent
            public object IndexBuffer;  // IndexBuffer or equivalent
            public Matrix? WorldMatrix; // Optional world transform matrix for instancing conversion

            public DrawIndexedCommandData(XnaPrimitiveType primitiveType, int baseVertex, int minVertexIndex, int numVertices, int startIndex, int primitiveCount, object vertexBuffer, object indexBuffer)
            {
                PrimitiveType = primitiveType;
                BaseVertex = baseVertex;
                MinVertexIndex = minVertexIndex;
                NumVertices = numVertices;
                StartIndex = startIndex;
                PrimitiveCount = primitiveCount;
                VertexBuffer = vertexBuffer;
                IndexBuffer = indexBuffer;
                WorldMatrix = null;
            }

            public DrawIndexedCommandData(XnaPrimitiveType primitiveType, int baseVertex, int minVertexIndex, int numVertices, int startIndex, int primitiveCount, object vertexBuffer, object indexBuffer, Matrix? worldMatrix)
            {
                PrimitiveType = primitiveType;
                BaseVertex = baseVertex;
                MinVertexIndex = minVertexIndex;
                NumVertices = numVertices;
                StartIndex = startIndex;
                PrimitiveCount = primitiveCount;
                VertexBuffer = vertexBuffer;
                IndexBuffer = indexBuffer;
                WorldMatrix = worldMatrix;
            }
        }

        /// <summary>
        /// Draw command data for non-indexed draw calls.
        /// Contains information needed to execute a DrawPrimitives call.
        /// </summary>
        private struct DrawCommandData
        {
            public XnaPrimitiveType PrimitiveType;
            public int VertexStart;
            public int PrimitiveCount;
            public object VertexBuffer; // VertexBuffer or equivalent

            public DrawCommandData(XnaPrimitiveType primitiveType, int vertexStart, int primitiveCount, object vertexBuffer)
            {
                PrimitiveType = primitiveType;
                VertexStart = vertexStart;
                PrimitiveCount = primitiveCount;
                VertexBuffer = vertexBuffer;
            }
        }

        /// <summary>
        /// Instanced draw command data.
        /// Contains information needed to execute a DrawInstancedPrimitives call.
        /// </summary>
        private struct DrawInstancedCommandData
        {
            public XnaPrimitiveType PrimitiveType;
            public int BaseVertex;
            public int MinVertexIndex;
            public int NumVertices;
            public int StartIndex;
            public int PrimitiveCountPerInstance;
            public int InstanceCount;
            public int StartInstanceLocation;
            public object VertexBuffer; // VertexBuffer or equivalent
            public object IndexBuffer;  // IndexBuffer or equivalent

            public DrawInstancedCommandData(XnaPrimitiveType primitiveType, int baseVertex, int minVertexIndex, int numVertices, int startIndex, int primitiveCountPerInstance, int instanceCount, int startInstanceLocation, object vertexBuffer, object indexBuffer)
            {
                PrimitiveType = primitiveType;
                BaseVertex = baseVertex;
                MinVertexIndex = minVertexIndex;
                NumVertices = numVertices;
                StartIndex = startIndex;
                PrimitiveCountPerInstance = primitiveCountPerInstance;
                InstanceCount = instanceCount;
                StartInstanceLocation = startInstanceLocation;
                VertexBuffer = vertexBuffer;
                IndexBuffer = indexBuffer;
            }
        }
        /// <summary>
        /// Optimizes a command buffer by merging and reordering commands.
        /// </summary>
        /// <param name="buffer">Command buffer to optimize. Can be null (no-op).</param>
        public void Optimize(CommandBuffer buffer)
        {
            if (buffer == null)
            {
                return;
            }

            // Get commands
            var commands = new List<CommandBuffer.RenderCommand>(buffer.GetCommands());

            // Sort by state to minimize changes
            commands.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));

            // Merge compatible commands
            MergeCommands(commands);

            // Clear and rebuild buffer
            buffer.Clear();
            foreach (CommandBuffer.RenderCommand cmd in commands)
            {
                buffer.AddCommand(cmd.Type, cmd.Data, cmd.SortKey);
            }
        }

        /// <summary>
        /// Merges compatible commands to reduce draw calls.
        ///
        /// This optimization reduces CPU overhead by combining multiple draw calls
        /// into a single call when they share the same render state and buffers.
        ///
        /// Merging strategies:
        /// 1. For indexed draws with same buffers: Combine into single draw with adjusted ranges
        /// 2. For non-indexed draws with same buffers: Combine into single draw with adjusted ranges
        /// 3. For compatible draws: Convert to instanced rendering when appropriate
        /// </summary>
        private void MergeCommands(List<CommandBuffer.RenderCommand> commands)
        {
            if (commands == null || commands.Count < 2)
            {
                return;
            }

            // Merge consecutive draw calls with same state
            int i = 0;
            while (i < commands.Count - 1)
            {
                CommandBuffer.RenderCommand current = commands[i];
                CommandBuffer.RenderCommand next = commands[i + 1];

                // Check if commands can be merged
                if (CanMerge(current, next))
                {
                    // Attempt to merge the commands
                    if (TryMergeDrawCommands(commands, i, i + 1))
                    {
                        // Successfully merged - remove the second command
                        commands.RemoveAt(i + 1);
                        // Don't increment i - check current command again as it may be mergeable with next
                    }
                    else
                    {
                        // Could not merge - move to next command
                        i++;
                    }
                }
                else
                {
                    // Cannot merge - move to next command
                    i++;
                }
            }
        }

        /// <summary>
        /// Attempts to merge two draw commands.
        ///
        /// Returns true if the commands were successfully merged into the first command,
        /// false if merging is not possible.
        /// </summary>
        private bool TryMergeDrawCommands(List<CommandBuffer.RenderCommand> commands, int firstIndex, int secondIndex)
        {
            CommandBuffer.RenderCommand first = commands[firstIndex];
            CommandBuffer.RenderCommand second = commands[secondIndex];

            // Handle indexed draw commands
            if (first.Type == CommandBuffer.CommandType.DrawIndexed && second.Type == CommandBuffer.CommandType.DrawIndexed)
            {
                return TryMergeDrawIndexedCommands(commands, firstIndex, secondIndex);
            }

            // Handle non-indexed draw commands
            if (first.Type == CommandBuffer.CommandType.Draw && second.Type == CommandBuffer.CommandType.Draw)
            {
                return TryMergeDrawCommandsNonIndexed(commands, firstIndex, secondIndex);
            }

            // Handle instanced draw commands
            if (first.Type == CommandBuffer.CommandType.DrawInstanced && second.Type == CommandBuffer.CommandType.DrawInstanced)
            {
                return TryMergeDrawInstancedCommands(commands, firstIndex, secondIndex);
            }

            // Cannot merge different command types
            return false;
        }

        /// <summary>
        /// Attempts to merge two indexed draw commands.
        ///
        /// Two indexed draws can be merged if they:
        /// 1. Use the same primitive type
        /// 2. Use the same vertex and index buffers
        /// 3. Have compatible ranges (can be combined into a single draw)
        /// </summary>
        private bool TryMergeDrawIndexedCommands(List<CommandBuffer.RenderCommand> commands, int firstIndex, int secondIndex)
        {
            CommandBuffer.RenderCommand first = commands[firstIndex];
            CommandBuffer.RenderCommand second = commands[secondIndex];

            // Extract draw command data - must be in expected format
            DrawIndexedCommandData? firstDataNullable = ExtractDrawIndexedData(first.Data);
            DrawIndexedCommandData? secondDataNullable = ExtractDrawIndexedData(second.Data);

            if (!firstDataNullable.HasValue || !secondDataNullable.HasValue)
            {
                return false; // Cannot extract data - data format not recognized, cannot merge
            }

            DrawIndexedCommandData firstData = firstDataNullable.Value;
            DrawIndexedCommandData secondData = secondDataNullable.Value;

            // Check if we successfully extracted valid data (non-null buffers indicate valid extraction)
            if (firstData.VertexBuffer == null || firstData.IndexBuffer == null ||
                secondData.VertexBuffer == null || secondData.IndexBuffer == null)
            {
                return false; // Invalid or unrecognized data format - cannot merge
            }

            // Check if commands use the same buffers and primitive type
            if (!AreBuffersEqual(firstData.VertexBuffer, secondData.VertexBuffer) ||
                !AreBuffersEqual(firstData.IndexBuffer, secondData.IndexBuffer) ||
                firstData.PrimitiveType != secondData.PrimitiveType)
            {
                return false; // Cannot merge - different buffers or primitive type
            }

            // Check if the draws are consecutive in the buffer (can be merged into single draw)
            // For indexed draws, we can merge if the second draw starts where the first ends
            int firstEndIndex = firstData.StartIndex + (firstData.PrimitiveCount * GetIndicesPerPrimitive(firstData.PrimitiveType));
            if (secondData.StartIndex == firstEndIndex && firstData.BaseVertex == secondData.BaseVertex)
            {
                // Merge: Combine into single draw with expanded range
                DrawIndexedCommandData mergedData = new DrawIndexedCommandData(
                    firstData.PrimitiveType,
                    firstData.BaseVertex,
                    Math.Min(firstData.MinVertexIndex, secondData.MinVertexIndex), // Use minimum vertex index
                    Math.Max(firstData.NumVertices, secondData.NumVertices), // Use maximum vertex count
                    firstData.StartIndex, // Start at first draw's start index
                    firstData.PrimitiveCount + secondData.PrimitiveCount, // Combined primitive count
                    firstData.VertexBuffer,
                    firstData.IndexBuffer);

                // Update the first command with merged data
                CommandBuffer.RenderCommand mergedCommand = new CommandBuffer.RenderCommand
                {
                    Type = first.Type,
                    Data = mergedData,
                    SortKey = first.SortKey
                };
                commands[firstIndex] = mergedCommand;
                return true;
            }

            // If not consecutive, check if we can convert to instanced rendering
            // Instancing conversion: Same geometry with different transforms can be converted to instanced rendering
            // This optimization reduces draw calls when the same mesh is rendered multiple times with different transforms
            if (TryConvertToInstancedRendering(commands, firstIndex, secondIndex, firstData, secondData))
            {
                return true;
            }

            // Cannot merge - different geometry or transforms not available
            return false;
        }

        /// <summary>
        /// Attempts to merge two non-indexed draw commands.
        ///
        /// Two non-indexed draws can be merged if they:
        /// 1. Use the same primitive type
        /// 2. Use the same vertex buffer
        /// 3. Have consecutive ranges (can be combined into a single draw)
        /// </summary>
        private bool TryMergeDrawCommandsNonIndexed(List<CommandBuffer.RenderCommand> commands, int firstIndex, int secondIndex)
        {
            CommandBuffer.RenderCommand first = commands[firstIndex];
            CommandBuffer.RenderCommand second = commands[secondIndex];

            // Extract draw command data - must be in expected format
            DrawCommandData? firstDataNullable = ExtractDrawData(first.Data);
            DrawCommandData? secondDataNullable = ExtractDrawData(second.Data);

            if (!firstDataNullable.HasValue || !secondDataNullable.HasValue)
            {
                return false; // Cannot extract data - data format not recognized, cannot merge
            }

            DrawCommandData firstData = firstDataNullable.Value;
            DrawCommandData secondData = secondDataNullable.Value;

            // Check if we successfully extracted valid data (non-null buffer indicates valid extraction)
            if (firstData.VertexBuffer == null || secondData.VertexBuffer == null)
            {
                return false; // Invalid or unrecognized data format - cannot merge
            }

            // Check if commands use the same buffer and primitive type
            if (!AreBuffersEqual(firstData.VertexBuffer, secondData.VertexBuffer) ||
                firstData.PrimitiveType != secondData.PrimitiveType)
            {
                return false; // Cannot merge - different buffers or primitive type
            }

            // Check if the draws are consecutive in the buffer (can be merged into single draw)
            int verticesPerPrimitive = GetVerticesPerPrimitive(firstData.PrimitiveType);
            int firstEndVertex = firstData.VertexStart + (firstData.PrimitiveCount * verticesPerPrimitive);
            if (secondData.VertexStart == firstEndVertex)
            {
                // Merge: Combine into single draw with expanded range
                DrawCommandData mergedData = new DrawCommandData(
                    firstData.PrimitiveType,
                    firstData.VertexStart, // Start at first draw's start vertex
                    firstData.PrimitiveCount + secondData.PrimitiveCount, // Combined primitive count
                    firstData.VertexBuffer);

                // Update the first command with merged data
                CommandBuffer.RenderCommand mergedCommand = new CommandBuffer.RenderCommand
                {
                    Type = first.Type,
                    Data = mergedData,
                    SortKey = first.SortKey
                };
                commands[firstIndex] = mergedCommand;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to merge two instanced draw commands.
        ///
        /// Two instanced draws can be merged if they:
        /// 1. Use the same primitive type
        /// 2. Use the same vertex and index buffers
        /// 3. Have the same geometry (same base vertex, start index, primitive count per instance)
        /// 4. Can combine instance counts
        /// </summary>
        private bool TryMergeDrawInstancedCommands(List<CommandBuffer.RenderCommand> commands, int firstIndex, int secondIndex)
        {
            CommandBuffer.RenderCommand first = commands[firstIndex];
            CommandBuffer.RenderCommand second = commands[secondIndex];

            // Extract draw command data - must be in expected format
            DrawInstancedCommandData? firstDataNullable = ExtractDrawInstancedData(first.Data);
            DrawInstancedCommandData? secondDataNullable = ExtractDrawInstancedData(second.Data);

            if (!firstDataNullable.HasValue || !secondDataNullable.HasValue)
            {
                return false; // Cannot extract data - data format not recognized, cannot merge
            }

            DrawInstancedCommandData firstData = firstDataNullable.Value;
            DrawInstancedCommandData secondData = secondDataNullable.Value;

            // Check if we successfully extracted valid data (non-null buffers indicate valid extraction)
            if (firstData.VertexBuffer == null || firstData.IndexBuffer == null ||
                secondData.VertexBuffer == null || secondData.IndexBuffer == null)
            {
                return false; // Invalid or unrecognized data format - cannot merge
            }

            // Check if commands use the same buffers and have identical geometry
            if (!AreBuffersEqual(firstData.VertexBuffer, secondData.VertexBuffer) ||
                !AreBuffersEqual(firstData.IndexBuffer, secondData.IndexBuffer) ||
                firstData.PrimitiveType != secondData.PrimitiveType ||
                firstData.BaseVertex != secondData.BaseVertex ||
                firstData.StartIndex != secondData.StartIndex ||
                firstData.PrimitiveCountPerInstance != secondData.PrimitiveCountPerInstance)
            {
                return false; // Cannot merge - different buffers, geometry, or primitive type
            }

            // Merge: Combine instance counts (same geometry, more instances)
            DrawInstancedCommandData mergedInstancedData = new DrawInstancedCommandData(
                firstData.PrimitiveType,
                firstData.BaseVertex,
                firstData.MinVertexIndex,
                firstData.NumVertices,
                firstData.StartIndex,
                firstData.PrimitiveCountPerInstance,
                firstData.InstanceCount + secondData.InstanceCount, // Combined instance count
                firstData.StartInstanceLocation, // Start at first draw's start instance
                firstData.VertexBuffer,
                firstData.IndexBuffer);

            // Check if either command has instance matrices (from instancing conversion)
            // If so, we need to combine the instance matrices
            Matrix[] combinedInstanceMatrices = null;
            bool firstHasMatrices = first.Data is InstancedDrawCommandData;
            bool secondHasMatrices = second.Data is InstancedDrawCommandData;

            if (firstHasMatrices || secondHasMatrices)
            {
                // Extract instance matrices from both commands
                Matrix[] firstMatrices = null;
                Matrix[] secondMatrices = null;

                if (firstHasMatrices)
                {
                    InstancedDrawCommandData firstWrapper = (InstancedDrawCommandData)first.Data;
                    firstMatrices = firstWrapper.InstanceMatrices;
                }

                if (secondHasMatrices)
                {
                    InstancedDrawCommandData secondWrapper = (InstancedDrawCommandData)second.Data;
                    secondMatrices = secondWrapper.InstanceMatrices;
                }

                // Combine instance matrices
                int totalInstanceCount = firstData.InstanceCount + secondData.InstanceCount;
                combinedInstanceMatrices = new Matrix[totalInstanceCount];

                // Copy first command's instance matrices
                if (firstMatrices != null && firstMatrices.Length == firstData.InstanceCount)
                {
                    Array.Copy(firstMatrices, 0, combinedInstanceMatrices, 0, firstData.InstanceCount);
                }
                else
                {
                    // If first command doesn't have matrices, create identity matrices
                    // (This shouldn't happen in practice, but handle it gracefully)
                    for (int i = 0; i < firstData.InstanceCount; i++)
                    {
                        combinedInstanceMatrices[i] = Matrix.Identity;
                    }
                }

                // Copy second command's instance matrices
                if (secondMatrices != null && secondMatrices.Length == secondData.InstanceCount)
                {
                    Array.Copy(secondMatrices, 0, combinedInstanceMatrices, firstData.InstanceCount, secondData.InstanceCount);
                }
                else
                {
                    // If second command doesn't have matrices, create identity matrices
                    for (int i = 0; i < secondData.InstanceCount; i++)
                    {
                        combinedInstanceMatrices[firstData.InstanceCount + i] = Matrix.Identity;
                    }
                }

                // Create extended instanced command data with combined matrices
                InstancedDrawCommandData extendedMergedData = new InstancedDrawCommandData
                {
                    InstancedData = mergedInstancedData,
                    InstanceMatrices = combinedInstanceMatrices
                };

                // Update the first command with merged data
                CommandBuffer.RenderCommand mergedCommand = new CommandBuffer.RenderCommand
                {
                    Type = first.Type,
                    Data = extendedMergedData,
                    SortKey = first.SortKey
                };
                commands[firstIndex] = mergedCommand;
                return true;
            }
            else
            {
                // No instance matrices - use standard instanced command data
                // Update the first command with merged data
                CommandBuffer.RenderCommand mergedCommand = new CommandBuffer.RenderCommand
                {
                    Type = first.Type,
                    Data = mergedInstancedData,
                    SortKey = first.SortKey
                };
                commands[firstIndex] = mergedCommand;
                return true;
            }
        }

        /// <summary>
        /// Checks if two commands can be merged.
        ///
        /// Commands can be merged if they:
        /// 1. Have the same sort key (same render state, material, etc.)
        /// 2. Are the same command type (Draw, DrawIndexed, or DrawInstanced)
        /// </summary>
        private bool CanMerge(CommandBuffer.RenderCommand a, CommandBuffer.RenderCommand b)
        {
            // Commands can be merged if they have the same sort key (same state, material, etc.)
            // and are draw commands of compatible types
            if (a.SortKey != b.SortKey)
            {
                return false; // Different render state - cannot merge
            }

            // Both must be draw commands
            bool aIsDraw = a.Type == CommandBuffer.CommandType.Draw ||
                          a.Type == CommandBuffer.CommandType.DrawIndexed ||
                          a.Type == CommandBuffer.CommandType.DrawInstanced;

            bool bIsDraw = b.Type == CommandBuffer.CommandType.Draw ||
                          b.Type == CommandBuffer.CommandType.DrawIndexed ||
                          b.Type == CommandBuffer.CommandType.DrawInstanced;

            if (!aIsDraw || !bIsDraw)
            {
                return false; // Not draw commands - cannot merge
            }

            // Must be the same draw command type
            return a.Type == b.Type;
        }

        /// <summary>
        /// Extracts draw indexed command data from command data object.
        ///
        /// Returns the extracted data if successful, null if the data format is not recognized.
        /// This ensures we only attempt to merge commands with known, valid data formats.
        /// </summary>
        private DrawIndexedCommandData? ExtractDrawIndexedData(object data)
        {
            if (data == null)
            {
                return null; // Null data - cannot extract
            }

            // If data is already a DrawIndexedCommandData structure (boxed or unboxed), return it
            if (data is DrawIndexedCommandData drawData)
            {
                return drawData;
            }

            // Data format not recognized - return null to indicate extraction failure
            // This prevents incorrect merging when data is in an unknown format
            return null;
        }

        /// <summary>
        /// Extracts draw command data from command data object.
        ///
        /// Returns the extracted data if successful, null if the data format is not recognized.
        /// This ensures we only attempt to merge commands with known, valid data formats.
        /// </summary>
        private DrawCommandData? ExtractDrawData(object data)
        {
            if (data == null)
            {
                return null; // Null data - cannot extract
            }

            // If data is already a DrawCommandData structure (boxed or unboxed), return it
            if (data is DrawCommandData drawData)
            {
                return drawData;
            }

            // Data format not recognized - return null to indicate extraction failure
            // This prevents incorrect merging when data is in an unknown format
            return null;
        }

        /// <summary>
        /// Extracts draw instanced command data from command data object.
        ///
        /// Returns the extracted data if successful, null if the data format is not recognized.
        /// This ensures we only attempt to merge commands with known, valid data formats.
        ///
        /// Handles both DrawInstancedCommandData and InstancedDrawCommandData (wrapper with instance matrices).
        /// </summary>
        private DrawInstancedCommandData? ExtractDrawInstancedData(object data)
        {
            if (data == null)
            {
                return null; // Null data - cannot extract
            }

            // If data is already a DrawInstancedCommandData structure (boxed or unboxed), return it
            if (data is DrawInstancedCommandData drawData)
            {
                return drawData;
            }

            // If data is InstancedDrawCommandData (wrapper with instance matrices), extract the instanced data
            if (data is InstancedDrawCommandData instancedWrapper)
            {
                return instancedWrapper.InstancedData;
            }

            // Data format not recognized - return null to indicate extraction failure
            // This prevents incorrect merging when data is in an unknown format
            return null;
        }

        /// <summary>
        /// Checks if two buffer objects are equal.
        /// Buffers are compared by reference equality, native handle identity, or buffer identity.
        ///
        /// Comparison strategy (in order of preference):
        /// 1. Reference equality (fast path for same object instance)
        /// 2. Native handle comparison (for IVertexBuffer/IIndexBuffer interfaces with NativeHandle)
        /// 3. MonoGame buffer handle comparison (for VertexBuffer/IndexBuffer via reflection)
        /// 4. GetHashCode/Equals comparison (fallback for other buffer types)
        ///
        /// This ensures that buffers representing the same GPU resource are correctly identified
        /// even if they are different object instances, enabling proper command merging optimization.
        /// </summary>
        private bool AreBuffersEqual(object bufferA, object bufferB)
        {
            if (bufferA == null && bufferB == null)
            {
                return true; // Both null - equal
            }

            if (bufferA == null || bufferB == null)
            {
                return false; // One null, one not - not equal
            }

            // Fast path: Reference equality check
            if (ReferenceEquals(bufferA, bufferB))
            {
                return true; // Same object instance - definitely equal
            }

            // Check if both implement IVertexBuffer or IIndexBuffer interfaces
            // These interfaces expose NativeHandle property for buffer identity comparison
            IVertexBuffer vertexBufferA = bufferA as IVertexBuffer;
            IVertexBuffer vertexBufferB = bufferB as IVertexBuffer;
            if (vertexBufferA != null && vertexBufferB != null)
            {
                // Both are vertex buffers - compare native handles
                IntPtr handleA = vertexBufferA.NativeHandle;
                IntPtr handleB = vertexBufferB.NativeHandle;

                // If both have valid native handles, compare them
                if (handleA != IntPtr.Zero && handleB != IntPtr.Zero)
                {
                    return handleA == handleB; // Same native handle = same GPU resource
                }

                // If native handles are not available (e.g., MonoGame wrappers return IntPtr.Zero),
                // fall through to other comparison methods
            }

            IIndexBuffer indexBufferA = bufferA as IIndexBuffer;
            IIndexBuffer indexBufferB = bufferB as IIndexBuffer;
            if (indexBufferA != null && indexBufferB != null)
            {
                // Both are index buffers - compare native handles
                IntPtr handleA = indexBufferA.NativeHandle;
                IntPtr handleB = indexBufferB.NativeHandle;

                // If both have valid native handles, compare them
                if (handleA != IntPtr.Zero && handleB != IntPtr.Zero)
                {
                    return handleA == handleB; // Same native handle = same GPU resource
                }

                // If native handles are not available, fall through to other comparison methods
            }

            // Check if both are MonoGame VertexBuffer or IndexBuffer types
            // MonoGame buffers may have internal handles that we can access via reflection
            // This is necessary because MonoGame's VertexBuffer/IndexBuffer don't expose handles directly
            VertexBuffer mgVertexBufferA = bufferA as VertexBuffer;
            VertexBuffer mgVertexBufferB = bufferB as VertexBuffer;
            if (mgVertexBufferA != null && mgVertexBufferB != null)
            {
                // Try to get internal handle via reflection
                IntPtr? handleA = GetMonoGameBufferHandle(mgVertexBufferA);
                IntPtr? handleB = GetMonoGameBufferHandle(mgVertexBufferB);

                if (handleA.HasValue && handleB.HasValue)
                {
                    return handleA.Value == handleB.Value; // Same internal handle = same GPU resource
                }

                // If reflection fails, use GetHashCode/Equals as fallback
                // MonoGame buffers should have proper equality semantics
                return mgVertexBufferA.Equals(mgVertexBufferB);
            }

            IndexBuffer mgIndexBufferA = bufferA as IndexBuffer;
            IndexBuffer mgIndexBufferB = bufferB as IndexBuffer;
            if (mgIndexBufferA != null && mgIndexBufferB != null)
            {
                // Try to get internal handle via reflection
                IntPtr? handleA = GetMonoGameBufferHandle(mgIndexBufferA);
                IntPtr? handleB = GetMonoGameBufferHandle(mgIndexBufferB);

                if (handleA.HasValue && handleB.HasValue)
                {
                    return handleA.Value == handleB.Value; // Same internal handle = same GPU resource
                }

                // If reflection fails, use GetHashCode/Equals as fallback
                return mgIndexBufferA.Equals(mgIndexBufferB);
            }

            // Final fallback: Use object.Equals for any other buffer types
            // This handles custom buffer implementations that may override Equals
            return bufferA.Equals(bufferB);
        }

        /// <summary>
        /// Attempts to extract the native handle from a MonoGame buffer via reflection.
        ///
        /// MonoGame's VertexBuffer and IndexBuffer store internal handles that identify
        /// the GPU resource, but these are not directly exposed. This method uses reflection
        /// to access internal fields that may contain the buffer handle.
        ///
        /// Common internal field names in MonoGame:
        /// - _glBuffer (OpenGL buffer ID)
        /// - _d3dBuffer (DirectX buffer pointer)
        /// - Handle (generic handle property)
        ///
        /// Returns the handle if found, null otherwise.
        /// </summary>
        private IntPtr? GetMonoGameBufferHandle(object buffer)
        {
            if (buffer == null)
            {
                return null;
            }

            Type bufferType = buffer.GetType();

            // Try to find a Handle property (common in graphics APIs)
            PropertyInfo handleProperty = bufferType.GetProperty("Handle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (handleProperty != null)
            {
                object handleValue = handleProperty.GetValue(buffer);
                if (handleValue != null)
                {
                    // Try to convert to IntPtr
                    if (handleValue is IntPtr)
                    {
                        return (IntPtr)handleValue;
                    }

                    // Try to convert from uint (OpenGL buffer IDs are often uint)
                    if (handleValue is uint)
                    {
                        return new IntPtr((uint)handleValue);
                    }

                    // Try to convert from int
                    if (handleValue is int)
                    {
                        return new IntPtr((int)handleValue);
                    }
                }
            }

            // Try to find _glBuffer field (OpenGL buffer ID)
            FieldInfo glBufferField = bufferType.GetField("_glBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
            if (glBufferField != null)
            {
                object glBufferValue = glBufferField.GetValue(buffer);
                if (glBufferValue != null)
                {
                    if (glBufferValue is uint)
                    {
                        return new IntPtr((uint)glBufferValue);
                    }
                    if (glBufferValue is int)
                    {
                        return new IntPtr((int)glBufferValue);
                    }
                    if (glBufferValue is IntPtr)
                    {
                        return (IntPtr)glBufferValue;
                    }
                }
            }

            // Try to find _d3dBuffer field (DirectX buffer pointer)
            FieldInfo d3dBufferField = bufferType.GetField("_d3dBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
            if (d3dBufferField != null)
            {
                object d3dBufferValue = d3dBufferField.GetValue(buffer);
                if (d3dBufferValue != null)
                {
                    if (d3dBufferValue is IntPtr)
                    {
                        return (IntPtr)d3dBufferValue;
                    }
                }
            }

            // Try to find any field with "Handle" in the name
            FieldInfo[] allFields = bufferType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in allFields)
            {
                string fieldName = field.Name.ToLowerInvariant();
                if (fieldName.Contains("handle") || fieldName.Contains("bufferid") || fieldName.Contains("resource"))
                {
                    object fieldValue = field.GetValue(buffer);
                    if (fieldValue != null)
                    {
                        if (fieldValue is IntPtr)
                        {
                            return (IntPtr)fieldValue;
                        }
                        if (fieldValue is uint)
                        {
                            return new IntPtr((uint)fieldValue);
                        }
                        if (fieldValue is int)
                        {
                            return new IntPtr((int)fieldValue);
                        }
                    }
                }
            }

            // Could not find handle via reflection
            return null;
        }

        /// <summary>
        /// Attempts to convert two indexed draw commands with same geometry but different transforms to instanced rendering.
        ///
        /// This optimization converts multiple draw calls of the same geometry with different world transforms
        /// into a single instanced draw call, dramatically reducing draw call overhead.
        ///
        /// Requirements for instancing conversion:
        /// 1. Both commands must have the same geometry (same buffers, same indices, same vertex ranges)
        /// 2. Both commands must have world transform matrices available
        /// 3. The transforms must be different (otherwise they would be identical draws)
        /// 4. The primitive type must support instancing (TriangleList, LineList, PointList)
        /// </summary>
        /// <param name="commands">List of commands being optimized.</param>
        /// <param name="firstIndex">Index of the first draw command.</param>
        /// <param name="secondIndex">Index of the second draw command.</param>
        /// <param name="firstData">First draw command data.</param>
        /// <param name="secondData">Second draw command data.</param>
        /// <returns>True if conversion to instanced rendering was successful, false otherwise.</returns>
        private bool TryConvertToInstancedRendering(List<CommandBuffer.RenderCommand> commands, int firstIndex, int secondIndex, DrawIndexedCommandData firstData, DrawIndexedCommandData secondData)
        {
            // Check if both commands have world transform matrices available
            if (!firstData.WorldMatrix.HasValue || !secondData.WorldMatrix.HasValue)
            {
                return false; // Cannot convert to instancing without transform data
            }

            // Check if transforms are different (if identical, they would be redundant draws)
            Matrix firstMatrix = firstData.WorldMatrix.Value;
            Matrix secondMatrix = secondData.WorldMatrix.Value;
            if (AreMatricesEqual(firstMatrix, secondMatrix))
            {
                return false; // Transforms are identical - would be redundant instances
            }

            // Check if geometry is identical (required for instancing)
            // Geometry is identical if:
            // 1. Same buffers (already checked in TryMergeDrawIndexedCommands)
            // 2. Same base vertex
            // 3. Same start index
            // 4. Same primitive count
            // 5. Same vertex range (min vertex index, num vertices)
            if (firstData.BaseVertex != secondData.BaseVertex ||
                firstData.StartIndex != secondData.StartIndex ||
                firstData.PrimitiveCount != secondData.PrimitiveCount ||
                firstData.MinVertexIndex != secondData.MinVertexIndex ||
                firstData.NumVertices != secondData.NumVertices)
            {
                return false; // Geometry differs - cannot use instancing
            }

            // Check if primitive type supports instancing
            // Instancing is typically supported for TriangleList, LineList, PointList
            // TriangleStrip and LineStrip can be instanced but require special handling
            if (firstData.PrimitiveType != XnaPrimitiveType.TriangleList &&
                firstData.PrimitiveType != XnaPrimitiveType.LineList &&
                firstData.PrimitiveType != XnaPrimitiveType.PointList)
            {
                // TriangleStrip and LineStrip can be instanced but require careful handling
                // For now, we only support TriangleList, LineList, and PointList
                return false;
            }

            // Create instance data for the two transforms
            // Instance data contains world matrices for each instance
            Matrix[] instanceMatrices = new Matrix[]
            {
                firstMatrix,
                secondMatrix
            };

            // Create instance buffer data
            // Based on GPUInstancing.InstanceData structure: Matrix WorldMatrix (64 bytes)
            // For MonoGame, we'll create a simple instance buffer with just the matrices
            // In a full implementation, this would use DynamicVertexBuffer with InstanceData structure
            // For now, we'll store the matrices in the command data and let the renderer handle buffer creation

            // Create instanced draw command data
            // Both instances use the same geometry (firstData geometry)
            DrawInstancedCommandData instancedData = new DrawInstancedCommandData(
                firstData.PrimitiveType,
                firstData.BaseVertex,
                firstData.MinVertexIndex,
                firstData.NumVertices,
                firstData.StartIndex,
                firstData.PrimitiveCount, // Primitive count per instance (same for both)
                2, // Instance count (two instances)
                0, // Start instance location
                firstData.VertexBuffer,
                firstData.IndexBuffer);

            // Create extended instanced command data that includes instance matrices
            // We need to store the instance matrices somewhere - create a wrapper structure
            InstancedDrawCommandData extendedData = new InstancedDrawCommandData
            {
                InstancedData = instancedData,
                InstanceMatrices = instanceMatrices
            };

            // Update the first command to be an instanced draw
            CommandBuffer.RenderCommand instancedCommand = new CommandBuffer.RenderCommand
            {
                Type = CommandBuffer.CommandType.DrawInstanced,
                Data = extendedData,
                SortKey = commands[firstIndex].SortKey // Preserve sort key (same render state)
            };
            commands[firstIndex] = instancedCommand;

            // Mark the second command for removal (it's been merged into the instanced draw)
            // The caller will remove it
            return true;
        }

        /// <summary>
        /// Extended instanced draw command data that includes instance transform matrices.
        /// This structure wraps DrawInstancedCommandData and adds instance-specific transform data.
        /// </summary>
        private struct InstancedDrawCommandData
        {
            public DrawInstancedCommandData InstancedData;
            public Matrix[] InstanceMatrices;
        }

        /// <summary>
        /// Checks if two matrices are equal (within floating-point tolerance).
        /// </summary>
        /// <param name="a">First matrix.</param>
        /// <param name="b">Second matrix.</param>
        /// <returns>True if matrices are equal within tolerance, false otherwise.</returns>
        private bool AreMatricesEqual(Matrix a, Matrix b)
        {
            const float tolerance = 0.0001f; // Floating-point comparison tolerance

            return Math.Abs(a.M11 - b.M11) < tolerance && Math.Abs(a.M12 - b.M12) < tolerance &&
                   Math.Abs(a.M13 - b.M13) < tolerance && Math.Abs(a.M14 - b.M14) < tolerance &&
                   Math.Abs(a.M21 - b.M21) < tolerance && Math.Abs(a.M22 - b.M22) < tolerance &&
                   Math.Abs(a.M23 - b.M23) < tolerance && Math.Abs(a.M24 - b.M24) < tolerance &&
                   Math.Abs(a.M31 - b.M31) < tolerance && Math.Abs(a.M32 - b.M32) < tolerance &&
                   Math.Abs(a.M33 - b.M33) < tolerance && Math.Abs(a.M34 - b.M34) < tolerance &&
                   Math.Abs(a.M41 - b.M41) < tolerance && Math.Abs(a.M42 - b.M42) < tolerance &&
                   Math.Abs(a.M43 - b.M43) < tolerance && Math.Abs(a.M44 - b.M44) < tolerance;
        }

        /// <summary>
        /// Gets the number of indices per primitive for a given primitive type.
        /// </summary>
        private int GetIndicesPerPrimitive(XnaPrimitiveType primitiveType)
        {
            switch (primitiveType)
            {
                case XnaPrimitiveType.TriangleList:
                    return 3;
                case XnaPrimitiveType.TriangleStrip:
                    return 1; // Triangle strip uses 1 index per triangle after first
                case XnaPrimitiveType.LineList:
                    return 2;
                case XnaPrimitiveType.LineStrip:
                    return 1; // Line strip uses 1 index per line after first
                case XnaPrimitiveType.PointList:
                    return 1;
                default:
                    return 3; // Default to triangle list
            }
        }

        /// <summary>
        /// Gets the number of vertices per primitive for a given primitive type.
        /// </summary>
        private int GetVerticesPerPrimitive(XnaPrimitiveType primitiveType)
        {
            switch (primitiveType)
            {
                case XnaPrimitiveType.TriangleList:
                    return 3;
                case XnaPrimitiveType.TriangleStrip:
                    return 1; // Triangle strip uses 1 vertex per triangle after first
                case XnaPrimitiveType.LineList:
                    return 2;
                case XnaPrimitiveType.LineStrip:
                    return 1; // Line strip uses 1 vertex per line after first
                case XnaPrimitiveType.PointList:
                    return 1;
                default:
                    return 3; // Default to triangle list
            }
        }
    }
}

