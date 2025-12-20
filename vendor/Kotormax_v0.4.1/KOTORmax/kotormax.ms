/*-----------------------------------------------------------------------------\

	KOTORmax
	by bead-v
	
	Derived from:
    NWmax
    by Joco (jameswalker@clear.net.nz)

    Credits:
    Wayland Reid :-
        Extends Wayland's (wreid@spectrumanalytic.com) Import/Export script
    Zaddix :-
        Based on code from Zaddix's original WOK file importer
    BioWare :-
        This code is based on the information gleaned from BioWares
        3DS Max scripts. In many cases the Bioware code is used instead of
        "re-inventing the wheel". I have attempted to note all those places
        where I have used the Bioware code.


    Legal Stuff
    1. This is free software and is provided with no explicit or implied
       warranty. If you use this software then you do so at your own risk.
    2. If there is any law in your country that requires the author to provide
       any form of support or indemnity on the use of the this software you are
       not therefore authorised to use said software as no such indemnity or
       support will be provided (per 1) other than at the authors discretion.
    3. Credit has been given where code or methods have been integrated from
       other sources.
    4. In the case of code used from the Bioware scripts their intellectual
       property rights still hold.
    5. No code has been knowingly included from other sources where that code
       is sold for commercial purposes.




   Updates:
   .8b61    Fix for global sanity check issues.
            Enhanced wok checker tool

   .8b22    Fix for fadinglight token not being parsed
            Fix for wok test not out putting to sanity check file
            Support for wok material file being fully driven from .ini file
            A number of misc bug fixes that I have lost track of.
\-----------------------------------------------------------------------------*/

-- Ensure stack and heap size is large enough
--if ( heapSize < 7500000 ) then heapSize = 7500000
--if ( stackLimit < 4000000 ) then stackLimit = 4000000
---- following NWmax 0.8c, stack and heap size should be higher
if ( heapSize < 80000000 ) then heapSize = 80000000
if ( stackLimit < 80000000 ) then stackLimit = 80000000

global gkx_maxver = (maxversion())[1]
global gblVersion = "0.4.1"
global g_kotormaxPath = scriptsPath + "KOTORmax\\"
global kotormax_handle -- handle to the master floater window
if autostart == undefined then autostart = false

ClearListener()
format "--------------------------------------------------------------\r\n"
format "\r\n"
format "                        KOTORmax v%\r\n" gblVersion
format "\r\n"
format "--------------------------------------------------------------\r\n"

--------------------------------------------------------------------------------
---
--- Global Variables and init setups
---
--------------------------------------------------------------------------------
global DEBUG = false	-- debug flag
global SPEEDT = false	-- speed timer flag
global strIndent1 = "  "
global strIndent2 = "    "
global strIndent3 = "      "
global strIndent4 = "        "
global strIndent5 = "          "
--
-- Load kotormax ini global vars
global g_3dsmax = getINISetting (g_kotormaxPath + "kotormax.ini") "init" "usemax"
global g_verbose = getINISetting (g_kotormaxPath + "kotormax.ini") "init" "verbose"
global gkx_dock = getINISetting (g_kotormaxPath + "kotormax.ini") "init" "dock"
global gkx_visible = getINISetting (g_kotormaxPath + "kotormax.ini") "init" "visible"
global gkx_sanity = getINISetting (g_kotormaxPath + "kotormax.ini") "init" "sanity"
global g_basepointer = getINISetting (g_kotormaxPath + "kotormax.ini") "init" "basepointer"
global g_exportwok = getINISetting (g_kotormaxPath + "kotormax.ini") "init" "exportwok"
global g_exportcolors = getINISetting (g_kotormaxPath + "kotormax.ini") "init" "exportcolors"
global g_dotAscii = getINISetting (g_kotormaxPath + "kotormax.ini") "init" "dotascii"
global g_expLower = getINISetting (g_kotormaxPath + "kotormax.ini") "init" "lowercase"
global gkx_pos_kotormax = getINISetting (g_kotormaxPath + "kotormax.ini") "init" "position"
global g_exportPath = getINISetting (g_kotormaxPath + "kotormax.ini") "init" "lastexport"
format "KOTORmax.ini loaded\r\n"

-- Correct ini vars for undefined and set required defaults and type conversion
format "Correct ini vars\r\n"
if g_verbose == undefined then g_verbose = 0
else g_verbose = (g_verbose as integer)
   
if gkx_dock == undefined then gkx_dock = 0
else gkx_dock = (gkx_dock as integer)
	
if gkx_visible == undefined then gkx_visible = 0
else gkx_visible = (gkx_visible as integer)
	
if gkx_sanity == undefined or gkx_sanity == "" then gkx_sanity = "111110111111111"
	
if g_basepointer == undefined then g_basepointer = 1
else g_basepointer = (g_basepointer as integer)

if g_exportwok == undefined then g_exportwok = 1
else g_exportwok = (g_exportwok as integer)

