/*-----------------------------------------------------------------------------\

    Speed Buttons UI

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

filein (g_kotormaxPath+"kotormax_scripts\\odyssey_fn_desparkle.ms")
filein (g_kotormaxPath+"kotormax_scripts\\kx_adjustnormals.ms")


-- Global var for holding unlnked hierarchy of selected nodes
global g_linkCopy = #()

fn LoadPlugIns =
(
    -- load plugins into g_plugins
    -- Pass through the plugins dir and process all the .ini files
    g_plugins = #()
    local filepattern = g_kotormaxPath+"plugins\\*.ini"
    local files = getFiles filepattern
    for f in files do
    (
        -- Each file is an ini file so get the keys and add to global array
        append g_plugins (getINISetting f "setup" "name")
        append g_plugins (getINISetting f "setup" "script")
    )
)


--------------------------------------------------------------------------------
rollout kx_util2 "Skin Tools" width:162 height:68
(
    button btn_skindumper "Skin Dumper" width:144 height:18
    button btn_weightmaster "Weight Master" width:144 height:18

    on btn_skindumper pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_skindumper.ms")) catch() )
    on btn_weightmaster pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_weightmaster.ms")) catch() )
)

rollout kx_util3 "Mesh Tools" width:162 height:68
(
    --button btn_trimesh "Convert to Editable Mesh"  width:144 height:18 align:#center toolTip:"Convert object into an editable mesh, preserving modifiers."
    --button btn_bumpmaterial "Add TXI Properties"  width:144 height:18 align:#center toolTip:"Adds TXI Properties to the materials used by the objects in selection."
    --button btn_shadowchecker "Check Shadows" width:144 height:18 align:#center toolTip:"Check shadow validity of all selected objects."
    --button btn_tidy_materials "Rationalise Materials" width:144 height:18 align:#center toolTip:"Rationalise the materials in the scene"
    button btn_resetxform "ResetXForm" width:70 height:18 toolTip:"Auto unlinks selection, reset xforms the editable meshes and relinks. Pivots can be preserved or not." across:2
    checkbox chk_keeppivots "Safe Pivots" checked:true
    checkbutton btn_linkcopy "Copy/Paste Links" width:90 height:18 align:#left across:2 toolTip:"Copy linkages of selected nodes, then on 2nd push restore them."
    button btn_copyreset "Reset" width:44 height:18 align:#right toolTip:"Clear copy link buffer"
    button btn_modelscaler "Scale Wizard" width:144 height:18 align:#center
    button btn_tvertdisplace "TVert Displacer" width:144 height:18 align:#center toolTip:"Scale/Move tverts on a material that has been resized"
    button btn_desparkle "\"Desparkle\" Model" width:144 height:18 align:#center toolTip:"Desparkle by rounding all verts to nearest cm"
    button btn_limitweights "Delete Extra Skin Weights" width:144 height:18 align:#center toolTip:"Preserve the 4 weights with the largest influence on all skin verts in the scene and delete the rest."
    --button btn_chilliskinner "Chilli Skinner" width:144 height:18 align:#center
	group "Smoothing"
	(
		button btn_smooth "Apply Smoothing" width:144 height:18 align:#center toolTip:"This will recalculate the vertex normals in the same way they are calculated by the compilers, using smoothing groups. Warning: This is an expensive operation on larger models!"
		checkbox chk_areaweight "Use Area Weighting" checked:true
		checkbox chk_angleweight "Use Angle Weighting" checked:false
		checkbox chk_creasetest "Use Crease Angle" checked:false
		spinner spn_creaseangle "Crease Angle:" range:[0,180,60] type:#integer
	)
	
	on btn_limitweights pressed do undo on
	(
		kx_LimitAllWeights()
	)
	
	on btn_smooth pressed do undo on
	(
		CalculateVertexNormals bArea:chk_areaweight.state bAngle:chk_angleweight.state bCrease:chk_creasetest.state
		max views redraw
	)

    on btn_bumpmaterial pressed do 
	(
		for obj in selection do
		(
			if obj.material != undefined then
			(
				
			)
		)
	)
	
    --on btn_chilliskinner pressed do ( try (filein (scriptsPath + "CSv3.0.2/CSv3-0-2.ms")) catch() )

    on btn_modelscaler pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_scalewiz.ms")) catch() )

    on btn_tvertdisplace pressed do
    (
        try (filein (g_kotormaxPath + "kotormax_scripts/kx_tvertdisplacer.ms")) catch()
    )

    on btn_copyreset pressed do
    (
        g_linkCopy = #()
        btn_linkcopy.checked = false
    )

    on btn_linkcopy changed down do
    (
        if down then
        (
            -- copy links
            g_linkCopy = #()
            for s in selection do
            (
                append g_linkCopy s
                append g_linkCopy s.parent
            )
        ) else
        (
            -- restore links
            local i
            for i = 1 to g_linkCopy.count by 2 do
            (
                try
                    g_linkCopy[i].parent = g_linkCopy[i+1]
                catch ()
            )
        )
    )

    on btn_resetxform pressed do
    (
        local lParents = #()
        clearListener()
        -- build up parent list
        for s in selection do
        (
            append lParents s
            append lParents s.parent
        )
        -- unlink the selection
        format "unlink\r\n"
        for s in selection do
        (
            s.parent = undefined
        )
        -- do the reset
        format "reset xform\r\n"
        -- do the reset
        local rotvalue
        local piv
        local orig
        for s in selection do
        (
            if (iskindof s Editable_Mesh) then
            (
                -- preserve pivots orientation/rotation
                if chk_keeppivots.checked then
                (
                    rotvalue = s.rotation
                    s.rotation = (quat 0 0 0 1)
                )
                orig = copy s
                kx_reset_transforms s
                if chk_keeppivots.checked then
                (
                    s.rotation = rotvalue
                )
                format "XForm added: %\r\n" s.name

                -- collapse stack
                try ( collapsestack s ) catch()
                format "XForm collapsed\r\n"

                -- restore original modifiers less the Xform
                kx_copy_modifiers s orig ignore:#("XForm")
                delete orig
            ) else
            (
                s.scale = [1,1,1]
            )
        )
        -- relink
        format "relink\r\n"
        for n = 1 to lParents.count by 2 do
        (
            format "n=%\r\n" n
            lParents[n].parent = lParents[n+1]
        )
    )


    -- Tidy Materials
    --
    fn qcompare obj1 obj2 =
    (
        local s1 = obj1.mat.diffusemap.filename
        local s2 = obj2.mat.diffusemap.filename

        if s1 < s2 then return -1
        if s1 > s2 then return 1
        0   -- they must be equal
    )
    fn matcompare mat1 mat2 =
    (
        if  (mat1.name != mat2.name) and \
            (mat1.diffusemap.filename == mat2.diffusemap.filename) and \
            (mat1.diffuse == mat2.diffuse) and \
            (mat1.ambient == mat2.ambient) and \
            (mat1.specular == mat2.specular) and \
            (mat1.specularlevel == mat2.specularlevel) and \
            (mat1.glossiness == mat2.glossiness) and \
            (mat1.selfillumcolor == mat2.selfillumcolor) and \
            (mat1.opacity == mat2.opacity) \
        then return true
        false   -- assume not the same unless proven otherwise
    )

    on btn_tidy_materials pressed do
    (
        local objlist = #()
        format "Material Rationalisation\r\n"
        format "... Getting objects\r\n"

        -- get the list of objs that have standard materials with a bitmap
        for o in $objects do
        (
            if (o.mat!=undefined) and ((classof o.mat)==Standardmaterial) then (
                if o.mat.diffusemap != undefined then (
                    append objlist o
                )
            )
        )
        format "... Sorting objects\r\n"
        -- sort the array by the filename on the material
        qsort objlist qcompare

        -- start the rationalisation
        format "... Rationalise objects\r\n"
        local matDeleteList = #()
        local i, j, m
        for i = 1 to objlist.count do
        (
            j = i + 1
            -- make sure that j is within array bounds
            if j <= objlist.count then
            (
                while (matcompare objlist[i].mat objlist[j].mat) do
                (
                    format "... Object >>  %  << rationalised\r\n" objlist[j]
                    -- put this material on the delete list
                    append matDeleteList objlist[j].mat
                    -- rationalise the assigned material
                    objlist[j].mat = objlist[i].mat
                    -- move pointer forward 1
                    j += 1
                    -- make sure j is within array bounds
                    if j > objlist.count then exit
                )
                -- move i forward to j-1
                i = j - 1
            )
        )
        -- Now clean out the materials to be deleted from the scene
        format "... Remove redundant materials\r\n"
        for m in matDeleteList do (
            format "... Material >>  %  << removed\r\n" m.name
            deleteItem sceneMaterials m.name
        )
        format "Material Rationalisation Ends\r\n"
    )
    -- End Tidy Materials
    --

    --
    -- Test for valid shadows.  If a ray from the pivot point hits the
    -- front of any face in the object, the shadows will be off.
    -- Code by roboius (http://rockbottom.vtex.net/)
    --
    fn make_color_str c =
    (
        local r,g,b
        r = c.red as string
        g = c.green as string
        b = c.blue as string
        r+"|"+g+"|"+b
    )
    fn parse_color_str s =
    (
        local list
        local r,g,b
        list = filterString s "|"
        r = list[1] as integer
        g = list[2] as integer
        b = list[3] as integer
        return (color r g b)
    )
    fn test_shadows obj =
    (
        local invalid_shadow_color = [255,0,0]
        local disabled_shadow_color = [0,0,0]
        local face, normal, vertex, dp, errors = 0
        local bHasFailed = 0

        if (not iskindof obj Editable_Mesh) then ( return 0 )

        try
            bHasFailed = (getUserProp obj "shadowfailed") as integer
        catch( bHasFailed = 0 )

        if bHasFailed == 0 then (
            setUserProp obj "wirecolor" (make_color_str obj.wirecolor)
            originalcolor = obj.wirecolor
        ) else (
            originalcolor = parse_color_str (getUserProp obj "wirecolor")
            if originalcolor == undefined then (
                originalcolor = obj.wirecolor
            )
        )

        for ii = 1 to obj.numfaces do
        (
            face = getface obj ii
            normal = getfacenormal obj ii
            vertex = getvert obj face.x

            dp = dot normal vertex

            if ((dot normal obj.pivot) - dp > 5e-3) then ( errors += 1 )
        )

        if (errors > 0) then (
            format "Warning: Object % may have an invalid shadow.\r\n" obj.name
            obj.wirecolor = invalid_shadow_color
            setUserProp obj "shadowfailed" 1
            return 1
        ) else (
            obj.wirecolor = originalcolor
            setUserProp obj "shadowfailed" 0
            return 0
        )
    )
    on btn_shadowchecker pressed do
    (
        for obj in selection do
            test_shadows obj
    )
    -- End shadow test functions
    --
)

rollout kx_util4 "Anim Tools" width:162 height:68
(
    button btn_animeditor "Editor" width:68 height:18 align:#left across:2 toolTip:"Edit animations and events."
    button btn_animapper "Mapper" width:68 height:18 align:#right toolTip:"Import animations off nodes in an MDL and map to scene nodes."
    button btn_animops "Anim Ops" width:68 height:18 align:#left across:2 toolTip:"Some operations on animations."
    button btn_keyops "Key Ops" width:68 height:18 align:#right toolTip:"A number of animation key manipulation operations."
    --button btn_animeditor "Anim Editor" width:144 height:18
    --button btn_animapper "Anim Mapper" width:144 height:18 toolTip:"Import animations off nodes in an MDL and map to scene nodes"
    --button btn_keymaster "Anim Ops" width:144 height:18 toolTip:"Some operations on animations"
    --button btn_animcopy "Key Ops" width:144 height:18 toolTip:"A number of animation key manipulation operations"
    button btn_keytweak "Mass Key Tweaker" width:144 height:18 toolTip:"Mass tweaking of rot and pos keys"
    button btn_keyreduct "Reduce Anim Keys" width:144 height:18 toolTip:"Rationalise the animation keys for the selected nodes"
    button btn_bakeanim "Bake Anims" width:144 height:18

    on btn_animeditor pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_animeditor.ms")) catch(format "kx_animeditor.ms threw an exception or wasn't found!\n") )
    on btn_animapper pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_mapper.ms")) catch(format "kx_mapper.ms threw an exception or wasn't found!\n") )
    on btn_animops pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_animops.ms")) catch(format "kx_animops.ms threw an exception or wasn't found!\n") )
    on btn_keyops pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_keyops.ms")) catch(format "kx_keyops.ms threw an exception or wasn't found!\n") )
    on btn_keytweak pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_masskeytweak.ms")) catch(format "kx_masskeytweak.ms threw an exception or wasn't found!\n") )
    on btn_keyreduct pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_keyreduct.ms")) catch(format "kx_keyreduct.ms threw an exception or wasn't found!\n") )
    on btn_bakeanim pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_animbaker.ms")) catch(format "kx_animbaker.ms threw an exception or wasn't found!\n") )
)

global kx_unitBackup = #()
global kx_unitMetric = #(#Metric, #Meters, #Centimeters, 1)
global kx_bUnitNoInterrupt = false
fn kx_setUnits unitOptions =
(
	if unitOptions.count != 4 then return false
	
	kx_bUnitNoInterrupt = true
	units.displaytype = unitOptions[1]
	units.metrictype = unitOptions[2]
	units.SystemType = unitOptions[3]
	units.SystemScale = unitOptions[4]
	kx_bUnitNoInterrupt = false
	
	local unitsConverted = #()
	if unitOptions[1] == #Metric then append unitsConverted "1"
	else if unitOptions[1] == #US then append unitsConverted "2"
	else if unitOptions[1] == #Custom then append unitsConverted "3"
	else if unitOptions[1] == #Generic then append unitsConverted "0"
	else throw "Unit Display Type does not match any valid value!"
	if unitOptions[2] == #Millimeters then append unitsConverted "0"
	else if unitOptions[2] == #Centimeters then append unitsConverted "1"
	else if unitOptions[2] == #Meters then append unitsConverted "2"
	else if unitOptions[2] == #Kilometers then append unitsConverted "3"
	else throw "Unit Metric Display Type does not match any valid value!"
	if unitOptions[3] == #Inches then append unitsConverted "0"
	else if unitOptions[3] == #Feet then append unitsConverted "1"
	else if unitOptions[3] == #Miles then append unitsConverted "2"
	else if unitOptions[3] == #Millimeters then append unitsConverted "3"
	else if unitOptions[3] == #Centimeters then append unitsConverted "4"
	else if unitOptions[3] == #Meters then append unitsConverted "5"
	else if unitOptions[3] == #Kilometers then append unitsConverted "6"
	else throw "Unit System Type does not match any valid value!"
	append unitsConverted (unitOptions[4] as string)
	
	-- Array contains the section, attrib, value, file
	kx_setinivalue #(#("Performance", "UnitDispType", unitsConverted[1], (g_ini_location + g_gmaxini)), 
					 #("Performance", "UnitDispMetricType", unitsConverted[2], (g_ini_location + g_gmaxini)),
					 #("Performance", "UnitType", unitsConverted[3], (g_ini_location + g_gmaxini)),
		             #("Performance", "UnitScale", unitsConverted[4], (g_ini_location + g_gmaxini)))
)
rollout kx_prefs "Preferences" width:162 height:68
(
    checkbutton btn_rollup "Float Speedbar" width:144 height:18 align:#center checked:false
    --checkbox chk_autodock "Autodock" checked:(kx_itob(gkx_dock))
    checkbox chk_usepointer "Use Base Pointers" checked:(kx_itob(g_basepointer))
    button btn_removepointer "Delete Base Arrow Pointers" width:144 height:18 align:#center
	checkbox chk_exportwok "Export WOK File" checked:(kx_itob (g_exportwok))
	checkbox chk_exportcolors "Export Vertex Colors" checked:(kx_itob (g_exportcolors))
	checkbox chk_dotascii "Use .ascii By Default" checked:(kx_itob (g_dotAscii))
	checkbox chk_lowercase "Lowercase Filename" checked:(kx_itob (g_expLower))
    checkbutton btn_environ "Use Game Units" width:144 height:18 align:#center toolTip:"Sets the units to meters."
    group "Sanity Check Options"
    (
        checkbox box_RotationCheck "Base: No Rotation" checked:runRotationCheck
        checkbox box_AnimCheck "Anim: Start < End" checked:runAnimCheck
        checkbox box_NameCheck "Node: Valid Name" checked:runNameCheck
        checkbox box_ScaleCheck "Node: Valid Scale" checked:runScaleCheck
        checkbox box_PartTextureCheck "BodyPart: Node=Texture" checked:runPartTextureCheck
        checkbox box_WeldCheck "Mesh: Rounded To cm" checked:runWeldCheck
        checkbox box_ClassCheck "Node: Type 'boolean'" checked:runClassCheck
        checkbox box_ControllerCheck "Node: Valid Controllers" checked:runControllerCheck
        checkbox box_DoganCheck "Mesh: Valid Tvert Values" checked:runDoganCheck
        checkbox box_BitmapNameCheck "Bitmap: Name < 16 chars" checked:runBitmapNameCheck
        checkbox box_FaceCheck "Mesh: Faces > 0" checked:runFaceCheck
        checkbox box_AnimrootCheck "Anim: Animroot Exists" checked:runAnimrootCheck
        checkbox box_BoneCountCheck "Skin: 4 Bones per Vert" checked:runBoneCountCheck
        checkbox box_DuplicateNamesCheck "Node: Unique Names" checked:runDuplicateNamesCheck
        checkbox box_AabbCheck "Aabb: 1 per Model" checked:runAabbCheck
    )
	
	on btn_removepointer pressed do
	(
		for obj in $objects where matchPattern obj.name pattern:"ignore_NGon*" or matchPattern obj.name pattern:"ignore_basepointer" do
		(
			select obj
			max delete
		)
	)

	/*
    on chk_autodock changed state do
    (
		-- Array contains the section, attrib, value, file
		kx_setinivalue #(#("init", "dock", (kx_btoi state) as string, (g_kotormaxPath + "kotormax.ini")))
    )
	*/
    on chk_usepointer changed state do
    (
		-- Array contains the section, attrib, value, file
		kx_setinivalue #(#("init", "basepointer", (kx_btoi state) as string, (g_kotormaxPath + "kotormax.ini")))
		g_basepointer = (kx_btoi state)
    )
    on chk_exportwok changed state do
    (
		-- Array contains the section, attrib, value, file
		kx_setinivalue #(#("init", "exportwok", (kx_btoi state) as string, (g_kotormaxPath + "kotormax.ini")))
		g_exportwok = (kx_btoi state)
    )
    on chk_exportcolors changed state do
    (
		-- Array contains the section, attrib, value, file
		kx_setinivalue #(#("init", "exportcolors", (kx_btoi state) as string, (g_kotormaxPath + "kotormax.ini")))
		g_exportcolors = (kx_btoi state)
    )
    on chk_dotascii changed state do
    (
		-- Array contains the section, attrib, value, file
		kx_setinivalue #(#("init", "dotascii", (kx_btoi state) as string, (g_kotormaxPath + "kotormax.ini")))
		g_dotAscii = (kx_btoi state)
    )

    on chk_lowercase changed state do
    (
		-- Array contains the section, attrib, value, file
		kx_setinivalue #(#("init", "lowercase", (kx_btoi state) as string, (g_kotormaxPath + "kotormax.ini")))
		g_expLower = (kx_btoi state)
    )

	fn saveSanity =
	(
        local sanitychecks = #(runRotationCheck, runAnimCheck, runNameCheck, runScaleCheck, runPartTextureCheck, \
                               runWeldCheck, runClassCheck, runControllerCheck, runDoganCheck, runBitmapNameCheck, \
                               runFaceCheck, runAnimrootCheck, runBoneCountCheck, runDuplicateNamesCheck, runAabbCheck)
        local sPrint = "";

		for bCheck in sanitychecks do if bCheck then sPrint += "1" else sPrint += "0"

		-- Array contains the section, attrib, value, file
		kx_setinivalue #(#("init", "sanity", sPrint, (g_kotormaxPath + "kotormax.ini")))
	)

	on box_RotationCheck changed newState do (
    	-- 1
		runRotationCheck = newState
		saveSanity()
	)
	on box_AnimCheck changed newState do (
    	-- 2
		runAnimCheck = newState
		saveSanity()
	)
	on box_NameCheck changed newState do (
    	-- 3
		runNameCheck = newState
		saveSanity()
	)
	on box_ScaleCheck changed newState do (
    	-- 4
		runScaleCheck = newState
		saveSanity()
	)
	on box_PartTextureCheck changed newState do (
    	-- 5
		runPartTextureCheck = newState
		saveSanity()
	)
	on box_WeldCheck changed newState do (
    	-- 6
		runWeldCheck = newState
		saveSanity()
	)
	on box_ClassCheck changed newState do (
    	-- 7
		runClassCheck = newState
		saveSanity()
	)
	on box_ControllerCheck changed newState do (
    	-- 8
		runControllerCheck = newState
		saveSanity()
	)
	on box_DoganCheck changed newState do (
    	-- 9
		runDoganCheck = newState
		saveSanity()
	)
	on box_BitmapNameCheck changed newState do (
    	-- 10
		runBitmapNameCheck = newState
		saveSanity()
	)
	on box_FaceCheck changed newState do (
    	-- 11
		runFaceCheck = newState
		saveSanity()
	)
	on box_AnimrootCheck changed newState do (
    	-- 12
		runAnimrootCheck = newState
		saveSanity()
	)
	on box_BoneCountCheck changed newState do (
    	-- 13
		runBoneCountCheck = newState
		saveSanity()
	)
	on box_DuplicateNamesCheck changed newState do (
    	-- 14
		runDuplicateNamesCheck = newState
		saveSanity()
	)
	on box_AabbCheck changed newState do (
    	-- 15
		runAabbCheck = newState
		saveSanity()
	)

    -- Deal with float/dock feature
    on btn_rollup changed val do
    (
        if val then
        (
            try (
                cui.FloatDialogBar kotormax_handle
                cui.UnRegisterDialogBar kotormax_handle
                --kotormax_handle.size = [192,85]
                max views redraw
            ) catch ()
        ) else
        (
            try (
                kotormax_handle.size = [192,635]
                cui.RegisterDialogBar kotormax_handle \
                    maxSize:[-1,-1] \
                    style:#(#cui_dock_vert, #cui_floatable, #cui_handles)
                cui.DockDialogBar kotormax_handle #cui_dock_left
            ) catch ()
        )
		kx_setinivalue #(#("init", "dock", (kx_btoi (not val)) as string, (g_kotormaxPath + "kotormax.ini")))
		gkx_dock = (kx_btoi (not val))
    )
	    
	-- old speed bar code
    on kx_prefs open do
    (
        -- correctly set the floating button state
        try (
            tst_state = cui.getDockState kotormax_handle
            -- is docked
            btn_rollup.checked = false
        ) catch
        (
            -- not docked
            btn_rollup.checked = true
        )
        if(gkx_dock == 0) then btn_rollup.checked = true
        else btn_rollup.checked = false
    )
	
    on btn_environ changed val do
    (
		if val then
		(
			kx_unitBackup = #(units.displaytype, units.metrictype, units.SystemType, units.SystemScale)
			kx_setUnits kx_unitMetric
		)
		else
		(
			kx_setUnits kx_unitBackup
		)
    )
)

