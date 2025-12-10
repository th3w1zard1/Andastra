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
        ModuleResource Git();
        ModuleResource Layout();
        ModuleResource Creature(string resref);
        ModuleResource Door(string resref);
        ModuleResource Placeable(string resref);
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

            ModuleResource gitRes = module?.Git();
            if (gitRes != null)
            {
                git = gitRes.Resource();
            }

            ModuleResource lytRes = module?.Layout();
            if (lytRes != null)
            {
                lyt = lytRes.Resource();
            }

            return (git, lyt);
        }

        public Dictionary<string, object> GetCreatureModelData(object gitCreature, Module module)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module_loader.py:98-173
            // Original: def get_creature_model_data(self, git_creature, module: Module) -> dict[str, str | None]:

            // Get creature resource from module
            var creatureResRef = ""; // TODO: Extract resref from gitCreature (GITCreature.ResRef)
            var creatureResource = module.Creature(creatureResRef);

            if (creatureResource == null)
            {
                return new Dictionary<string, object>
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

            var utc = creatureResource.Resource() as UTC;
            if (utc == null)
            {
                return new Dictionary<string, object>
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

            // TODO: Implement creature.get_body_model, get_head_model, get_weapon_models, get_mask_model
            // These require porting the creature utility functions from tools/creature.py
            // For now, return placeholder values
            var bodyModel = (string)null; // creature.get_body_model(utc, _installation, appearance: TableCreatures, baseitems: TableBaseItems);
            var bodyTexture = (string)null; // creature.get_body_model(utc, _installation, appearance: TableCreatures, baseitems: TableBaseItems);
            var headModel = (string)null; // creature.get_head_model(utc, _installation, appearance: TableCreatures, heads: TableHeads);
            var headTexture = (string)null; // creature.get_head_model(utc, _installation, appearance: TableCreatures, heads: TableHeads);
            var rhandModel = (string)null; // creature.get_weapon_models(utc, _installation, appearance: TableCreatures, baseitems: TableBaseItems);
            var lhandModel = (string)null; // creature.get_weapon_models(utc, _installation, appearance: TableCreatures, baseitems: TableBaseItems);
            var maskModel = (string)null; // creature.get_mask_model(utc, _installation);

            return new Dictionary<string, object>
            {
                { "body_model", bodyModel },
                { "body_texture", bodyTexture },
                { "head_model", headModel },
                { "head_texture", headTexture },
                { "rhand_model", rhandModel },
                { "lhand_model", lhandModel },
                { "mask_model", maskModel }
            };
        }

        public string GetDoorModelName(object door, Module module)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module_loader.py:175-199
            // Original: def get_door_model_name(self, door, module: Module) -> str | None:

            // Get door resource from module
            var doorResRef = ""; // TODO: Extract resref from door (GITDoor.ResRef)
            var doorResource = module.Door(doorResRef);

            if (doorResource == null)
            {
                return null;
            }

            var utd = doorResource.Resource() as UTD;
            if (utd == null)
            {
                return null;
            }

            // Get appearance_id from UTD and lookup in TableDoors
            var appearanceId = utd.AppearanceId;
            var row = TableDoors.GetRow(appearanceId);

            if (row == null)
            {
                return null;
            }

            return row.GetString("modelname");
        }

        public string GetPlaceableModelName(object placeable, Module module)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/module_loader.py:201-225
            // Original: def get_placeable_model_name(self, placeable, module: Module) -> str | None:

            // Get placeable resource from module
            var placeableResRef = ""; // TODO: Extract resref from placeable (GITPlaceable.ResRef)
            var placeableResource = module.Placeable(placeableResRef);

            if (placeableResource == null)
            {
                return null;
            }

            var utp = placeableResource.Resource() as UTP;
            if (utp == null)
            {
                return null;
            }

            // Get appearance_id from UTP and lookup in TablePlaceables
            var appearanceId = utp.AppearanceId;
            var row = TablePlaceables.GetRow(appearanceId);

            if (row == null)
            {
                return null;
            }

            return row.GetString("modelname");
        }
    }
}

