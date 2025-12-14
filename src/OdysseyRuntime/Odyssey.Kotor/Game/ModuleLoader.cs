using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Entities;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Module;
using Odyssey.Core.Navigation;
using Odyssey.Core.Enums;
using CSharpKOTOR.Resource.Generics.DLG;
using JetBrains.Annotations;

namespace Odyssey.Kotor.Game
{
    /// <summary>
    /// Loads modules from KOTOR game files.
    /// TODO: Integrate with CSharpKOTOR resource loading properly.
    /// </summary>
    public class ModuleLoader
    {
        private readonly string _gamePath;
        private readonly World _world;
        private NavigationMesh _currentNavMesh;
        private RuntimeArea _currentArea;

        public ModuleLoader(string gamePath, World world)
        {
            _gamePath = gamePath;
            _world = world;
        }

        /// <summary>
        /// Gets the navigation mesh for the current module.
        /// </summary>
        [CanBeNull]
        public NavigationMesh GetNavigationMesh()
        {
            return _currentNavMesh;
        }

        /// <summary>
        /// Loads a dialogue by ResRef.
        /// </summary>
        [CanBeNull]
        public DLG LoadDialogue(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                return null;
            }

            // TODO: Integrate with CSharpKOTOR resource provider
            // For now, try to load from module or override folder
            string dialogPath = Path.Combine(_gamePath, "override", resRef + ".dlg");
            if (!File.Exists(dialogPath))
            {
                // Try modules folder
                dialogPath = Path.Combine(_gamePath, "modules", resRef + ".dlg");
            }

            if (!File.Exists(dialogPath))
            {
                Console.WriteLine("[ModuleLoader] Dialogue not found: " + resRef);
                return null;
            }

            try
            {
                byte[] data = File.ReadAllBytes(dialogPath);
                return DLGHelper.ReadDlg(data);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load dialogue " + resRef + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Loads a script by ResRef.
        /// </summary>
        [CanBeNull]
        public byte[] LoadScript(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                return null;
            }

            // TODO: Integrate with CSharpKOTOR resource provider
            // For now, try to load from module or override folder
            string scriptPath = Path.Combine(_gamePath, "override", resRef + ".ncs");
            if (!File.Exists(scriptPath))
            {
                // Try modules folder
                scriptPath = Path.Combine(_gamePath, "modules", resRef + ".ncs");
            }

            if (!File.Exists(scriptPath))
            {
                Console.WriteLine("[ModuleLoader] Script not found: " + resRef);
                return null;
            }

            try
            {
                return File.ReadAllBytes(scriptPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleLoader] Failed to load script " + resRef + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Load a module by name.
        /// </summary>
        public void LoadModule(string moduleName)
        {
            Console.WriteLine("[ModuleLoader] Loading module: " + moduleName);

            // Find module files
            string modulesPath = Path.Combine(_gamePath, "modules");
            string rimPath = Path.Combine(modulesPath, moduleName + ".rim");
            string modPath = Path.Combine(modulesPath, moduleName + ".mod");

            bool moduleExists = File.Exists(modPath) || File.Exists(rimPath);
            if (!moduleExists)
            {
                Console.Error.WriteLine("[ModuleLoader] WARNING: Module file not found: " + moduleName);
                Console.Error.WriteLine("[ModuleLoader] Checked: " + modPath);
                Console.Error.WriteLine("[ModuleLoader] Checked: " + rimPath);
                // Continue with placeholder data for testing
            }

            // TODO: Parse actual module files using CSharpKOTOR
            // For now, create placeholder module/area for testing

            var runtimeModule = CreatePlaceholderModule(moduleName);
            _world.CurrentModule = runtimeModule;

            var runtimeArea = CreatePlaceholderArea(moduleName);
            _world.CurrentArea = runtimeArea;
            _currentArea = runtimeArea;

            // Create some placeholder entities
            CreatePlaceholderEntities(runtimeArea);

            // Create placeholder navigation mesh
            _currentNavMesh = CreatePlaceholderNavMesh(runtimeArea);

            Console.WriteLine("[ModuleLoader] Module loaded (placeholder): " + moduleName);
        }

        private RuntimeModule CreatePlaceholderModule(string moduleName)
        {
            var module = new RuntimeModule();
            module.ResRef = moduleName;
            module.DisplayName = "Module: " + moduleName;
            module.EntryArea = moduleName;

            // TODO: Load these from IFO file
            // module.EntryPosition = new Vector3(0, 0, 0);
            // module.EntryFacing = 0;
            module.DawnHour = 6;
            module.DuskHour = 18;
            module.MinutesPastMidnight = 720; // Noon

            Console.WriteLine("[ModuleLoader] FIXME: Module properties not loaded from IFO");
            return module;
        }

        private RuntimeArea CreatePlaceholderArea(string areaName)
        {
            var area = new RuntimeArea();
            area.ResRef = areaName;
            area.DisplayName = "Area: " + areaName;
            area.Tag = areaName;

            // TODO: Load lighting from ARE file
            // TODO: Load fog settings
            // TODO: Load walkmesh from BWM files
            // TODO: Load room layout from LYT file

            Console.WriteLine("[ModuleLoader] FIXME: Area properties not loaded from ARE/GIT");
            return area;
        }

        private void CreatePlaceholderEntities(RuntimeArea area)
        {
            // Create a placeholder player spawn point
            var playerSpawn = _world.CreateEntity(ObjectType.Waypoint, Vector3.Zero, 0);
            playerSpawn.Tag = "wp_player_spawn";
            area.AddEntity(playerSpawn);

            // TODO: Load creatures from GIT Creature List
            // TODO: Load placeables from GIT Placeable List
            // TODO: Load doors from GIT Door List
            // TODO: Load triggers from GIT TriggerList
            // TODO: Load waypoints from GIT WaypointList
            // TODO: Load sounds from GIT SoundList

            Console.WriteLine("[ModuleLoader] FIXME: Entities not loaded from GIT");
        }

        private NavigationMesh CreatePlaceholderNavMesh(RuntimeArea area)
        {
            // Create a simple flat navigation mesh for testing
            // This would normally be loaded from BWM/WOK files
            var navMesh = new NavigationMesh();

            // Add a simple ground plane for testing
            var vertices = new List<Vector3>
            {
                new Vector3(-50, 0, -50),
                new Vector3(50, 0, -50),
                new Vector3(50, 0, 50),
                new Vector3(-50, 0, 50)
            };

            var indices = new List<int> { 0, 1, 2, 0, 2, 3 };

            navMesh.BuildFromTriangles(vertices, indices);

            Console.WriteLine("[ModuleLoader] FIXME: NavMesh placeholder created - should load from BWM");
            return navMesh;
        }
    }
}
