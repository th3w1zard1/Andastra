using System.Collections.Generic;
using CSharpKOTOR.Formats.TwoDA;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Resources;

namespace CSharpKOTOR.Common
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module_loader.py (backend-agnostic loader)
    public static class ModuleDataSearch
    {
        public static readonly SearchLocation[] SearchOrder2DA = { SearchLocation.OVERRIDE, SearchLocation.CHITIN };
    }

    /// <summary>
    /// Minimal module resource interfaces to mirror PyKotor loader contracts.
    /// </summary>
    public interface IModuleResource<T>
    {
        T Resource();
    }

    public interface IModule
    {
        IModuleResource<object> Git();
        IModuleResource<object> Layout();
        IModuleResource<object> Creature(string resref);
        IModuleResource<object> Door(string resref);
    }

    /// <summary>
    /// Backend-agnostic module data loader (partial parity).
    /// </summary>
    public class ModuleDataLoader
    {
        private readonly Installation.Installation _installation;

        public TwoDA TableDoors { get; private set; } = new TwoDA();
        public TwoDA TablePlaceables { get; private set; } = new TwoDA();
        public TwoDA TableCreatures { get; private set; } = new TwoDA();
        public TwoDA TableHeads { get; private set; } = new TwoDA();
        public TwoDA TableBaseItems { get; private set; } = new TwoDA();

        public ModuleDataLoader(Installation.Installation installation)
        {
            _installation = installation;
            Load2daTables();
        }

        private void Load2daTables()
        {
            TableDoors = Load2da("genericdoors");
            TablePlaceables = Load2da("placeables");
            TableCreatures = Load2da("appearance");
            TableHeads = Load2da("heads");
            TableBaseItems = Load2da("baseitems");
        }

        private TwoDA Load2da(string name)
        {
            Installation.ResourceResult res = _installation.Resources.LookupResource(name, ResourceType.TwoDA, ModuleDataSearch.SearchOrder2DA);
            if (res == null)
            {
                return new TwoDA();
            }

            var reader = new TwoDABinaryReader(res.Data);
            return reader.Load();
        }

        public (object git, object lyt) GetModuleResources(IModule module)
        {
            object git = null;
            object lyt = null;

            IModuleResource<object> gitRes = module?.Git();
            if (gitRes != null)
            {
                git = gitRes.Resource();
            }

            IModuleResource<object> lytRes = module?.Layout();
            if (lytRes != null)
            {
                lyt = lytRes.Resource();
            }

            return (git, lyt);
        }

        public Dictionary<string, string> GetCreatureModelData(object gitCreature, IModule module)
        {
            // Placeholder parity: actual model resolution depends on creature utilities not yet ported.
            return new Dictionary<string, string>
            {
                { "body_model", null },
                { "body_texture", null },
                { "head_model", null },
                { "head_texture", null },
                { "rhand_model", null },
                { "lhand_model", null },
                { "mask_model", null }
            };
        }

        public string GetDoorModelName(object door, IModule module)
        {
            // Placeholder parity: depends on door UTC/UTD formats and appearance tables.
            return null;
        }
    }
}