-- Here we'll make a callback script for 'Reset', to reapply the current unit options.
global kx_unitsReset = false
fn markResetGameUnits =
(
	kx_unitsReset = true
)
fn restoreGameUnits =
(
	kx_unitsReset = false
	if kx_prefs.btn_environ.checked then
	(
		kx_setUnits kx_unitMetric
	)
)
fn updateGameUnitsButton =
(
	if kx_bUnitNoInterrupt or kx_unitsReset then return false
	local unitCurrent = #(units.displaytype, units.metrictype, units.SystemType, units.SystemScale)
	local bMatch = true
	for i = 1 to 4 do if kx_unitMetric[i] != unitCurrent[i] then bMatch = false
	if kx_prefs.btn_environ.checked and not bMatch then
	(
		kx_prefs.btn_environ.checked = false
	)
	else if not kx_prefs.btn_environ.checked and bMatch then
	(
		kx_prefs.btn_environ.checked = true
	)
)
callbacks.addScript #unitsChange "updateGameUnitsButton()" id:#kx_gameUnits
callbacks.addScript #postSystemStartup "updateGameUnitsButton()" id:#kx_gameUnits --It doesn't make sense to call this here, because the system will have already started up, no?
callbacks.addScript #systemPreReset "markResetGameUnits()" id:#kx_gameUnits
callbacks.addScript #systemPostReset "restoreGameUnits()" id:#kx_gameUnits

