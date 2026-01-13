using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BioWare.NET.Resource.Formats.GFF.Generics.DLG;

namespace HolocronToolset.Editors.DLG
{
    /// <summary>
    /// Debug utilities for DLG editor.
    /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/dlg/debug_utils.py
    /// </summary>
    public static class DLGDebugUtils
    {
        /// <summary>
        /// Generate a string representation of the object with additional details.
        /// Matching PyKotor: def custom_extra_info(obj) -> str
        /// </summary>
        public static string CustomExtraInfo(object obj)
        {
            if (obj == null)
            {
                return "null";
            }
            return $"{obj.GetType().Name} id={RuntimeHelpers.GetHashCode(obj)}";
        }

        /// <summary>
        /// Custom function to provide additional details about objects in the graph.
        /// Matching PyKotor: def detailed_extra_info(obj) -> str
        /// </summary>
        public static string DetailedExtraInfo(object obj)
        {
            if (obj == null)
            {
                return "null";
            }
            try
            {
                return obj.ToString();
            }
            catch (Exception)
            {
                return obj.GetType().Name;
            }
        }

        /// <summary>
        /// Filter to decide if the object should be included in the graph.
        /// Matching PyKotor: def is_interesting(obj) -> bool
        /// </summary>
        public static bool IsInteresting(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            // Check if object has fields/properties (has __dict__ equivalent)
            Type type = obj.GetType();
            return type.GetFields(BindingFlags.Public | BindingFlags.Instance).Length > 0 ||
                   type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Length > 0 ||
                   obj is ICollection;
        }