if g_exportcolors == undefined then g_exportcolors = 0
else g_exportcolors = (g_exportcolors as integer)

if g_dotAscii == undefined then g_dotAscii = 1
else g_dotAscii = (g_dotAscii as integer)

if g_expLower == undefined then g_expLower = 1
else g_expLower = (g_expLower as integer)

if gkx_pos_kotormax == undefined or (gkx_pos_kotormax == "") then gkx_pos_kotormax = [0,100]
else gkx_pos_kotormax = execute(gkx_pos_kotormax)

if g_exportPath == undefined or g_exportPath == "" then g_exportPath = GetDir #export

-- set globals for 3dsmax vs gmax
format "Setting globals for 3dsmax vs gmax...\r\n"
global g_fextn = ""
global g_gmaxini = ""
global g_ismax = false
global g_maxVer = (maxversion())[1]
if g_3dsmax == "1" or g_maxVer > 4200 then (
   g_fextn = ".max"
   g_gmaxini = "3dsmax.ini"
   g_ismax = true
) else (
   g_fextn = ".gmax"
   g_gmaxini = "gmax.ini"
   g_ismax = false
)

-- load in all paths from gmax.ini or max file
global g_ini_location = getDir #maxroot
if g_maxVer >= 9000 then g_ini_location = getDir #maxdata
if g_ini_location[g_ini_location.count] != "\\" then g_ini_location += "\\"
format "Loading in all paths from %%...\r\n" g_ini_location g_gmaxini
global g_maps_path = #()
for i = 1 to 100 do
(
	if (getINISetting (g_ini_location+g_gmaxini) "BitmapDirs" ("Dir"+(i as string))) == "" then exit
	append g_maps_path (getINISetting (g_ini_location+g_gmaxini) "BitmapDirs" ("Dir"+(i as string)))
)
format "Maps search dirs:\r\n"
print g_maps_path

-- Global stack to be used for files and generic stack routines
global g_FileHandles = #()

-- Exporting stream buffer
format "Setup exporting stream\r\n"
if g_ismax then
(
   global g_strBuffer = undefined	-- file stream buffer for exporting
   global g_delim = "\n"	-- Buffered export system delimter
) 
else
(
	global g_strBuffer = stringStream ""	-- StringStream buffer for compressed export system
	if g_verbose == 1 then
		g_delim = "~\n"
	else
		g_delim = "~"
)

-- Compiler
--global g_compiler = g_kotormaxPath + "mdlcomp\\nwnmdlcomp.exe"

-- Plug-Ins
global g_plugins = #()  -- list of plugins

-- Specific import globals
global lastDanglyName = undefined
global AnimRootNodeName = undefined
global LastAnimNumber = undefined
global LastAnimEventNumber = undefined

-- UI flags for active dialogs
global kx_animkeymaster_flag = false
global kx_skindumper_flag = false

-- global execute str - execution requires global scope, so this has to be preserved
global kx_exec_str

-- These are for the Sanity Check Preferences
-- Originally per Bioware's export scripts code. Legacy system.
format "Set Sanity check default settings\r\n"
global runRotationCheck = (gkx_sanity[1] as integer != 0)
global runAnimCheck = (gkx_sanity[2] as integer != 0)
global runNameCheck = (gkx_sanity[3] as integer != 0)
global runScaleCheck = (gkx_sanity[4] as integer != 0)
global runPartTextureCheck = (gkx_sanity[5] as integer != 0)
global runWeldCheck = (gkx_sanity[6] as integer != 0)
global runClassCheck = (gkx_sanity[7] as integer != 0)
global runControllerCheck = (gkx_sanity[8] as integer != 0)
global runDoganCheck = (gkx_sanity[9] as integer != 0)
global runBitmapNameCheck = (gkx_sanity[10] as integer != 0)
global runFaceCheck = (gkx_sanity[11] as integer != 0)
global runAnimrootCheck = (gkx_sanity[12] as integer != 0)
global runBoneCountCheck = (gkx_sanity[13] as integer != 0)
global runDuplicateNamesCheck = (gkx_sanity[14] as integer != 0)
global runAabbCheck = (gkx_sanity[15] as integer != 0)

-- this function might work well for testing
fn kx_coderefresh =
(
	filein (g_kotormaxPath+"kotormax_scripts/odyssey_fn_general.ms")
	filein (g_kotormaxPath+"kotormax_scripts/odyssey_fn_sanity.ms")
	filein (g_kotormaxPath+"kotormax_scripts/odyssey_fn_export.ms")
	filein (g_kotormaxPath+"kotormax_scripts/odyssey_fn_import.ms")
	filein (g_kotormaxPath+"kotormax_scripts/kx_gui_create.ms")
	filein (g_kotormaxPath+"kotormax_scripts/kx_gui_speedbtns.ms")
	filein (g_kotormaxPath+"kotormax_scripts/kx_gui_import.ms")
	filein (g_kotormaxPath+"kotormax_scripts/kx_gui_about.ms")
	max views redraw
)

