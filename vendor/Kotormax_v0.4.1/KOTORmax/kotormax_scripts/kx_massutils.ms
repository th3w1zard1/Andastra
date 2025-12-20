/*-----------------------------------------------------------------------------\

    Mass Action Utilities

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



--------------------------------------------------------------------------------
-- Mass operations UI
rollout kx_massutils "Mass Operations" width:250
(
	group "Mass Sanity Check"
	(
		-- massmdlsanity pattern:"d:\\mymdlfiles\\*.mdl"
		edittext edt_sanitypattern "File Pattern:"
		button btn_massmdlsanity "Mass Sanity Check"
	)
	
	group "Mass Load and Export"
	(
		edittext edt_source "Source:"
		edittext edt_target "Target:"
		checkbox chk_anims "Include Anims" checked:true
		button btn_massload "Load" across:2 tooltip:"For every .mdl that matches the pattern, import it and save the max scene under \"target\modelname\""
		button btn_massexport "Export" tooltip:"For every max file that matches the pattern, load it and export the .mdl under \"target\modelname\""
	)
	
	on kx_massutils open do
	(
		-- load up any saved path values
		local sanity = getINISetting (g_kotormaxPath + "kotormax.ini") "massutils" "mass_sanity_pattern"
		local src = getINISetting (g_kotormaxPath + "kotormax.ini") "massutils" "mass_source_pattern"
		local trgt = getINISetting (g_kotormaxPath + "kotormax.ini") "massutils" "mass_target_path"
		
		if sanity != undefined then edt_sanitypattern.text = sanity
		if src != undefined then edt_source.text = src
		if trgt != undefined then edt_target.text = trgt
	)
	
	on kx_massutils close do
	(
		local datalist = #()
		
		-- Each sub array contains the section, atrrib, vlaue, file
		append datalist #("massutils", "mass_sanity_pattern", "\""+edt_sanitypattern.text+"\"", (g_kotormaxPath + "kotormax.ini"))
		append datalist #("massutils", "mass_source_pattern", "\""+edt_source.text+"\"", (g_kotormaxPath + "kotormax.ini"))
		append datalist #("massutils", "mass_target_path", "\""+edt_target.text+"\"", (g_kotormaxPath + "kotormax.ini"))
		
		-- save field values for later re use
		kx_setinivalue datalist
	)
	
	on btn_massmdlsanity pressed do
	(
		massmdlsanity pattern:(edt_sanitypattern.text)
	)
	
	on btn_massload pressed do
	(
		massload pattern:(edt_source.text) target:(edt_target.text) anims:(chk_anims.checked)
	)
	
	on btn_massexport pressed do
	(
		massexport pattern:(edt_source.text) target:(edt_target.text) anims:(chk_anims.checked)
	)
)

createDialog kx_massutils