--------------------------------------------------------------------------------
--                                                                            --
-- KOTORmax                                                                   --
-- by bead-v                                                                  --
--                                                                            --
-- Originally made for:                                                       --
-- NWN mdl Import/Export utility for GMax, v0.3b                              --
-- by Wayland (wreid@spectrumanalytic.com)                                    --
--                                                                            --
-- (c) Wayland Reid                                                           --
-- Based on Zaddix's original WOK file importer maxscript                     --
-- Based on code by (c) Copyright Zaddix, 2002                                --
--                                                                            --
-- Use this code in any way you see fit.                                      --
-- If you use any/all of the code in your own scripts,                        --
-- just credit me and Zaddix somewhere.                                       --
--                                                                            --
--------------------------------------------------------------------------------



----------------------------------------------------------------------------
----------------------------------------------------------------------------
---
--- ImportFile routines
---
----------------------------------------------------------------------------
----------------------------------------------------------------------------

-- import function for rollout and supporting utils
fn kx_importer =
(
	local bDoAnims = true
	local bDoGeom = true
	local targetAnimBase = undefined
    local mdlFileName = ImportRollout.import_filename_edit.text
    local animname	= undefined -- for loading single anims
    local insertpoint = 10

    case ImportRollout.rdo_options.state of
    (
	    1: ( bDoGeom=true; bDoAnims=true; )
	    2: ( bDoGeom=true; bDoAnims=false; )
	    3: (
				bDoGeom=false; bDoAnims=true;

                local base = undefined
                local basecount = 0
                for o in selection do
                (
                    if (iskindof o odysseybase) then
                    (
                        basecount += 1
                        base = o
                    )
                )
                if basecount == 0 then
                (
                    -- no model base was selected so see if only 1 in the scene.
                    -- If there is only 1 then select it
                    for o in $objects do
                    (
                        if (iskindof o odysseybase) then
                        (
                            basecount += 1
                            base = o
                        )
                    )
                )
                if basecount == 1 then (
                    targetAnimBase = base
                )

                if(targetAnimBase != undefined) then
                (
                    -- find the largets frame ref on model base
                    local largestframe = 0
                    for an in base.animations do
                    (
                        tempstr = filterString an " "
                        if (tempstr.count > 3) and ((tempstr[3] as integer) > largestframe) then
                        (
                            largestframe = tempstr[3] as integer
                        )
                    )
                    insertpoint = largestframe + 10
                )

				if ImportRollout.edt_animname.text.count > 0 then
				(
					animname = filterstring (ImportRollout.edt_animname.text) ", "
	    	        --append animname (ImportRollout.edt_animname.text)
    	        )

    	        if ImportRollout.spn_startframe.value > -1 then(
        	        insertpoint = ImportRollout.spn_startframe.value + 10
    	        )

	            -- check to make sure we have only a model base selected
	            if targetAnimBase == undefined then
	            (
    	            MessageBox "No target model odysseybase selected for Animation Import" title:"No Selection Found"
    	            return 0
	            )
	       )
    )

    if (not kx_existFile mdlFileName) then
    (
        MessageBox ("Model not imported.  " + mdlFileName + " does not exist.") title:"File does not exist"
    )
    else
    (
        clearListener()
        disableSceneRedraw()
		local StrictFilename = mdlFileName
		while matchPattern StrictFilename pattern:"*.*" do StrictFilename = getFilenameFile  StrictFilename
        local pwk_file = (getFilenamePath mdlFileName)+StrictFilename+".pwk.ascii"
		if (not kx_existFile pwk_file) then pwk_file = (getFilenamePath mdlFileName)+StrictFilename+".pwk"
        local dwk_file = (getFilenamePath mdlFileName)+StrictFilename+".dwk.ascii"
		if (not kx_existFile dwk_file) then dwk_file = (getFilenamePath mdlFileName)+StrictFilename+".dwk"

        -- import MDL file
        ImportOdysseyModel \
            mdlFileName \                            -- filename
            bDoAnims \                               -- import anims
            false \ --ImportRollout.warn_checkbox.checked \    -- show warnings
            loadGeom:bDoGeom \                       -- import geometry
            destinationBase:targetAnimBase \         -- target odysseybase (if importing anims only)
            loadanimlist:animname \                  -- specific animation to import
            insertpoint:insertpoint \                                                       -- where to insert the new animation.
            showprogress:true \                                                             -- show progress
            location:[ImportRollout.spn_loc_x.value, ImportRollout.spn_loc_y.value, 0] \    -- import location
            uselytposition:ImportRollout.chk_uselyt.checked \                               -- use layout position
            progressbar:ImportProgress

        -- import PWK file if available
        if (kx_existFile pwk_file) then
            ImportOdysseyWalkmesh pwk_file "pwk"

        -- import DWK file if available
        if (kx_existFile dwk_file) then
            ImportOdysseyWalkmesh dwk_file "dwk"
			
        enableSceneRedraw()

        -- Redraw gmax viewports
        clearSelection()
        max tool zoomextents all
        max views redraw

        gc()

		/*
        if ImportRollout.chk_resetctrl.checked then
        (
			local objSum = $objects.count * 2.0
			local cnt = 0.0
			progressStart "Resetting Orientation"
			format "Setting to tcb rotation\r\n"
			for o in $objects do
			(
				format "Setting to tcb rotation for %\r\n" o.name
				--/*
				if hasProperty o "rotation" then
				(
					local CurrentController
					try( CurrentController = o.rotation.controller )
					catch( CurrentController = undefined )
					o.position.controller = tcb_position()
					if CurrentController != undefined then
						o.rotation.controller = tcb_rotation()
				)
				--* /
				cnt += 1.0
				if (progressUpdate (((cnt*100)/objSum) as integer)) == false then exit
			)
			format "Setting back to linear rotation\r\n"
			for o in $objects do
			(
				format "Setting back to linear rotation for %\r\n" o.name
				--/*
				if hasProperty o "rotation" then
				(
					local CurrentController
					try( CurrentController = o.rotation.controller )
					catch( CurrentController = undefined )
					o.position.controller = linear_position()
					if CurrentController != undefined then
						o.rotation.controller = linear_rotation()
				)
				--* /
				cnt += 1.0
				if (progressUpdate (((cnt*100)/objSum) as integer)) == false then exit
			)
			progressEnd()
        )
		*/
    )
)