-- Map channels
global kx_colorChannel = 0
global kx_diffuseChannel = 1
global kx_ambientChannel = 2
global kx_texture0Channel = 3
global kx_texture1Channel = 4
global kx_constraintChannel = 5
global kx_roomlinkChannel = 6

--
-- Start progress for loading
--
progressStart "PlugIns Loading ..."
global com_load_total = 9 -- setting progress 
global com_load = 0 -- tracking progress

--------------------------------------------------------------------------------
--
-- General functions
--
--------------------------------------------------------------------------------
filein (g_kotormaxPath+"kotormax_scripts/odyssey_fn_proto.ms")
filein (g_kotormaxPath+"kotormax_scripts/odyssey_fn_general.ms")

--------------------------------------------------------------------------------
---
--- Scripted Plug-Ins
---
--------------------------------------------------------------------------------
filein (g_kotormaxPath+"kotormax_scripts/odyssey_helper_dummy.ms")    -- 1
filein (g_kotormaxPath+"kotormax_scripts/odyssey_helper_base.ms")     -- 2
filein (g_kotormaxPath+"kotormax_scripts/odyssey_helper_light.ms")    -- 3
filein (g_kotormaxPath+"kotormax_scripts/odyssey_helper_emitter.ms")  -- 4
filein (g_kotormaxPath+"kotormax_scripts/odyssey_helper_reference.ms")-- 5
filein (g_kotormaxPath+"kotormax_scripts/odyssey_mod_trimesh.ms")     -- 6
filein (g_kotormaxPath+"kotormax_scripts/odyssey_mod_flex.ms")        -- 7
filein (g_kotormaxPath+"kotormax_scripts/odyssey_mod_walkmesh.ms")    -- 8
filein (g_kotormaxPath+"kotormax_scripts/odyssey_geom_lightsaber.ms") -- 9

--------------------------------------------------------------------------------
--
-- Import/Export functions
--
--------------------------------------------------------------------------------
filein (g_kotormaxPath+"kotormax_scripts/odyssey_fn_sanity.ms")
filein (g_kotormaxPath+"kotormax_scripts/odyssey_fn_export.ms")
filein (g_kotormaxPath+"kotormax_scripts/odyssey_fn_import.ms")

--------------------------------------------------------------------------------
---
---   Rollout definitions
---
--------------------------------------------------------------------------------
filein (g_kotormaxPath+"kotormax_scripts/kx_gui_create.ms")
filein (g_kotormaxPath+"kotormax_scripts/kx_gui_speedbtns.ms")
filein (g_kotormaxPath+"kotormax_scripts/kx_gui_import.ms")
filein (g_kotormaxPath+"kotormax_scripts/kx_gui_about.ms")


--------------------------------------------------------------------------------
---
---   Rollout Window creation
---
--------------------------------------------------------------------------------

fn kx_make_floater =
(
	kotormax_handle = newRolloutFloater "KOTORmax" 200 600 0 100 -- 192 600 0 100
	addRollout kx_util0			kotormax_handle rolledUp:true
	addRollout kx_util1			kotormax_handle rolledUp:true
	--addRollout kx_util2			kotormax_handle rolledUp:true
	addRollout kx_util3			kotormax_handle rolledUp:true
	addRollout kx_util4			kotormax_handle rolledUp:true
	--addRollout kx_util5			kotormax_handle rolledUp:true
	addRollout kx_util6			kotormax_handle rolledUp:true
	addRollout ImportRollout    kotormax_handle rolledUp:true
	addRollout kx_util_export   kotormax_handle rolledUp:true
	addRollout kx_prefs			kotormax_handle rolledUp:true
	addRollout AboutRollout     kotormax_handle rolledUp:false

	-- load in old position from local ini file
	kotormax_handle.pos = gkx_pos_kotormax
)

fn kx_destroy_floater =
(
	if kotormax_handle != undefined do
	(
	    -- make sure floater is not docked and unregister it
	    try (
	        cui.FloatDialogBar kotormax_handle
	        cui.UnRegisterDialogBar kotormax_handle
	    ) catch ()
	    -- close it.
	    closerolloutfloater kotormax_handle
	)
)

-- try to close out a floater if it already exists
kx_destroy_floater()

-- CREATE KOTORMAX WINDOW
if gkx_visible == 1 then
(
	kx_make_floater()
	if gkx_dock == 1 then
	(
		-- Register floater as a dockable bar
		cui.RegisterDialogBar kotormax_handle \
		    maxSize:[-1,-1] \
		    style:#(#cui_dock_vert, #cui_floatable, #cui_handles)
		-- By default dock the bar
		cui.DockDialogBar kotormax_handle #cui_dock_left
	)
	updateGameUnitsButton()
)

-- Set app title
--cui.setAppTitle ("KOTORmax v" + gblVersion)

if IsSceneRedrawDisabled()==true do enablesceneredraw()

format "------------------------------Done----------------------------\r\n\r\n"
progressEnd()
max views redraw
