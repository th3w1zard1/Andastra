using System;
using System.IO;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;

namespace Andastra.Parsing.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/heuristics.py:12-227
    // Original: def determine_game(path: os.PathLike | str) -> Game | None:
    /// <summary>
    /// Determines the game based on files and folders.
    /// </summary>
    public static class Heuristics
    {
        /// <summary>
        /// Determines the game based on files and folders.
        /// </summary>
        /// <param name="path">Path to game directory</param>
        /// <returns>Game enum or null</returns>
        /// <remarks>
        /// References:
        /// - vendor/KOTOR_Registry_Install_Path_Editor (Registry path detection)
        /// - vendor/HoloPatcher.NET/src/HoloPatcher/Utils (Game detection logic)
        /// Note: File and folder heuristics vary between Steam, GOG, and disc releases
        ///
        /// Processing Logic:
        /// 1. Normalize the path and check for existence of game files
        /// 2. Define checks for each game
        /// 3. Run checks and score games
        /// 4. Return game with highest score or None if scores are equal or all checks fail
        /// </remarks>
        public static BioWareGame? DetermineGame(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            string rPath = Path.GetFullPath(path);

            bool Check(string fileName)
            {
                string checkPath = Path.Combine(rPath, fileName);
                return File.Exists(checkPath) || Directory.Exists(checkPath);
            }

            // Checks for each game
            bool[] gaPcChecks = new[]
            {
                Check("streamwaves"),
                Check("swkotor.exe"),
                Check("swkotor.ini"),
                Check("rims"),
                Check("utils"),
                Check("32370_install.vdf"),
                Check("miles/mssds3d.m3d"),
                Check("miles/msssoft.m3d"),
                Check("data/party.bif"),
                Check("data/player.bif"),
                Check("modules/global.mod"),
                Check("modules/legal.mod"),
                Check("modules/mainmenu.mod"),
            };

            bool[] gaXboxChecks = new[]
            {
                Check("01_SS_Repair01.ini"),
                Check("swpatch.ini"),
                Check("dataxbox/_newbif.bif"),
                Check("rimsxbox"),
                Check("players.erf"),
                Check("downloader.xbe"),
                Check("rimsxbox/manm28ad_adx.rim"),
                Check("rimsxbox/miniglobal.rim"),
                Check("rimsxbox/miniglobaldx.rim"),
                Check("rimsxbox/STUNT_56a_a.rim"),
                Check("rimsxbox/STUNT_56a_adx.rim"),
                Check("rimsxbox/STUNT_57_adx.rim"),
                Check("rimsxbox/subglobal.rim"),
                Check("rimsxbox/subglobaldx.rim"),
                Check("rimsxbox/unk_m44ac_adx.rim"),
                Check("rimsxbox/M12ab_adx.rim"),
                Check("rimsxbox/mainmenu.rim"),
                Check("rimsxbox/mainmenudx.rim"),
                Check("rimsxbox/manm28ad_adx.rim"),
            };

            bool[] gaIosChecks = new[]
            {
                Check("override/ios_action_bg.tga"),
                Check("override/ios_action_bg2.tga"),
                Check("override/ios_action_x.tga"),
                Check("override/ios_action_x2.tga"),
                Check("override/ios_button_a.tga"),
                Check("override/ios_button_x.tga"),
                Check("override/ios_button_y.tga"),
                Check("override/ios_edit_box.tga"),
                Check("override/ios_enemy_plus.tga"),
                Check("override/ios_gpad_bg.tga"),
                Check("override/ios_gpad_gen.tga"),
                Check("override/ios_gpad_gen2.tga"),
                Check("override/ios_gpad_help.tga"),
                Check("override/ios_gpad_help2.tga"),
                Check("override/ios_gpad_map.tga"),
                Check("override/ios_gpad_map2.tga"),
                Check("override/ios_gpad_save.tga"),
                Check("override/ios_gpad_save2.tga"),
                Check("override/ios_gpad_solo.tga"),
                Check("override/ios_gpad_solo2.tga"),
                Check("override/ios_gpad_solox.tga"),
                Check("override/ios_gpad_solox2.tga"),
                Check("override/ios_gpad_ste.tga"),
                Check("override/ios_gpad_ste2.tga"),
                Check("override/ios_gpad_ste3.tga"),
                Check("override/ios_help.tga"),
                Check("override/ios_help2.tga"),
                Check("override/ios_help_1.tga"),
                Check("KOTOR"),
                Check("KOTOR.entitlements"),
                Check("kotorios-Info.plist"),
                Check("AppIcon29x29.png"),
                Check("AppIcon50x50@2x~ipad.png"),
                Check("AppIcon50x50~ipad.png"),
            };

            bool[] gaK2PcChecks = new[]
            {
                Check("streamvoice"),
                Check("swkotor2.exe"),
                Check("swkotor2.ini"),
                Check("LocalVault"),
                Check("LocalVault/test.bic"),
                Check("LocalVault/testold.bic"),
                Check("miles/binkawin.asi"),
                Check("miles/mssds3d.flt"),
                Check("miles/mssdolby.flt"),
                Check("miles/mssogg.asi"),
                Check("data/Dialogs.bif"),
            };

            bool[] gaK2XboxChecks = new[]
            {
                Check("combat.erf"),
                Check("effects.erf"),
                Check("footsteps.erf"),
                Check("footsteps.rim"),
                Check("SWRC"),
                Check("weapons.ERF"),
                Check("SuperModels/smseta.erf"),
                Check("SuperModels/smsetb.erf"),
                Check("SuperModels/smsetc.erf"),
                Check("SWRC/System/Subtitles_Epilogue.int"),
                Check("SWRC/System/Subtitles_YYY_06.int"),
                Check("SWRC/System/SWRepublicCommando.int"),
                Check("SWRC/System/System.ini"),
                Check("SWRC/System/UDebugMenu.u"),
                Check("SWRC/System/UnrealEd.int"),
                Check("SWRC/System/UnrealEd.u"),
                Check("SWRC/System/User.ini"),
                Check("SWRC/System/UWeb.int"),
                Check("SWRC/System/Window.int"),
                Check("SWRC/System/WinDrv.int"),
                Check("SWRC/System/Xbox"),
                Check("SWRC/System/XboxLive.int"),
                Check("SWRC/System/XGame.u"),
                Check("SWRC/System/XGameList.int"),
                Check("SWRC/System/XGames.int"),
                Check("SWRC/System/XInterface.u"),
                Check("SWRC/System/XInterfaceMP.u"),
                Check("SWRC/System/XMapList.int"),
                Check("SWRC/System/XMaps.int"),
                Check("SWRC/System/YYY_TitleCard.int"),
                Check("SWRC/System/Xbox/Engine.int"),
                Check("SWRC/System/Xbox/XboxLive.int"),
                Check("SWRC/Textures/GUIContent.utx"),
            };

            bool[] gaK2IosChecks = new[]
            {
                Check("override/ios_mfi_deu.tga"),
                Check("override/ios_mfi_eng.tga"),
                Check("override/ios_mfi_esp.tga"),
                Check("override/ios_mfi_fre.tga"),
                Check("override/ios_mfi_ita.tga"),
                Check("override/ios_self_box_r.tga"),
                Check("override/ios_self_expand2.tga"),
                Check("override/ipho_forfeit.tga"),
                Check("override/ipho_forfeit2.tga"),
                Check("override/kotor2logon.tga"),
                Check("override/lbl_miscroll_open_f.tga"),
                Check("override/lbl_miscroll_open_f2.tga"),
                Check("override/ydialog.gui"),
                Check("KOTOR II"),
                Check("KOTOR2-Icon-20-Apple.png"),
                Check("KOTOR2-Icon-29-Apple.png"),
                Check("KOTOR2-Icon-40-Apple.png"),
                Check("KOTOR2-Icon-58-apple.png"),
                Check("KOTOR2-Icon-60-apple.png"),
                Check("KOTOR2-Icon-76-apple.png"),
                Check("KOTOR2-Icon-80-apple.png"),
                Check("KOTOR2_LaunchScreen.storyboardc"),
                Check("KOTOR2_LaunchScreen.storyboardc/Info.plist"),
                Check("GoogleService-Info.plist"),
            };

            // Android checks not yet implemented - requires Android-specific file path knowledge
            bool[] gaAndroidChecks = new bool[0];

            // Determine the game with the most checks passed
            int gaScore = gaPcChecks.Count(x => x);
            int gaScore2 = gaK2PcChecks.Count(x => x);
            int gaXboxScore = gaXboxChecks.Count(x => x);
            int gaXboxScore2 = gaK2XboxChecks.Count(x => x);
            int gaIosScore = gaIosChecks.Count(x => x);
            int gaIosScore2 = gaK2IosChecks.Count(x => x);
            int gaAndroidScore = gaAndroidChecks.Count(x => x);

            BioWareGame? highestScoringGame = null;
            int highestScore = 0;

            if (gaScore > highestScore)
            {
                highestScore = gaScore;
                highestScoringGame = BioWareGame.K1;
            }

            if (gaScore2 > highestScore)
            {
                highestScore = gaScore2;
                highestScoringGame = BioWareGame.K2;
            }

            if (gaXboxScore > highestScore)
            {
                highestScore = gaXboxScore;
                highestScoringGame = BioWareGame.K1_XBOX;
            }

            if (gaXboxScore2 > highestScore)
            {
                highestScore = gaXboxScore2;
                highestScoringGame = BioWareGame.K2_XBOX;
            }

            if (gaIosScore > highestScore)
            {
                highestScore = gaIosScore;
                highestScoringGame = BioWareGame.K1_IOS;
            }

            if (gaIosScore2 > highestScore)
            {
                highestScore = gaIosScore2;
                highestScoringGame = BioWareGame.K2_IOS;
            }

            if (gaAndroidScore > highestScore)
            {
                highestScore = gaAndroidScore;
                highestScoringGame = BioWareGame.K1_ANDROID;
            }

            if (gaAndroidScore > highestScore)
            {
                highestScore = gaAndroidScore;
                highestScoringGame = BioWareGame.K2_ANDROID;
            }

            return highestScoringGame;
        }
    }
}
