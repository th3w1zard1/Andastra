/*-----------------------------------------------------------------------------\

    Function Prototypes

	KOTORmax
	by bead-v
	
	Derived from:
    NWmax
    by Joco (jameswalker@clear.net.nz)

    Credits:
    Wayland Reid :-
        Extends Wayland's Import/Export script with rewrites as needed
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
    4. No code has been knowingly included from other sources where that code
       is sold for commercial purposes.

\-----------------------------------------------------------------------------*/

/*
	LIST OF GLOBALS IN KOTORMAX.MS
(kotormax.ms only) gkx_maxver - version of max
	gblVersion - version of KOTORmax
	DEBUG - flag whether to print all sorts of debug messages during processing
	SPEEDT - "speed timer flag"
	strIndent[1-5] - increasing indent

	FLAGS
	1. defined first in "KOTORmax/kotormax.ini", then overriden by "nxlocalprefs.ini"
	gkx_dock - integer, dock on startup, default is 0
	g_basepointer - integer, use base pointer, default is 1
(kotormax.ms only) g_guiAnimListSize - integer size, default is 10
(kotormax.ms only) g_verbose - integer, in gmax use new lines when printing to listener, default is 0
(kotormax.ms only) gkx_visible - integer, make the floater, default is 0
(kotormax.ms only) gkx_sanity - string of 0|1, which sanity tests to apply, default is "11000111110111111111"
(kotormax.ms only) g_3dsmax - this is just a transitional variable, see below
	2. defined in "nxlocalprefs.ini" only
(kotormax.ms only) gkx_pos_kotormax - point2 position of kotormax on screen, default is [0, 100]

	3dsmax vs gmax variables
(kotormax.ms only) g_gmaxini - name of ini file ("3dsmax.ini" for 3ds, "gmax.ini" for gmax)
(kotormax.ms only) g_fextn - max save file extension (.max for 3ds, .gmax for gmax)
	g_ismax - boolean, true if 3ds, false if gmax.

	g_maps_path - array of paths loaded from the g_gmaxini, the importer looks for textures there if it doesn't find any locally
	g_strBuffer - a stringstream that gets dumped in gmax or a filestream in max
	g_delim - the delimiter "\n" in max, "~" in non-verbose gmax, "~\n" if verbose
	          I assume snoop inserts a new line for ~.
			  Verbose would then just be for a nicer display in the Listener.

	g_kotormaxPath - this is the KOTORmax root path.
	--g_compiler - compiler, but unnecessary for us at this point
	g_plugins
	g_exportPath - set to ""
	kotormax_handle - kotormax window

*/


--
-- Undefined Function Alert
--
function Alert = messageBox "This function has not been implemented!"

--------------------------------------------------------------------------------
-- Function Prototypes
--------------------------------------------------------------------------------
fn ShowDanglyConstraints node onoff = (Alert)
fn dump node = (Alert)
fn kx_progressPlus = (Alert)
fn kx_MakeUnqiueNodeName strName = (Alert)
fn kx_round x = (Alert)
fn kx_lowercase instring = (Alert)
fn kx_existFile fname = (Alert)
fn FindAnimByName lookup_name = (Alert)
fn FindFreeAnimNumber = (Alert)
fn FindFreeEventNumber = (Alert)
fn ShowDanglyConstraints node onoff = (Alert)
fn kx_UpdateMaterialEditor MatLib = (Alert)
fn kx_createWalkMaterial = (Alert)
fn selChanged = (Alert)
fn kx_roundp3 = (Alert)
fn kx_deletekeysinblock = (Alert)

--------------------------------------------------------------------------------
-- Import Functions
--------------------------------------------------------------------------------
global addMDLSkin = (Alert)
global ImportOdysseyModel = (Alert)
global massload = (Alert)

--------------------------------------------------------------------------------
-- Export Functions
--------------------------------------------------------------------------------
global ExportOdysseyModel = (Alert)
global SanityCheck = (Alert)
global massexport = (Alert)
-- utility functions
global kx_fprints = (Alert)
global kx_strclassof = (Alert)
global kx_materialdiffusebitmapname = (Alert)
global kx_materialambientbitmapname = (Alert)


--------------------------------------------------------------------------------
-- Rollouts
--------------------------------------------------------------------------------
global ImportRollout = (Alert)