rollout kx_util_export "MDL Exporting" width:162 height:68
(
    button btn_massops "Batch Operations" width:144 height:18 align:#center toolTip:"Batch Sanity Check, Export and Import."
    button btn_export_all "Export Selected" width:144 height:18 align:#center toolTip:"Export selected models or all models if no model base selected."
    group "Set Export Directory" (
        edittext edt_resetdir "Dir:" text:g_exportPath
        button btn_resetdirbrowse "Browse" width:68 height:18 tooltip:"Select the export path" across:2
        button btn_resetdir "Set" width:68 height:18 tooltip:"Set as export directory for all selected model bases."
    )

    on btn_massops pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_massutils.ms")) catch() )

    on btn_export_all pressed do
    (
        ExportAll()
    )

    on edt_resetdir entered val do
    (
        if not (kx_existDir val) then
            messagebox "Directory does not exist!"
    )

    on btn_resetdir pressed do
    (
        local bIsSelection = false

        if not (kx_existDir edt_resetdir.text) then
        (
            messagebox "Directory does not exist!"
            return 0
        )
        for b in $helpers do
        (
            if b.isSelected and (iskindof b odysseybase) then
            (
                bIsSelection = true
                -- node is selected so reset base
                b.export_path = edt_resetdir.text
            )
        )
        if not (bIsSelection) then
        (
            messagebox "No Odyssey Bases selected."
            return 0
        )
    )

    on btn_resetdirbrowse pressed do
    (
        local dirpath
        -- suggested by Danmar
        dirpath = getSavePath()
        if (dirpath!=undefined) then (
            edt_resetdir.text = dirpath
        )
    )
)