        /// <summary>
        /// Display a graph of back references for the given target_object.
        /// Matching PyKotor: def identify_reference_path(obj, max_depth=10)
        /// Finds all back reference chains leading to the target object using reflection-based graph traversal.
        /// </summary>
        public static void IdentifyReferencePath(object obj, int maxDepth = 10)
        {
            if (obj == null)
            {
                return;
            }

            // Find all back reference chains leading to 'obj'
            // Matching PyKotor: paths = objgraph.find_backref_chain(obj, predicate, max_depth=max_depth)
            List<List<object>> paths = FindBackRefChains(obj, maxDepth);

            // Ensure that paths is a list of lists, even if only one chain is found
            // Matching PyKotor: if not paths: paths = [[obj]]
            if (paths == null || paths.Count == 0)
            {
                paths = new List<List<object>> { new List<object> { obj } };
            }
            // Matching PyKotor: elif not isinstance(paths[0], list): paths = [paths]
            else if (paths.Count > 0 && paths[0] == null)
            {
                paths = new List<List<object>> { new List<object> { obj } };
            }

            // Output each reference path
            // Matching PyKotor: for path in paths: print("Reference Path:"); for ref in path: ...
            foreach (List<object> path in paths)
            {
                if (path == null || path.Count == 0)
                {
                    continue;
                }

                Debug.WriteLine("Reference Path:");
                foreach (object refObj in path)
                {
                    if (refObj == null)
                    {
                        continue;
                    }

                    string refType = refObj.GetType().Name;
                    int refId = RuntimeHelpers.GetHashCode(refObj);
                    StringBuilder refInfo = new StringBuilder($"Type={refType}, ID={refId}");

                    // Try to get more information about the object's definition
                    // Matching PyKotor: if hasattr(ref, "__name__"): ref_info += f", Name={ref.__name__}"
                    try
                    {
                        Type refTypeObj = refObj.GetType();
                        MemberInfo nameMember = refTypeObj.GetMember("__name__", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).FirstOrDefault();
                        if (nameMember == null)
                        {
                            // Check for Name property (common in .NET types)
                            PropertyInfo nameProp = refTypeObj.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                            if (nameProp != null && nameProp.CanRead)
                            {
                                try
                                {
                                    object nameValue = nameProp.GetValue(refObj);
                                    if (nameValue != null)
                                    {
                                        refInfo.Append($", Name={nameValue}");
                                    }
                                }
                                catch (Exception)
                                {
                                    // Ignore errors getting name
                                }
                            }
                        }

                        // Matching PyKotor: if hasattr(ref, "__module__"): ref_info += f", Module={ref.__module__}"
                        // In .NET, use Namespace instead of module
                        if (!string.IsNullOrEmpty(refTypeObj.Namespace))
                        {
                            refInfo.Append($", Module={refTypeObj.Namespace}");
                        }

                        // Method information
                        // Matching PyKotor: if inspect.isfunction(ref) or inspect.ismethod(ref): ...
                        // In .NET, we can get method information from MethodBase
                        if (refObj is MethodBase methodBase)
                        {
                            try
                            {
                                // Get method name and declaring type
                                string methodName = methodBase.Name;
                                string declaringType = methodBase.DeclaringType?.FullName ?? "Unknown";
                                refInfo.Append($", Method={declaringType}.{methodName}");
                            }
                            catch (Exception)
                            {
                                refInfo.Append(", Method info not available");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Matching PyKotor: except Exception as e: ref_info += f", Detail unavailable ({e!s})"
                        refInfo.Append($", Detail unavailable ({ex.Message})");
                    }

                    Debug.WriteLine(refInfo.ToString());
                }
                Debug.WriteLine(string.Empty);
            }
        }

        /// <summary>
        /// Finds all back reference chains leading to the target object.
        /// This is the .NET equivalent of Python's objgraph.find_backref_chain.
        /// Uses reflection to traverse object graphs and find all paths that reference the target.
        /// </summary>
        private static List<List<object>> FindBackRefChains(object target, int maxDepth)
        {
            if (target == null || maxDepth <= 0)
            {
                return new List<List<object>>();
            }

            // Get root objects to start searching from
            // In .NET, we can't enumerate all heap objects, so we start from known roots
            List<object> rootObjects = GetRootObjects();

            // Find all paths that lead to the target using depth-first search
            List<List<object>> allPaths = new List<List<object>>();
            int maxPaths = 100; // Limit number of paths to avoid excessive output

            foreach (object root in rootObjects)
            {
                if (root == null || allPaths.Count >= maxPaths)
                {
                    continue;
                }

                try
                {
                    // Use DFS to find paths from root to target
                    List<object> currentPath = new List<object>();
                    FindPathsDFS(root, target, currentPath, allPaths, new HashSet<object>(), maxDepth, maxPaths);
                }
                catch (Exception)
                {
                    // Ignore errors during path finding
                }
            }

            // If no paths found, return path containing just the target
            // Matching PyKotor: if not paths: paths = [[obj]]
            if (allPaths.Count == 0)
            {
                return new List<List<object>> { new List<object> { target } };
            }

            return allPaths;
        }

        /// <summary>
        /// Gets root objects to start reference tracking from.
        /// In .NET, we can't enumerate all heap objects, so we start from AppDomain and static fields.
        /// </summary>
        private static List<object> GetRootObjects()
        {
            List<object> roots = new List<object>();

            try
            {
                // Add current AppDomain
                roots.Add(AppDomain.CurrentDomain);

                // Scan all loaded assemblies for static fields
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        roots.Add(assembly);

                        // Get all types in the assembly
                        foreach (Type type in assembly.GetTypes())
                        {
                            try
                            {
                                // Get static fields
                                FieldInfo[] staticFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                                foreach (FieldInfo field in staticFields)
                                {
                                    try
                                    {
                                        if (!field.FieldType.IsPrimitive && field.FieldType != typeof(string))
                                        {
                                            object value = field.GetValue(null);
                                            if (value != null && IsInteresting(value))
                                            {
                                                roots.Add(value);
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        // Ignore errors accessing static fields
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // Ignore errors getting types
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore errors accessing assemblies
                    }
                }
            }
            catch (Exception)
            {
                // If we can't get root objects, return empty list
            }

            return roots;
        }

        /// <summary>
        /// Uses depth-first search to find all paths from source to target.
        /// This is more efficient than building the entire graph upfront.
        /// </summary>
        private static void FindPathsDFS(object source, object target, List<object> currentPath, List<List<object>> allPaths, HashSet<object> visited, int maxDepth, int maxPaths)
        {
            if (source == null || target == null || maxDepth <= 0 || allPaths.Count >= maxPaths)
            {
                return;
            }

            // Avoid cycles
            if (visited.Contains(source))
            {
                return;
            }

            // Add current node to path
            currentPath.Add(source);
            visited.Add(source);

            // Check if we found the target
            if (ReferenceEquals(source, target))
            {
                // Found a path - add it to results
                allPaths.Add(new List<object>(currentPath));
                currentPath.RemoveAt(currentPath.Count - 1);
                visited.Remove(source);
                return;
            }

            // Get all objects referenced by source using reflection
            List<object> referencedObjects = GetReferencedObjects(source);

            // Recursively search from each referenced object
            foreach (object referenced in referencedObjects)
            {
                if (allPaths.Count >= maxPaths)
                {
                    break;
                }

                if (referenced != null && !visited.Contains(referenced))
                {
                    FindPathsDFS(referenced, target, currentPath, allPaths, visited, maxDepth - 1, maxPaths);
                }
            }

            // Backtrack
            currentPath.RemoveAt(currentPath.Count - 1);
            visited.Remove(source);
        }

        /// <summary>
        /// Gets all objects referenced by the given object using reflection.
        /// Traverses fields, properties, and collections to find referenced objects.
        /// </summary>
        private static List<object> GetReferencedObjects(object obj)
        {
            List<object> referenced = new List<object>();

            if (obj == null)
            {
                return referenced;
            }

            try
            {
                Type type = obj.GetType();

                // Skip primitive types and strings
                if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
                {
                    return referenced;
                }

                // Get all fields (public and private instance fields)
                FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (FieldInfo field in fields)
                {
                    try
                    {
                        object fieldValue = field.GetValue(obj);
                        if (fieldValue != null)
                        {
                            if (IsInteresting(fieldValue))
                            {
                                referenced.Add(fieldValue);
                            }
                            else if (fieldValue is IEnumerable enumerable && !(fieldValue is string))
                            {
                                // Handle collections
                                foreach (object item in enumerable)
                                {
                                    if (item != null && IsInteresting(item))
                                    {
                                        referenced.Add(item);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore errors accessing fields
                    }
                }

                // Get all properties (public instance properties)
                PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (PropertyInfo property in properties)
                {
                    try
                    {
                        if (!property.CanRead || property.GetIndexParameters().Length > 0)
                        {
                            continue;
                        }

                        object propValue = property.GetValue(obj);
                        if (propValue != null)
                        {
                            if (IsInteresting(propValue))
                            {
                                referenced.Add(propValue);
                            }
                            else if (propValue is IEnumerable enumerable && !(propValue is string))
                            {
                                // Handle collections
                                foreach (object item in enumerable)
                                {
                                    if (item != null && IsInteresting(item))
                                    {
                                        referenced.Add(item);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore errors accessing properties
                    }
                }
            }
            catch (Exception)
            {
                // Ignore errors during reflection
            }

            return referenced;
        }

        /// <summary>
        /// Debug references for an object.
        /// Matching PyKotor: def debug_references(obj: Any)
        /// </summary>
        public static void DebugReferences(object obj)
        {
            IdentifyReferencePath(obj);
        }
    }
}