rollout ImportRollout "MDL Loading" width:162 height:250
(
	Group "Filename"
	(
		edittext import_filename_edit "" height:16 --width:128
		button import_browse_button "Browse" width:70 height:24
	)
	Group "Options"
	(
		radioButtons rdo_options "" width:123 height:48 labels:#("Import Geom+Anims", "Import Geom Only", "Import Anims Only") default:1
		label lbl_animname "Anim names to load:" enabled:false
		edittext edt_animname enabled:false
		spinner spn_startframe "Insert Frame:" type:#integer range:[-1,100000,-1] enabled:false
		--checkbox chk_resetctrl "Reset Orientation" checked:false --enabled:false
		--checkbox warn_checkbox "Show warnings" width:128 height:16
		checkbox chk_uselyt "Use Layout Position" checked:true
		spinner spn_loc_x "Import Loc X:" range:[0,1000000,0] enabled:false
		spinner spn_loc_y "Import Loc Y:" range:[0,1000000,0] enabled:false
	)
	button import_button "Import" width:144 height:24

	on chk_uselyt changed val do
	(
        if(val) then val = false
        else val = true
		spn_loc_x.enabled = val
		spn_loc_y.enabled = val
	)

	on rdo_options changed val do
	(
		if val == 3 then (
			edt_animname.enabled = true
			lbl_animname.enabled = true
			spn_startframe.enabled = true
		)
		else (
			edt_animname.enabled = false
			lbl_animname.enabled = false
			spn_startframe.enabled = false
		)
	)

	on import_browse_button pressed do
	(
		local sPattern = "Odyssey Ascii Model (*.mdl)|*.mdl|Odyssey Ascii Model (*.mdl.ascii)|*.mdl.ascii|"
		if kx_itob g_dotAscii then sPattern = "Odyssey Ascii Model (*.mdl.ascii)|*.mdl.ascii|Odyssey Ascii Model (*.mdl)|*.mdl|"
		
	    local filename = getOpenFileName caption:"Import mdl" types:sPattern
	    if (filename == undefined) then filename = ""
	    import_filename_edit.text = filename
	)

	on import_button pressed do
	(
		kx_importer()
	)
)