rollout kx_util1 "General Utils" width:162 height:68
(
    local vp_layout
    local plugin_scripts

    -- speedbar #1
    button btn_zoomextents "Zoom In" width:68 height:18 align:#left across:2 toolTip:"Zooms all viewports to selection."
    button btn_zoomextentsall "Zoom Out" width:68 height:18 align:#right toolTip:"Zooms all viewports to scene."
    button btn_sel_parent "Parent" width:68 height:18 align:#left across:2 toolTip:"Select Parent"
    button btn_sel_child "Children" width:68 height:18 align:#right toolTip:"Select Children"
    --button btn_makeintodummy "To Dummy" width:68 height:18 align:#left across:2 tooltop:"Make selected objects into Dummy helpers"
    --button btn_linker "Fast Linker" width:68 height:18 align:#right toolTip:"Link all selected objects to the OdysseyBase in the selection"

    -- speedbar #2
    button btn_orderchildren "Order Children" width:144 height:18 align:#center toolTip:"Manually reorder children of selected object"
    button btn_linker "Fast Linker" width:144 height:18 align:#center toolTip:"Link all selected objects to the OdysseyBase in the selection"
    button btn_randomisecolor "Random Wirecolor" width:144 height:18 align:#center toolTip:"Randomize the wirecolor of all selected objects"
	
	group "Hide / Unhide / Select"
	(
		radiobuttons rdo_hide labels:#("Only the following:", "All but the following:") default:1 columns:1
		checkbox chk_dummy "Dummy"
		checkbox chk_odybase "OdysseyBase"
		checkbox chk_odylight "OdysseyLight"
		checkbox chk_odyemitter "OdysseyEmitter"
		checkbox chk_odyreference "OdysseyReference"
		checkbox chk_odysaber "OdysseyLightsaber"
		checkbox chk_odywalk "OdysseyWalkmesh"
		checkbox chk_odyflex "OdysseyFlex"
		checkbox chk_odymesh "OdysseyTrimesh"
		checkbox chk_skin "Skin"
		checkbox chk_other "Other"
		button btn_hide "Hide" across:3
		button btn_show "Unhide" width:40
		button btn_select "Select" width:40 offset:[-6,0]
	)
	
    group "Plug-Ins"
    (
        dropdownlist dpl_prefabs height:10 width:130 offset:[-6,0] align:#left across:2
        button btn_prefab_do "Go" height:20 width:20 offset:[7,0] align:#right
    )
	
	fn getObjectCollection =
	(
		local show_array = #()
		for obj in $objects do
		(
			local bInclude = false
			if (chk_dummy.state and iskindof obj.baseobject dummy) then bInclude = true
			if not bInclude and (chk_odybase.state and (iskindof obj.baseobject odysseybase or matchPattern obj.name pattern:"ignore_basepointer")) then bInclude = true
			if not bInclude and (chk_odylight.state and iskindof obj.baseobject odysseylight) then bInclude = true
			if not bInclude and (chk_odyemitter.state and iskindof obj.baseobject odysseyemitter) then bInclude = true
			if not bInclude and (chk_odyreference.state and iskindof obj.baseobject odysseyreference) then bInclude = true
			if not bInclude and (chk_odysaber.state and iskindof obj.baseobject odysseylightsaber) then bInclude = true
			if not bInclude and (chk_odywalk.state and obj.modifiers[#OdysseyWalkmesh] != undefined) then bInclude = true
			if not bInclude and (chk_odyflex.state and obj.modifiers[#OdysseyFlex] != undefined) then bInclude = true
			if not bInclude and (chk_odymesh.state and obj.modifiers[#OdysseyTrimesh] != undefined) then bInclude = true
			if not bInclude and (chk_skin.state and obj.modifiers[#Skin] != undefined) then bInclude = true
			if not bInclude and (chk_other.state and not iskindof obj.baseobject dummy and \
								 not iskindof obj.baseobject odysseybase and \
								 not iskindof obj.baseobject odysseylight and \
								 not iskindof obj.baseobject odysseyemitter and \
								 not iskindof obj.baseobject odysseyreference and \
								 not iskindof obj.baseobject odysseylightsaber and \
								 not obj.modifiers[#OdysseyWalkmesh] != undefined and \
								 not obj.modifiers[#OdysseyFlex] != undefined and \
								 not obj.modifiers[#OdysseyTrimesh] != undefined and \
								 not obj.modifiers[#Skin] != undefined) then bInclude = true
			if rdo_hide.state == 1 and bInclude then append show_array obj
			else if rdo_hide.state == 2 and not bInclude then append show_array obj
		)
		return show_array
	)
	
	on btn_show pressed do
	(
		local obj_array = getObjectCollection()
		unhide obj_array
	)
	
	on btn_hide pressed do
	(
		local obj_array = getObjectCollection()
		hide obj_array
	)
	
	on btn_select pressed do
	(
		local obj_array = getObjectCollection()
		select obj_array
	)

	on btn_zoomextents pressed do
	(
		if selection.count > 0 then
		(
			Viewport.ZoomToBounds true (selection.min) (selection.max)
		)
	)
	on btn_zoomextentsall pressed do
	(
		max tool zoomextents all
	)
	
    on btn_randomisecolor pressed do
    (
        local sel = selection as array
        for s in sel do
        (
            try (
                s.wirecolor = random black white
            ) catch()
        )
    )

    -- old speed bar code
    on kx_util1 open do
    (
        local temp = #()
        local i

        LoadPlugIns()

        plugin_scripts = #()
        -- load scripts list into drop down
        for i = 1 to g_plugins.count by 2 do
        (
            append temp g_plugins[i]
            append plugin_scripts g_plugins[i+1]
        )
        dpl_prefabs.items = temp
    )

    -- select parent
    on btn_sel_parent pressed do
    (
        if selection[1] != undefined then
        (
            if selection[1].parent != undefined then
                select selection[1].parent
        )
    )
    -- select children
    on btn_sel_child pressed do
    (
        if selection[1] != undefined then
        (
            if selection[1].children != undefined and selection[1].children.count > 0 then
                select selection[1].children
        )
    )

    on btn_linker pressed do
    (
        local base = undefined
		local bases = #()
		bases = for obj in $helpers where iskindof obj odysseybase collect obj
		if bases.count == 1 then base = bases[1]
		else if bases.count > 0 then
		(
			bases = for obj in $selection where iskindof obj odysseybase collect obj
			if bases.count == 1 then base = bases[1]
			else if bases.count > 1 then
			(
				msg = "More than one OdysseyBase found in the selection"
				messageBox msg title:"Fast Linker"
			)
			else if bases.count == 0 then
			(
				msg = "No OdysseyBase found in the selection"
				messageBox msg title:"Fast Linker"
			)
		)
		
		if base == undefined then return 0

        -- we only have one odysseybase so start the linking process
		local linkobjects = #()
		if selection.count == 0 then linkobjects = $objects
		else linkobjects = selection
        for obj in linkobjects where obj != base and not matchPattern obj.name pattern:"ignore_*" do obj.parent = base
    )

    on btn_prefab_do pressed do
    (
        local index = dpl_prefabs.selection
        try (fileIn (g_kotormaxPath + "plugins\\"+plugin_scripts[index])) catch()
    )
    -- speed bar code stops

    on btn_orderchildren pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_orderchildren.ms")) catch() )

    -- Perform a model desparkle
    on btn_desparkle pressed do
    (
        if (kx_strclassof selection[1]) == "odysseybase" then
        (
            clearListener()
            --format "<snoopstart file=%>\r\n" (selection[1].name + "_desparklelog.txt")
            kx_mesh_desparkle selection[1]
            --format "</snoopstart>\r\n"
        ) else
            MessageBox "No OdysseyBase selected."
    )
)
