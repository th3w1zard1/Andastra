/*-----------------------------------------------------------------------------\

    GUI Create rollout
	
	KOTORmax
	by bead-v
	
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


rollout kx_util0 "Create" width:162
(
    -- ui
    group "Odyssey Objects"
    (
        checkbutton btn_odydummy "Dummy" width:68 height:17 align:#left across:2
        checkbutton btn_odybase "Base" width:68 height:17 align:#right
        checkbutton btn_odylight "Light" width:68 height:17 align:#left across:2 offset:[0, -2]
        checkbutton btn_odyemitter "Emitter" width:68 height:17 align:#right offset:[0, -2]
        checkbutton btn_odyreference "Reference" width:68 height:17 align:#left across:2 offset:[0, -2]
        checkbutton btn_odylightsaber "Lightsaber" width:68 height:17 align:#right offset:[0, -2]
        --label lbl_placeholder "" width:68 height:17 align:#right offset:[0, -2]
    )
    group "Odyssey Modifiers"
    (
        button btn_odytrimesh "Trimesh" width:68 height:17 align:#left across:2
        button btn_odyflex "Flex" width:68 height:17 align:#right
        button btn_odywalkmesh "Walkmesh" width:68 height:17 align:#left across:2 offset:[0, -2]
        button btn_skin "Skin" width:68 height:17 align:#right offset:[0, -2]
    )
	group "Convert"
	(
		button btn_makeintodummy "Convert to Dummy" width:144 height:18 align:#center toolTip:"Convert selected objects into Dummy helpers."
		button btn_trimesh "Convert to Editable Mesh"  width:144 height:18 align:#center toolTip:"Convert selected objects into Editable Meshes, preserving modifiers."
	)

    -- tools
    tool create_odydummy
    (
        on mousePoint clickNo do
        (
            if clickNo > 1 then
            (
                local oObject = createOdysseyDummy gridPoint
                select oObject
                if not ctrlKey then
                (
                    stopTool create_odydummy
                    #stop
                )
            )
        )
    )
    tool create_odylight
    (
        on mousePoint clickNo do
        (
            if clickNo > 1 then
            (
                local oObject = createOdysseyLight gridPoint
                select oObject
                if not ctrlKey then
                (
                    stopTool create_odylight
                    #stop
                )
            )
        )
    )
    tool create_odyemitter
    (
        on mousePoint clickNo do
        (
            if clickNo > 1 then
            (
                local oObject = createOdysseyEmitter gridPoint
                select oObject
                if not ctrlKey then
                (
                    stopTool create_odyemitter
                    #stop
                )
            )
        )
    )
    tool create_odyreference
    (
        on mousePoint clickNo do
        (
            if clickNo > 1 then
            (
                local oObject = createOdysseyReference gridPoint
                select oObject
                if not ctrlKey then
                (
                    stopTool create_odyreference
                    #stop
                )
            )
        )
    )
    tool create_odylightsaber
    (
        on mousePoint clickNo do
        (
            if clickNo > 1 then
            (
                local oObject = createOdysseyLightsaber gridPoint
                select oObject
                if not ctrlKey then
                (
                    stopTool create_odylightsaber
                    #stop
                )
            )
        )
    )
    tool create_odybase
    (
        on mousePoint clickNo do
        (
            if clickNo > 1 then
            (
                local oObject = createOdysseyBase gridPoint
				if not ctrlkey then
				(
					stopTool create_odybase
					select oObject
					createDialog basewiz modal:true
					#stop -- stop odysseybase creation in all cases, because we have a dialog
				)
            )
        )
    )

    -- actions
	on btn_odydummy changed state do
	(
        if state then
        (
            startTool create_odydummy
            btn_odydummy.state = false
        )
        else stopTool create_odydummy
	)
	on btn_odylight changed state do
	(
        if state then
        (
            startTool create_odylight
            btn_odylight.state = false
        )
        else stopTool create_odylight
	)
	on btn_odyemitter changed state do
	(
        if state then
        (
            startTool create_odyemitter
            btn_odyemitter.state = false
        )
        else stopTool create_odyemitter
	)
	on btn_odyreference changed state do
	(
        if state then
        (
            startTool create_odyreference
            btn_odyreference.state = false
        )
        else stopTool create_odyreference
	)
	on btn_odylightsaber changed state do
	(
        if state then
        (
            startTool create_odylightsaber
            btn_odylightsaber.state = false
        )
        else stopTool create_odylightsaber
	)
	on btn_odybase changed state do
	(
        if state then
        (
            startTool create_odybase
            btn_odybase.state = false
        )
        else stopTool create_odybase
	)
	on btn_odytrimesh pressed do
	(
        for node in selection do
        (
            local odyssey_mod = odysseytrimesh()
            try(
                if validModifier node odyssey_mod then addModifier node odyssey_mod
            ) catch()
        )
	)
	on btn_odywalkmesh pressed do
	(
        for node in selection do
        (
            local odyssey_mod = odysseywalkmesh()
            try(
                if validModifier node odyssey_mod then addModifier node odyssey_mod
            ) catch()
        )
	)
	on btn_odyflex pressed do
	(
        for node in selection do
        (
            local odyssey_mod = odysseyflex()
            try(
                if validModifier node odyssey_mod then addModifier node odyssey_mod
            ) catch()
        )
	)
	on btn_skin pressed do
	(
        for node in selection do
        (
            local skin_mod = skin()
            try(
                if validModifier node skin_mod and iskindof node editable_mesh then addModifier node skin_mod
            ) catch()
        )
	)
	
    on btn_makeintodummy pressed do
    (
        -- take the selected objects and make each into a dummy object
        clearlistener()
        local sel = selection as array
        local p -- position of s
        local r -- rotation of s
        local par -- parent of s
        local n -- name of s
        local newdummy

        for s in sel do
        (
            format "Processing: %\r\n" s
            p = s.pos
            r = s.rotation
            n = s.name
            par = s.parent
            newdummy = dummy pos:p
            newdummy.rotation = r
            newdummy.parent = par
            newdummy.boxsize = [10,10,10]
            for c in s.children do
            (
                c.parent = newdummy
            )
            select s
            max delete
            newdummy.name = n
        )
    )
	
    on btn_trimesh pressed do undo on
    (
        for obj in selection do
        (
            local orig = copy obj

            -- Converting to an Editable Patch and back again splits the
            -- given object's polygons into faces (triangles).
            convertToMesh obj
            convertTo obj Editable_Patch
            convertToMesh obj

            -- Restore the original modifiers.
            kx_copy_modifiers obj orig

            delete orig
        )
    )

    on kx_util0 moved val do
    (
		if gkx_dock == 0 then
		(
			-- store the current location of the KOTORmax Speedbar
			kx_setinivalue #(#("init", "position", (val as string), (g_kotormaxPath + "kotormax.ini")))
			/*
			if g_ismax then (
				setINISetting (scriptsPath+"nxlocalprefs.ini") "position" "kotormax" (val as string)
			)
			*/
		)
    )
)