rollout kx_util6 "Area Tools" width:162 height:68
(
    -- ui
    button btn_minimap "MiniMap Maker" width:144 height:18 align:#center
    button btn_orderrooms "Order Rooms" width:144 height:18 align:#center
    button btn_editvis "Edit Visibility" width:144 height:18 align:#center
    group "Layout File"
    (
        button btn_importlyt "Import" width:68 height:18 align:#left across:2 toolTip:"Import layout file. 'With Models' also imports relevant models from the same directory."
        button btn_exportlyt "Export" width:68 height:18 align:#right toolTip:"Export layout file. 'With Models' also exports relevant models to the same directory."
        checkbox chk_withvis "With VIS file" checked:true
        checkbox chk_withmodels "With Models" checked:false
        checkbox chk_withanims "With Anims" checked:true enabled:false offset:[15,0]
        edittext edit_suffix "Suffix" enabled:false offset:[15,0] width:120
    )

    -- ensure only active if running max version of kotormax
    on kx_util6 open do
    (
	    if ( not g_ismax ) then
	    (
		    btn_minimap.enabled = false
	    )
    )

    -- actions
    on btn_orderrooms pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_orderrooms.ms")) catch() )
    on btn_editvis pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_editvis.ms")) catch() )
	on chk_withmodels changed state do
	(
		chk_withanims.enabled = state
		edit_suffix.enabled = state
	)

    on btn_minimap pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_minimap_maker.ms")) catch() )

	on btn_importlyt pressed do
	(
	    local filename = getOpenFileName caption:"Import lyt" types:"Odyssey Layout (*.lyt)|*.lyt|"
		if filename == undefined then return 0
		local sSuffix = edit_suffix.text
		if matchPattern sSuffix pattern:"*.mdl.ascii" then sSuffix = substring sSuffix 0 (sSuffix.count - 10)
		else if matchPattern sSuffix pattern:"*.mdl" then sSuffix = substring sSuffix 0 (sSuffix.count - 4)
		g_exportPath = getFilenamePath filename
		ImportOdysseyLayout filename \
							showprogress:true \
							withvis:chk_withvis.checked \
							withmodels:chk_withmodels.checked \
							doanims:chk_withanims.checked \
							suffix:sSuffix
		
		kx_setinivalue #(#("init", "lastexport", g_exportPath, (g_kotormaxPath + "kotormax.ini")))	
	)

	on btn_exportlyt pressed do
	(
		local Rooms = for base in helpers where kx_istopmostbase base == 1 and base.layouttype == 2 collect base
		local Tracks = for base in helpers where kx_istopmostbase base == 1 and base.layouttype == 3 collect base
		local Obstacles = for base in helpers where kx_istopmostbase base == 1 and base.layouttype == 4 collect base
		local Doorhooks = #()

		local basestotal = Rooms.count + Tracks.count + Obstacles.count
		if basestotal == 0 then
		(
			messageBox "No Rooms, Tracks or Obstacles in the current scene. There is nothing to export." title:"Layout Export Failed" beep:true
			return false
		)

		for i = 1 to Rooms.count do
		(
			local room = (for base in Rooms where base.layoutnum == (i - 1) collect base)[1]
			if room == undefined then
			(
				messageBox "Error: Invalid room ordering. Please try ordering the rooms again." title:"Layout Export Failed" beep:true
				return false
			)
			else
			(
				join Doorhooks (for doorhook in room.children where matchPattern doorhook.name pattern:"door_*" and doorhook.name.count < 8 collect doorhook)
			)
		)

		-- passed the checks, so let the user pick the file now.
	    local filename = getSaveFileName caption:"Export lyt" types:"Odyssey Layout (*.lyt)|*.lyt|"
	    if (filename != undefined) then
		(
			g_exportPath = getFilenamePath filename

			if (matchPattern (getFilenameFile filename) pattern:"*	") then
			(
				messageBox "Model name contains a tab. ERROR!! Processing Halted."
				return -1
			)

			clearListener()

			-- set the external control snoop bracket.
			if not g_ismax then
			(
				format "<snoopstart file=%%>%" g_kotormaxPath "scratch\\exportcontrol.txt" g_delim to:g_strBuffer
				format "\r\n" g_delim to:g_strBuffer
	    		format "<snoopstart file=%>%" filename g_delim to:g_strBuffer
				format "\r\n" g_delim to:g_strBuffer
			)

			if g_ismax then
				g_strBuffer = createFile filename

			ExportOdysseyLayout (getFilenameFile filename) Rooms Tracks Obstacles Doorhooks \ 
								showprogress:true  \
								withvis:chk_withvis.checked \
								withmodels:chk_withmodels.checked \
								doanims:chk_withanims.checked

			if g_ismax then
			(
				flush g_strBuffer
				close g_strBuffer
			)

			if not g_ismax then
			(
				format "\r\n" g_delim to:g_strBuffer
				format "</snoopstart>%" g_delim to:g_strBuffer  -- closes model export
				format "\r\n" g_delim to:g_strBuffer
				format "</snoopstart>%" g_delim to:g_strBuffer  -- closes exportcontrol.txt
				format "\r\n" g_delim to:g_strBuffer
				format "</snoopend>%" g_delim to:g_strBuffer
				--format "\r\n" to:g_strBuffer
			)

			kx_FlushBuffer force:true

			kx_setinivalue #(#("init", "lastexport", g_exportPath, (g_kotormaxPath + "kotormax.ini")))

			--format "Done exporting." to:g_strBuffer
			--kx_FlushBuffer force:true

			clearSelection()
		)
	)
)
