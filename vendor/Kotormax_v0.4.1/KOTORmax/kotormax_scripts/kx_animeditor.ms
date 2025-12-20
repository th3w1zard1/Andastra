/*-----------------------------------------------------------------------------\

    Animation Editor

	KOTORmax
	by bead-v
	
	Derived from:
    NWmax
    by Joco (jameswalker@clear.net.nz)

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

global kx_animeditor_flag
rollout kx_animeditor "Animation Editor"
(
    local max_anim_frame
    local nodesel = undefined

	fn is_modelbase obj =
	(
    	(iskindof obj odysseybase)
	)

	-- gui
	label lbl_modellbl "Model: " width:50 pos:[12, 10] align:#left across:4
	label lbl_model "" pos:[82, 10] align:#left
	pickbutton btn_setbase "Pick Model Base" filter:is_modelbase pos:[255, 8]
	button btn_refresh "Refresh" pos:[200, 8]

	button btn_save "<" pos:[195, 50]
	button btn_load ">" pos:[195, 70]
	label lbl_name1 "Name:" pos:[230, 50] width:100
	edittext edit_name pos:[227, 65] width:122
	dropdownlist ddl_name pos:[230, 83] width:120
	label lbl_animroot "Animation Root:" pos:[230, 110] width:100
	edittext edit_animroot pos:[227, 125] align:#left width:120
	spinner spn_transition "Transition:  " type:#float range:[0, 10000, 0] pos:[270, 155] align:#left width:78
	spinner spn_framestart "Frame Start:" type:#integer range:[0, 10000, 0] pos:[270, 185] align:#left width:78
	spinner spn_frameend "Frame End: " type:#integer range:[0, 10000, 0] pos:[270, 205] align:#left width:78
	checkbox chk_export "Export" checked:true pos:[230, 230]
	multilistbox lst_anims "Animations" pos:[12, 30] height:16 width:180 align:#left
	button btn_delete "Delete" align:#left width:58 pos:[13, 266]
	button btn_zoomin "Zoom In" align:#left width:58 pos:[73, 266]
	button btn_zoomout "Zoom Out" align:#left width:58 pos:[133, 266]

	button btn_event_save "<" pos:[195, 320]
	--button btn_event_load ">" pos:[195, 340]
	label lbl_name2 "Name:" pos:[230, 320] width:100
	edittext edit_event_name pos:[227, 335] width:122
	dropdownlist ddl_event_name pos:[230, 353] width:120
	spinner spn_event_frame "Frame:        " type:#integer range:[0, 10000, 0] pos:[270, 390] align:#left width:80
	multilistbox lst_events "Events" height:8 width:180 align:#left pos:[12, 300]
	button btn_event_delete "Delete" width:180 align:#left

    -- read list of animation names from a file
    fn ReadAnimationNameList filename =
    (
        namelist = #()
        fh = openFile filename mode:"r"
        if (fh != undefined) then
        (
            while not eof(fh) do
            (
                animname = readDelimitedString fh " \n"
                if (animname != "") then
                (
                    append namelist animname
                )
                else
                (
                    exit
                )
            )
            close fh
        )
        return namelist
    )


    fn GetFirstSelectedMlbItem Mlb =
    (
        for i = 1 to Mlb.items.count do
        (
            if(Mlb.selection[i]) then return Mlb.items[i]
        )
        return undefined
    )

    fn refresh_anim_namelist =
    (
        if (nodesel != undefined and nodesel.classification != undefined) then
        (
            namelist = readAnimationNameList (g_kotormaxPath + "names\\"+ nodesel.classification + ".nam")
            ddl_name.items = namelist
        )
        namelist = readAnimationNameList (g_kotormaxPath + "names\\"+ "events.nam")
        ddl_event_name.items = namelist
    )

	fn refresh_list =
	(
    	local temp = #()
    	if nodesel != undefined then
    	(
            lbl_model.text = nodesel.name
        	local mbase = nodesel
        	join temp mbase.animations
        	lst_anims.items = temp
        	refresh_anim_namelist()
    	)
        else lbl_model.text = ""
        ddl_name.selection = 0
        ddl_event_name.selection = 0
        edit_name.text = ""
        edit_event_name.text = ""
        edit_animroot.text = ""
        spn_event_frame.value = (sliderTime.frame as integer)
        spn_framestart.value = (sliderTime.frame as integer)
        spn_frameend.value = (sliderTime.frame as integer)
        spn_transition.value = 0
        chk_export.checked = true
        lst_anims.selection = #{}
	)

    fn updateeventlist=
    (
    	if nodesel != undefined then
    	(
            local tmpList = #()
            for e in nodesel.events do (
                join tmpList #(e)
            )
            lst_events.items = tmpList
            lst_events.selection = #{}
        )
    )

    fn ZoomToAnim=
    (
    	-- parse out the selected animation
        local str_anim = GetFirstSelectedMlbItem lst_anims
        if (str_anim != undefined) then
        (
            -- parse the animation string
            tokens = filterString str_anim " "
            local first_frame = (tokens[2] as integer)
            local last_frame = (tokens[3] as integer)

            -- zoom to the double clicked animation
            if (first_frame < last_frame) then
            (
                animationRange = (interval first_frame last_frame)
                sliderTime = first_frame
            )
        )
    )

    fn LoadSelectedAnimation =
    (
        --format "loading selected anim\n"
        local str_anim = GetFirstSelectedMlbItem lst_anims
          if (str_anim != undefined) then
          (
			--format "passed the check\n"
			-- parse the animation string
			tokens = filterString str_anim " "
			lst_anims.selection = #(findItem lst_anims.items str_anim)
			local nSelIndex = findItem ddl_name.items tokens[1]
			ddl_name.selection = nSelIndex
			if(nSelIndex == 0) then edit_name.text = tokens[1]
			else edit_name.text = ""


			spn_framestart.value = (tokens[2] as integer)
			spn_frameend.value = (tokens[3] as integer)
			spn_transition.value = (tokens[4] as float)
			-- to be backwards compatible we need to check if [5] is null
			if tokens[5] == undefined then
				chk_export.checked = true
			else
				chk_export.checked = (kx_itob (tokens[5] as integer))
			-- need to check if there is animroot data
			if tokens.count > 5 then
			(
				-- we have anim root data
				edit_animroot.text = tokens[6]
			)
			else (
				edit_animroot.text = ""
			)
			--format "done updating stuff\n"
        )
        else
        (
            ddl_name.selection = 0
            edit_name.text = ""
            edit_animroot.text = ""
            spn_framestart.value = (sliderTime.frame as integer)
            spn_frameend.value = (sliderTime.frame as integer)
            spn_transition.value = 0
            chk_export.checked = true
        )
    )

    fn SaveOpenAnimation =
    (
    	if nodesel != undefined then
    	(
            --format "Saving animation\n"
            local sAnimName = edit_name.text
            if(sAnimName == "" or sAnimName == undefined) then sAnimName = ddl_name.selected
			if sAnimName == undefined then return false
            local sItemString = sAnimName + " " + (spn_framestart.value as integer) as string + " " + (spn_frameend.value as integer) as string + " " + (spn_transition.value) as string + " " + (kx_btoi chk_export.checked) as string + " " + edit_animroot.text
            --format "Saving string: %\n" sItemString
            --local nFound = 0
            local aCopy = nodesel.animations
            for i = 1 to nodesel.animations.count do (
                tokens = filterString nodesel.animations[i] " "
                --format "Comparing % and %\n" tokens[1] sAnimName
                if(tokens[1] == sAnimName) then
                (
                    --format "Match found at index %\n" i
                    aCopy[i] = sItemString
                    nodesel.animations = aCopy
                    refresh_list()
                    return 0
                    --nFound = i
                    --exit
                )
            )
            --format "No match found, appending string\n"
            append aCopy sItemString
            nodesel.animations = aCopy
            refresh_list()
        )
    )

    fn SaveOpenEvent =
    (
    	if nodesel != undefined then
    	(
            --format "Saving event\n"
            local sEventName = edit_event_name.text
            if(sEventName == "" or sEventName == undefined) then sEventName = ddl_event_name.selected
            local sItemString = sEventName + " " + (spn_event_frame.value as integer) as string
            --format "Saving string: %\n" sItemString
            local aCopy = nodesel.events
            append aCopy sItemString
            nodesel.events = aCopy
            updateeventlist()
        )
    )

    on btn_delete pressed do
    (
        if (nodesel != undefined and lst_anims.selection != #{}) then
        (
            local sel = lst_anims.selection
            --aCopy = nodesel.animations
            for i = nodesel.animations.count to 1 by -1 do (
                --format "Checking to delete item %\n" i
                if(sel[i]) then(
                    --format "Found match for deletion\n"
                    deleteItem nodesel.animations i
                )
            )
            --nodesel.animations = aCopy
            lst_anims.selection = #{}
        )
    	refresh_list()
    )

    on btn_event_delete pressed do
    (
        if (nodesel != undefined and lst_events.selection != #{}) then
        (
            local sel = lst_events.selection
            aCopy = nodesel.events
            for i = nodesel.events.count to 1 by -1 do (
                --format "Checking to delete event %\n" i
                if(sel[i]) then(
                    --format "Found match for deletion\n"
                    deleteItem aCopy i
                )
            )
            nodesel.events = aCopy
            lst_events.selection = #{}
        )
    	updateeventlist()
    )

	on btn_setbase picked obj do
	(
        nodesel = obj
    	refresh_list()
        updateeventlist()
	)

	on lst_anims doubleClicked val do
	(
        stopAnimation()
    	if nodesel != undefined then
    	(
            --stopAnimation()
            lst_anims.selection = val
            ZoomToAnim()
            LoadSelectedAnimation()
            playAnimation()
      )
    )

	on btn_refresh pressed do
	(
    	refresh_list()
        updateeventlist()
	)

    on btn_zoomin pressed do
    (
    	if nodesel != undefined then
    	(
            stopAnimation()
            -- parse out the selected animation
            local str_anim = GetFirstSelectedMlbItem lst_anims
            if (lst_anims.selection != #{}) then
            (
                local first_frame = 1000000
                local last_frame = 0
                for i = 1 to lst_anims.items.count where lst_anims.selection[i] do
                (
                    -- parse the animation string
                    tokens = filterString lst_anims.items[i] " "
                    if first_frame > (tokens[2] as integer) then first_frame = (tokens[2] as integer)
                    if last_frame < (tokens[3] as integer) then last_frame = (tokens[3] as integer)
                )

                -- zoom to the double clicked animation
                if (first_frame < last_frame) then
                (
                    animationRange = (interval first_frame last_frame)
                    sliderTime = first_frame
                )
            )
            --ZoomToAnim()
        )
    )

    on btn_zoomout pressed do(
    	if nodesel != undefined then
    	(
            stopAnimation()
            if(max_anim_frame == undefined) then max_anim_frame = 0
            for str_anim in nodesel.animations do(
                local sTokens = filterString str_anim " "
                local nMax = sTokens[3] as integer
                if(nMax > max_anim_frame) then max_anim_frame = nMax
            )
            animationRange = (interval 0 max_anim_frame)
        )
        else animationRange = (interval 0 100)
    )

	on btn_load pressed do(
        --format "load pressed\n"
        LoadSelectedAnimation()
	)

	on btn_save pressed do
	(
        --format "save pressed\n"
        SaveOpenAnimation()
	)

	on btn_event_save pressed do
	(
        --format "save event pressed\n"
        SaveOpenEvent()
	)

	-- actions
	on kx_animeditor open do
	(
    	kx_animeditor_flag = true
    	spn_frameend.value = (sliderTime.frame as integer)
    	spn_framestart.value = (sliderTime.frame as integer)
    	spn_event_frame.value = (sliderTime.frame as integer)
        nodesel = undefined
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
    	    -- If there is only 1 then select it, else show message.
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
            nodesel = base
            refresh_list()
            updateeventlist()
        )
	)

	on kx_animeditor close do
	(
    	kx_animeditor_flag = false
	)
)

if kx_animeditor_flag != true then
(
    --format "firing animeditor dialog!\n"
    createDialog kx_animeditor width:362
)
