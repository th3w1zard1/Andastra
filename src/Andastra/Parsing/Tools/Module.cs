using System;
using System.Collections.Generic;
using System.IO;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.RIM;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Formats.VIS;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Formats.LYT;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource.Generics.ARE;

namespace Andastra.Parsing.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/module.py
    // Original: Module-related utility functions
    public static class ModuleTools
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/module.py:296-341
        // Original: def rim_to_mod(...)
        public static void RimToMod(
            string filepath,
            string rimFolderpath = null,
            string moduleRoot = null,
            BioWareGame? game = null)
        {
            var rOutpath = new CaseAwarePath(filepath);
            if (!FileHelpers.IsModFile(rOutpath.GetResolvedPath()))
            {
                throw new ArgumentException("Specified file must end with the .mod extension");
            }

            moduleRoot = Installation.Installation.GetModuleRoot(moduleRoot ?? filepath);
            var rRimFolderpath = rimFolderpath != null ? new CaseAwarePath(rimFolderpath) : new CaseAwarePath(Path.GetDirectoryName(filepath));

            string filepathRim = Path.Combine(rRimFolderpath.GetResolvedPath(), $"{moduleRoot}.rim");
            string filepathRimS = Path.Combine(rRimFolderpath.GetResolvedPath(), $"{moduleRoot}_s.rim");
            string filepathDlgErf = Path.Combine(rRimFolderpath.GetResolvedPath(), $"{moduleRoot}_dlg.erf");

            var mod = new ERF(ERFType.MOD);
            if (File.Exists(filepathRim))
            {
                var rim = RIMAuto.ReadRim(filepathRim);
                foreach (var res in rim)
                {
                    mod.SetData(res.ResRef.ToString(), res.ResType, res.Data);
                }
            }

            if (File.Exists(filepathRimS))
            {
                var rimS = RIMAuto.ReadRim(filepathRimS);
                foreach (var res in rimS)
                {
                    mod.SetData(res.ResRef.ToString(), res.ResType, res.Data);
                }
            }

            if ((game == null || game.Value.IsK2()) && File.Exists(filepathDlgErf))
            {
                var dlgErf = ERFAuto.ReadErf(filepathDlgErf);
                foreach (var res in dlgErf)
                {
                    mod.SetData(res.ResRef.ToString(), res.ResType, res.Data);
                }
            }

            ERFAuto.WriteErf(mod, filepath, ResourceType.MOD);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/module.py:51-293
        // Original: def clone_module(...)
        public static void CloneModule(
            string root,
            string identifier,
            string prefix,
            string name,
            Installation.Installation installation,
            bool copyTextures = false,
            bool copyLightmaps = false,
            bool keepDoors = false,
            bool keepPlaceables = false,
            bool keepSounds = false,
            bool keepPathing = false)
        {
            var oldModule = new Module(root, installation);
            var newModule = new ERF(ERFType.MOD);

            var gitRes = oldModule.Git();
            var git = gitRes?.Resource() as Resource.Generics.GIT;
            if (git == null)
            {
                throw new ArgumentException($"No GIT file found in module '{root}'");
            }

            var ifoRes = oldModule.Info();
            var ifo = ifoRes?.Resource() as Resource.Generics.IFO;
            ResRef oldResref = null;
            if (ifo != null)
            {
                oldResref = ifo.ResRef;
                ifo.ResRef = new ResRef(identifier);
                ifo.ModName = LocalizedString.FromEnglish(identifier.ToUpperInvariant());
                ifo.Tag = identifier.ToUpperInvariant();
                ifo.AreaName.SetData(identifier);
                newModule.SetData("module", ResourceType.IFO, GFFAuto.BytesGff(IFOHelpers.DismantleIfo(ifo), ResourceType.GFF));
            }
            else
            {
                new Andastra.Parsing.Logger.RobustLogger().Warning($"No IFO found in module to be cloned: '{root}'");
            }

            var areRes = oldModule.Are();
            var are = areRes?.Resource() as ARE;
            if (are != null)
            {
                are.Name = LocalizedString.FromEnglish(name);
                newModule.SetData(identifier, ResourceType.ARE, GFFAuto.BytesGff(AREHelpers.DismantleAre(are), ResourceType.GFF));
            }
            else
            {
                new Andastra.Parsing.Logger.RobustLogger().Warning($"No ARE found in module to be cloned: '{root}'");
            }

            if (keepPathing)
            {
                var pthRes = oldModule.Pth();
                var pth = pthRes?.Resource() as Resource.Generics.PTH;
                if (pth != null)
                {
                    newModule.SetData(identifier, ResourceType.PTH, GFFAuto.BytesGff(PTHHelpers.DismantlePth(pth), ResourceType.GFF));
                }
            }

            git.Creatures.Clear();
            git.Encounters.Clear();
            git.Stores.Clear();
            git.Triggers.Clear();
            git.Waypoints.Clear();
            git.Cameras.Clear();

            if (keepDoors)
            {
                for (int i = 0; i < git.Doors.Count; i++)
                {
                    var door = git.Doors[i];
                    string oldResname = door.ResRef.ToString();
                    string newResname = $"{identifier}_dor{i}";
                    door.ResRef.SetData(newResname);
                    door.Tag = newResname;

                    var utdModRes = oldModule.Door(oldResname);
                    if (utdModRes == null)
                    {
                        new Andastra.Parsing.Logger.RobustLogger().Warning($"No UTD found for door '{oldResname}' in module '{root}'");
                        continue;
                    }
                    var utdRes = utdModRes.Resource() as Resource.Generics.UTD;
                    if (utdRes == null)
                    {
                        new Andastra.Parsing.Logger.RobustLogger().Warning($"UTD resource is None for door '{oldResname}' in module '{root}'");
                        continue;
                    }

                    newModule.SetData(newResname, ResourceType.UTD, GFFAuto.BytesGff(UTDHelpers.DismantleUtd(utdRes), ResourceType.GFF));
                }
            }
            else
            {
                git.Doors.Clear();
            }

            if (keepPlaceables)
            {
                for (int i = 0; i < git.Placeables.Count; i++)
                {
                    var placeable = git.Placeables[i];
                    string oldResname = placeable.ResRef.ToString();
                    string newResname = $"{identifier}_plc{i}";
                    placeable.ResRef.SetData(newResname);
                    placeable.Tag = newResname;

                    var utpModRes = oldModule.Placeable(oldResname);
                    if (utpModRes == null)
                    {
                        new Andastra.Parsing.Logger.RobustLogger().Warning($"No UTP found for placeable '{oldResname}' in module '{root}'");
                        continue;
                    }
                    var utpRes = utpModRes.Resource() as Resource.Generics.UTP;
                    if (utpRes == null)
                    {
                        new Andastra.Parsing.Logger.RobustLogger().Warning($"UTP resource is None for placeable '{oldResname}' in module '{root}'");
                        continue;
                    }

                    newModule.SetData(newResname, ResourceType.UTP, GFFAuto.BytesGff(UTPHelpers.DismantleUtp(utpRes), ResourceType.GFF));
                }
            }
            else
            {
                git.Placeables.Clear();
            }

            if (keepSounds)
            {
                git.Sounds.Clear();
            }
            else
            {
                for (int i = 0; i < git.Sounds.Count; i++)
                {
                    var sound = git.Sounds[i];
                    string oldResname = sound.ResRef.ToString();
                    string newResname = $"{identifier}_snd{i}";
                    sound.ResRef.SetData(newResname);
                    sound.Tag = newResname;

                    var utsModRes = oldModule.Sound(oldResname);
                    if (utsModRes == null)
                    {
                        new Andastra.Parsing.Logger.RobustLogger().Warning($"No UTS found for sound '{oldResname}' in module '{root}'");
                        continue;
                    }
                    var utsRes = utsModRes.Resource() as Resource.Generics.UTS;
                    if (utsRes == null)
                    {
                        new Andastra.Parsing.Logger.RobustLogger().Warning($"UTS resource is None for sound '{oldResname}' in module '{root}'");
                        continue;
                    }
                    newModule.SetData(newResname, ResourceType.UTS, GFFAuto.BytesGff(UTSHelpers.DismantleUts(utsRes), ResourceType.GFF));
                }
            }

            newModule.SetData(identifier, ResourceType.GIT, GFFAuto.BytesGff(GITHelpers.DismantleGit(git), ResourceType.GFF));

            var lytRes = oldModule.Layout();
            var lyt = lytRes?.Resource() as Andastra.Parsing.Resource.Formats.LYT.LYT;

            var visRes = oldModule.Vis();
            var vis = visRes?.Resource() as Formats.VIS.VIS;

            var newLightmaps = new Dictionary<string, string>();
            var newTextures = new Dictionary<string, string>();
            if (lyt != null)
            {
                foreach (var room in lyt.Rooms)
                {
                    string oldModelName = room.Model;
                    string newModelName = StringUtils.IReplace(oldModelName, oldResref?.ToString() ?? "", identifier);

                    room.Model = new Andastra.Parsing.Common.ResRef(newModelName);
                    if (vis != null && vis.RoomExists(oldModelName))
                    {
                        vis.RenameRoom(oldModelName, newModelName);
                    }

                    var mdlResource = installation.Resource(oldModelName, ResourceType.MDL);
                    byte[] mdlData = mdlResource?.Data;
                    if (mdlData == null)
                    {
                        continue;
                    }
                    var mdxResource = installation.Resource(oldModelName, ResourceType.MDX);
                    byte[] mdxData = mdxResource?.Data;
                    if (mdxData == null)
                    {
                        continue;
                    }
                    var wokResource = installation.Resource(oldModelName, ResourceType.WOK);
                    byte[] wokData = wokResource?.Data;
                    if (wokData == null)
                    {
                        continue;
                    }

                    if (copyTextures)
                    {
                        foreach (string texture in ModelTools.IterateTextures(mdlData))
                        {
                            if (newTextures.ContainsKey(texture))
                            {
                                continue;
                            }
                            string newTextureName = prefix + texture.Substring(3);
                            newTextures[texture] = newTextureName;

                            var tpc = installation.Texture(
                                texture,
                                new[]
                                {
                                    SearchLocation.CHITIN,
                                    SearchLocation.OVERRIDE,
                                    SearchLocation.TEXTURES_GUI,
                                    SearchLocation.TEXTURES_TPA
                                });
                            if (tpc == null)
                            {
                                new Andastra.Parsing.Logger.RobustLogger().Warning($"TPC/TGA resource not found for texture '{texture}' in module '{root}'");
                                continue;
                            }
                            tpc = tpc.Copy();
                            if (tpc.Format() == Formats.TPC.TPCTextureFormat.BGR || tpc.Format() == Formats.TPC.TPCTextureFormat.DXT1 || tpc.Format() == Formats.TPC.TPCTextureFormat.Greyscale)
                            {
                                tpc.Convert(Formats.TPC.TPCTextureFormat.RGB);
                            }
                            else if (tpc.Format() == Formats.TPC.TPCTextureFormat.BGRA || tpc.Format() == Formats.TPC.TPCTextureFormat.DXT3 || tpc.Format() == Formats.TPC.TPCTextureFormat.DXT5)
                            {
                                tpc.Convert(Formats.TPC.TPCTextureFormat.RGBA);
                            }
                            newModule.SetData(newTextureName, ResourceType.TGA, Formats.TPC.TPCAuto.BytesTpc(tpc, ResourceType.TGA));
                        }
                        mdlData = ModelTools.ChangeTextures(mdlData, newTextures);
                    }

                    if (copyLightmaps)
                    {
                        foreach (string lightmap in ModelTools.IterateLightmaps(mdlData))
                        {
                            if (newLightmaps.ContainsKey(lightmap))
                            {
                                continue;
                            }
                            string newLightmapName = $"{identifier}_lm_{newLightmaps.Count}";
                            newLightmaps[lightmap] = newLightmapName;

                            var tpc = installation.Texture(
                                lightmap,
                                new[]
                                {
                                    SearchLocation.CHITIN,
                                    SearchLocation.OVERRIDE,
                                    SearchLocation.TEXTURES_GUI,
                                    SearchLocation.TEXTURES_TPA
                                });
                            if (tpc == null)
                            {
                                new Andastra.Parsing.Logger.RobustLogger().Warning($"TPC/TGA resource not found for lightmap '{lightmap}' in module '{root}'");
                                continue;
                            }
                            tpc = tpc.Copy();
                            if (tpc.Format() == Formats.TPC.TPCTextureFormat.BGR || tpc.Format() == Formats.TPC.TPCTextureFormat.DXT1 || tpc.Format() == Formats.TPC.TPCTextureFormat.Greyscale)
                            {
                                tpc.Convert(Formats.TPC.TPCTextureFormat.RGB);
                            }
                            else if (tpc.Format() == Formats.TPC.TPCTextureFormat.BGRA || tpc.Format() == Formats.TPC.TPCTextureFormat.DXT3 || tpc.Format() == Formats.TPC.TPCTextureFormat.DXT5)
                            {
                                tpc.Convert(Formats.TPC.TPCTextureFormat.RGBA);
                            }
                            newModule.SetData(newLightmapName, ResourceType.TGA, Formats.TPC.TPCAuto.BytesTpc(tpc));
                        }
                        mdlData = ModelTools.ChangeLightmaps(mdlData, newLightmaps);
                    }

                    mdlData = ModelTools.Rename(mdlData, newModelName);
                    newModule.SetData(newModelName, ResourceType.MDL, mdlData);
                    newModule.SetData(newModelName, ResourceType.MDX, mdxData);
                    newModule.SetData(newModelName, ResourceType.WOK, wokData);
                }
            }

            if (vis != null)
            {
                newModule.SetData(identifier, ResourceType.VIS, Formats.VIS.VISAuto.BytesVis(vis));
            }
            else
            {
                new Andastra.Parsing.Logger.RobustLogger().Warning($"No VIS found in module to be cloned: '{root}'");
            }

            if (lyt != null)
            {
                newModule.SetData(identifier, ResourceType.LYT, Andastra.Parsing.Resource.Formats.LYT.LYTAuto.BytesLyt(lyt));
            }
            else
            {
                new Andastra.Parsing.Logger.RobustLogger().Error($"No LYT found in module to be cloned: '{root}'");
            }

            string modulePath = installation.ModulePath();
            string outputPath = System.IO.Path.Combine(modulePath, $"{identifier}.mod");
            ERFAuto.WriteErf(newModule, outputPath, ResourceType.MOD);
        }
    }
}
