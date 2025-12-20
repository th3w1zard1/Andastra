/*-----------------------------------------------------------------------------\

    Animation Parts Mapper

	KOTORmax
	by bead-v
	
	Derived from:
    NWmax
    by Joco (jameswalker@clear.net.nz)

    Anim Mapper supports the copying of animations from model parts
    to other model parts that are in different files. For example you
    could copy just the right arm swing anims from the a_ba model chain
    to a custom model.

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


global kx_animpartsmapper_flag
-- parts anim mapper gui
rollout kx_animpartsmapper "Animation Parts Importing Mapper" width:540 height:514
(
    local oTargetBase = undefined

	fn is_modelbase obj =
	(
    	(iskindof obj odysseybase)
	)

	GroupBox grp1 "1. Sources to Targets" pos:[4,7] width:154 height:507
	edittext edt_mdl "" pos:[14,26] width:126 height:24
	button btn_browse "Browse MDL" pos:[19,53] width:80 height:19
	button btn_loadmdl "Load" pos:[103,53] width:36 height:19
	listbox lst_mdlobjs "Source MDL Nodes" pos:[13,78] width:136 height:13
	pickbutton btn_setbase "Pick Target MDL Base" filter:is_modelbase pos:[20,275] width:121 height:21
	listbox lst_trgnobhs "Target MDL Nodes" pos:[13,300] width:136 height:12
	button btn_refresh "Refresh Target Nodes" pos:[20,485] width:121 height:21


	GroupBox grp4 "2. Anim Data" pos:[163,7] width:373 height:311
	button btn_selectall "Sel All" pos:[175,202] width:50 height:16
	button btn_deselectall "Desel All" pos:[230,202] width:50 height:16
	button btn_addanim "Add" pos:[285,203] width:50 height:16
	multiListBox lst_availanims "Anim names available:" pos:[174,30] width:161 height:11
	multiListBox lst_animstoproc "Anims to process:" pos:[365,30] width:161 height:11
	button btn_selectall2 "Sel All" pos:[366,203] width:50 height:16
	button btn_deselectall2 "Desel All" pos:[421,203] width:50 height:16
	button btn_delanim "Remove" pos:[476,202] width:50 height:16
	dropdownList drp_options "Options" pos:[176,221] width:146 height:40 items:#("Append To End", "Use Insert Point")
	spinner spn_inserrtpoint "Insert Point:" pos:[372,240] width:125 height:16 enabled:false range:[0,100000,0] type:#integer scale:1
	button btn_import "Import" pos:[273,275] width:136 height:26
	--checkbox chk_resetorient "Reset Orientation" pos:[360,490] checked:false
	
	GroupBox grp3 "Mappings: Source --> Target" pos:[163,324] width:374 height:190
	button btn_addmap ">>" pos:[173,347] width:40 height:28
	button btn_delmap "<<" pos:[173,377] width:40 height:28
	button btn_automap "Auto" pos:[173,407] width:40 height:28
	button btn_mapimport "Import" pos:[173,437] width:40 height:28
	button btn_mapexport "Export" pos:[173,467] width:40 height:28
	listbox lst_mappings "" pos:[220,340] width:309 height:12
	
	

	on btn_mapimport pressed do
	(
		local sPattern = "Mapping file (*.txt)|*.txt|"
		
	    local f = getOpenFileName caption:"Import mapping file" types:sPattern

		if f != undefined then
		(
			format ("Importing: " + f + "\n")
			
			local tmp = #()

			-- only progress further if there IS a file available to process
			if not kx_existFile f then return false

			-- Open up the file
			local mdlStream = openFile f mode:"r"

			try (
				local lineNum = 0
				
				while not eof mdlStream do
				(
					local tok = kx_Tokenizer()
					local line = getNextNonBlankLine mdlStream
					tok.SetString (line)
					lineNum += 1

					-- Read the data ID tag
					local idToken = tok.ReadToken()

					if idToken[1] == "#" or idToken == "" or idToken == undefined then continue
					
					local idToken2 = tok.ReadToken()
					if idToken2[1] == "#" or idToken2 == "" or idToken2 == undefined then
					(
						--messageBox ("Error: no target node for " + idToken + " provided in the mapping file")
						throw ("Error: no target node for " + idToken + " provided in the mapping file")
					)
					
					append tmp (idToken + " --> " + idToken2)
					
				) -- END while()
			) -- END try()
			catch
			(
				messageBox ("Mapper Script Error reading Mapping File at line:" + lineNum as string)
				close mdlStream
				throw
			)
			close mdlStream
			
			lst_mappings.items = tmp
		)
		return false
	)
	
	on btn_mapexport pressed do
	(
		local sPattern = "Mapping file (*.txt)|*.txt|"
		
	    local f = getSaveFileName caption:"Export mapping file" types:sPattern

		if f != undefined then
		(
			format ("Exporting: " + f + "\n")
			
			g_exportPath = getFilenamePath f

			if (matchPattern (getFilenameFile f) pattern:"*	") then
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
	    		format "<snoopstart file=%>%" f g_delim to:g_strBuffer
				format "\r\n" g_delim to:g_strBuffer
			)
			else g_strBuffer = createFile f
			
			-- EXPORT CONTENTS
			
			for m in lst_mappings.items do
			(
				local mtoks = filterString m " ->"
				format "% %%" mtoks[1] mtoks[2] g_delim to:g_strBuffer
			)
			
			-- DONE EXPORTING CONTENTS
			
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
			else
			(
				flush g_strBuffer
				close g_strBuffer
			)

			kx_FlushBuffer force:true
			
			-- do a general clean up as we eat some memory in these scripts
			gc()

			--kx_setinivalue #(#("init", "lastexport", g_exportPath, (g_kotormaxPath + "kotormax.ini")))
		)
		
		return false
	)

	fn RefreshTargetNodes =
	(
        if(oTargetBase != undefined) then
        (
            local temp = #()
            -- load scene objects
            for o in $objects where not isDeleted o do
            (
                if isdescendantof o oTargetBase then append temp o.name
            )
            lst_trgnobhs.items = temp
        )
		return false
    )

	on kx_animpartsmapper open do
	(
        oTargetBase = undefined
	    local base = undefined
        local basecount = 0
	    for o in selection where not isDeleted o do
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
    	    for o in $objects where not isDeleted o do
    	    (
        	    if (iskindof o odysseybase) then
        	    (
            	    basecount += 1
            	    base = o
        	    )
    	    )
	    )
        if basecount == 1 then (
            oTargetBase = base
        )

        RefreshTargetNodes()

		kx_animpartsmapper_flag = true
	)

	on kx_animpartsmapper close do
	(
    	kx_animpartsmapper_flag = false
	)

	on btn_setbase picked obj do
	(
        oTargetBase = obj
        RefreshTargetNodes()
	)

	on btn_browse pressed do
	(
		local sPattern = "Odyssey Ascii Model (*.mdl)|*.mdl|Odyssey Ascii Model (*.mdl.ascii)|*.mdl.ascii|"
		if kx_itob g_dotAscii then sPattern = "Odyssey Ascii Model (*.mdl.ascii)|*.mdl.ascii|Odyssey Ascii Model (*.mdl)|*.mdl|"
		
	    local f = getOpenFileName caption:"MDL file with source animations" types:sPattern

		if f != undefined then
		(
			edt_mdl.text = f
		)
	)

	on btn_selectall pressed do
	(
		lst_availanims.selection = #{1..(lst_availanims.items.count)}
	)

	on btn_deselectall pressed do
	(
		lst_availanims.selection = 0
	)

	on btn_addanim pressed do
	(
		local tmp = lst_animstoproc.items as array
		for s in lst_availanims.selection do
		(
			-- only add animation if not already in list
			if (findItem tmp lst_availanims.items[s]) == 0 then
			(
				append tmp lst_availanims.items[s]
			)
		)
		lst_animstoproc.items = tmp
	)

	on btn_selectall2 pressed do
	(
		lst_animstoproc.selection = #{1..(lst_animstoproc.items.count)}
	)

	on btn_deselectall2 pressed do
	(
		lst_animstoproc.selection = 0
	)

	on btn_delanim pressed do
	(
		local tmp = lst_animstoproc.items as array
		local sel = lst_animstoproc.selection as array
		sort sel
		for s = sel.count to 1 by -1 do
		(
			deleteitem tmp sel[s]
		)
		lst_animstoproc.items = tmp
	)

	on btn_refresh pressed do
	(
        RefreshTargetNodes()
	)

	on drp_options selected val do
	(
		case val of
		(
			1: spn_inserrtpoint.enabled = false
			2: spn_inserrtpoint.enabled = true
		)
	)

	on btn_loadmdl pressed do
	(
		-- load all the geom nodes in the mdl file
	    -- Model heirarchy defs
	    local MDL_DEF = 1
	    local MDL_GEOM = 2
	    local MDL_ANIM = 3
	    local MDL_NOT_IN_MDL = 4

	    local pFile = edt_mdl.text

	    if (pFile == unefined) or (pFile == "") then return false

	    local currentPath = getFilenamePath pFile

	    -- Open up the file and check to see if its a binary file
	    local binMdlStream = fopen pFile "rb"
	    local isNotBinary = ReadByte binMdlStream
	    fclose binMdlStream

	    if (isNotBinary == 0) then MessageBox("Invalid ascii MDL file!")

	    -- Open up the file
	    local mdlStream = openFile pFile mode:"r"

	    local lineNum = 0
	    local mdl_pos = MDL_DEF
	    local quitEarly = false


	    -- Setup basis for model header
	    local newModelClassification = 1
	    local newModelSupermodel = "NULL"

	    local nodenames = #()
	    local animnames = #()

	    try (
	        -- Go through the whole mdl file
	        while not (eof mdlStream) and not quitEarly do
	        (
	            local tok = kx_Tokenizer()
	            local line = getNextNonBlankLine mdlStream
	            tok.SetString (line)
	            lineNum += 1


	            -- Read the data ID tag
	            local idToken = tok.ReadToken()

	            -- if idToken is a string token the convert to lowercase
	            if (isKindOf idToken string) then idToken = kx_lowercase idToken

	            if (idToken[1] == "#") then idToken = "#"
	            if (idToken == "donemodel") then
	            (
	                mdl_pos = MDL_NOT_IN_MDL
	                quitEarly = true
	            )
	            case mdl_pos of (
	                ----------------------------------------------------------------
	                -- Model definition
	                ----------------------------------------------------------------
	                MDL_DEF:
	                (
	                    if DEBUG then format "MDL_DEF:  Line:% :: %\r\n" lineNum idToken
	                    case idToken of
	                    (
	                        "#": () -- ignore comments
	                        "newmodel": newModelName = tok.ReadString()
	                        "beginmodelgeom": mdl_pos = MDL_GEOM
	                    )
	                ) -- End MDL_DEF case

	                -- Model Geometry
	                MDL_GEOM:
	                (
	                    if DEBUG then format "MDL_GEOM:  Line:% :: %\r\n" lineNum idToken
	                    case idToken of
	                    (
	                        "#": () -- ignore comments
	                        "node": -- Node Struct
	                        (
	                            -- Define initial node data
	                            objType = tok.ReadToken()
	                            objName = tok.ReadToken()
	                            append nodenames objName
	                        )


	                        "endmodelgeom": -- End of model geometry
	                        (
	                            mdl_pos = MDL_ANIM;
	                        )
	                    ) -- end iktoken case statement
	                ) -- End MDL_GEOM case


	                ----------------------------------------------------------------
	                -- Load model animations
	                ----------------------------------------------------------------
	                MDL_ANIM:
	                (
	                    if DEBUG then format "MDL_ANIM:  Line:% :: %\r\n" lineNum idToken
	                    case idToken of
	                    (
	                        "#": () -- ignore comments

	                        "newanim": -- New animation
	                        (
	                            animName = tok.ReadToken()
	                            append animnames animName
	                        )
	                        "doneanim": ()
	                    )
	                ) -- End MDL_ANIM case
	            ) -- end mdl_pos case statement
	        ) -- end while not eof
	    )
	    catch
	    (
	        messageBox ("Odyssey Importer Script Error in Animation Mapper reading MDL file at line:" + lineNum as string)
	        close mdlStream
	        throw
	    )
	    close mdlStream
	    kx_FlushBuffer()

	    -- now add the mdl name list back in
	    lst_mdlobjs.items = nodenames
        lst_availanims.items = animnames
	)

	on btn_addmap pressed do
	(
		-- add only of selection in both list boxes
		local mdlsel = lst_mdlobjs.selected
		local trgsel = lst_trgnobhs.selected
		local tmp

		if (mdlsel != undefined) and (trgsel != undefined) then
		(
			tmp = lst_mappings.items as array
			append tmp (mdlsel + " --> " + trgsel)
			lst_mappings.items = tmp
		)
	)

	on btn_delmap pressed do
	(
		local mapsel = lst_mappings.selection
		local tmp = lst_mappings.items as array
		-- sanity check
		if mapsel == 0 then return false
		deleteItem tmp mapsel
		lst_mappings.items = tmp
	)

	on btn_automap pressed do
	(
		-- work through the mdl list and try to map to same name in scene node list
		local tmp = #()
		for mdl in lst_mdlobjs.items do
		(
			for src in lst_trgnobhs.items do
			(
				if kx_lowercase(mdl) == kx_lowercase(src) then
				(
					append tmp (mdl + " --> " + src)
				)
			)
		)
		if tmp.count > 0 then
		(
			lst_mappings.items = tmp
		)
	)

	on btn_import pressed do
	(
	    -- make sure the model base is selected
	    local base = oTargetBase
	    if base == undefined then
	    (
            messagebox "Target Model Base not selected"
            return false
	    )

	    -- make sure there are anims marked for processing
	    if lst_animstoproc.items.count < 1 then
	    (
		    messagebox "No animations marked for processing"
		    return false
	    )

	    -- make sure there are mappings
	    if lst_mappings.items.count < 1 then
	    (
		    messagebox "No mappings present"
		    return false
	    )

        clearListener()

        -- setup for processing
        local animlist = lst_animstoproc.items as array
        local animcount = 0
        local maptbl = #() -- setup map table
        bStopWork = false
        for m in (lst_mappings.items) do
        (
	        mtoks = filterString m " ->"
	        append maptbl mtoks[1]
	        append maptbl mtoks[2]
        )

        -- work out the insert point
        insertframe = -1
        case drp_options.selection of
        (
	        -- insert at end of last named anim
	        1:
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
		        insertframe = largestframe + 10
	        )

	        -- insert at specified point
	        2: insertframe = spn_inserrtpoint.value
        )

        bStopWork = not (ProgressUpdate ((animcount * 100)/animlist.count))
        if bStopWork then exit

        if insertframe > -1 then (
	        disableSceneRedraw()
	        ImportOdysseyModel \
	            (edt_mdl.text) \
	            true \
	            false \
	            loadGeom:false \
	            destinationBase:base \
	            loadanimlist:animlist \
	            insertpoint:insertframe \
	            mappingtbl:maptbl \
	            animloadtype:(drp_options.selection) \
	            showprogress:true
	        enableSceneRedraw()
        )
        else format "Anim: Insert frame < 0\r\n"

		/*
        if(chk_resetorient.checked) then
        (
            -- reset controllers after all anim import processing has completed.
            local objSum = $objects.count * 2.0
            local cnt = 0.0
            progressStart "Reset Controllers"
            format "Setting to tcb rotation\r\n"
            for o in $objects do
            (
                o.rotation.controller = tcb_rotation()
                --o.pos.controller = tcb_position()
                --o.scale.controller = tcb_scale()
                cnt += 1
                if (progressUpdate (((cnt*100)/objSum) as integer)) == false then return 0
            )
            format "Setting back to linear rotation\r\n"
            for o in $objects do
            (
                o.rotation.controller = linear_rotation()
                --o.pos.controller = linear_position()
                --o.scale.controller = linear_scale()
                cnt += 1
                if (progressUpdate (((cnt*100)/objSum) as integer)) == false then return 0
            )
            progressEnd()
        )
		*/
	)
)

if kx_animpartsmapper_flag != true then
(
    createdialog kx_animpartsmapper style:#(#style_titlebar, #style_border, #style_sysmenu, #style_minimizebox)
)
