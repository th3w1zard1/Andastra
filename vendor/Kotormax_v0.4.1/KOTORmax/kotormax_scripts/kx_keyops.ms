/*-----------------------------------------------------------------------------\

    Anim Key Ops

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
-- Animation key copy rollout. Used for Dialog creation
rollout AnimKeyCopy "Animation Key Copier" width:162
(
    spinner spn_startframe "Start Frame:" range:[0,100000,0] type:#integer
    spinner spn_endframe  "End Frame:" range:[0,100000,0] type:#integer
    spinner spn_targetframe "Target Frame:" range:[0,100000,0] type:#integer
    group "" (
	    pickbutton pck_source "Source" align:#left width:50 across:2 toolTip:"Select the source node to copy anim keys from"
	    label lbl_src "#undefined" align:#right
	    pickbutton pck_target "Target" align:#left width:50 across:2 toolTip:"Select the target node. If blank the source node will be used."
	    label lbl_trg "#undefined" align:#right
	    checkbox chk_pos "Pos" checked:true across:3
	    checkbox chk_rot "Rot" checked:true
	    checkbox chk_scl "Scale" checked:true
	    checkbox chk_children "Copy child nodes" checked:true
	    radiobuttons rdo_abs labels:#("absolute","relative") default:1
	)
    group "" (
	-- not yet implemented. Trying to figure out the math needed here
	    --label lbl_notyet "Not implimented:Researching."			enabled:false
        checkbox chk_mirror "Mirror on axes - Rot only" checked:false  enabled:true
        checkbox chk_x "X" across:3                       enabled:true
        checkbox chk_y "Y"                                enabled:true
        checkbox chk_z "Z"                                enabled:true
    )
	button btn_ok "COPY" width:64 across:2
	button btn_cancel "Done" width:64
	
	on spn_startframe changed val do
	(
    	if val > spn_endframe.value then (
    	    spn_endframe.value = val
	    )
	)
	
	on spn_endframe changed val do
	(
    	if val < spn_startframe.value then (
        	spn_startframe.value = val
    	)
	)
	
	on pck_source picked obj do
	(
    	lbl_src.text = obj.name
	)

	on pck_target picked obj do
	(
    	lbl_trg.text = obj.name
    	-- if copying to a target != to source we assume not copying a hierarchy
    	-- unless the user EXPLICTLY sets it
    	if pck_target.object != pck_source.object then (
    	    chk_children.checked = false
	    )
	)
	
	on btn_cancel pressed do ( DestroyDialog AnimKeyCopy )

	-- Animation copy code
	--
	local trgHierCounts = undefined
	local srcHierCounts = undefined
	local trgHierObjs = undefined
	local trgHierIndex
	-- Build a node tree hierachy into an array
    fn buildHierarchyShape node cntlist objlist =
    (
        if cntlist != undefined then (
            append cntlist node.children.count
        )
        if objlist != undefined then (
            append objlist node
        )
        for child in node.children do (
            buildHierarchyShape child cntlist objlist
        )
    )
	
	on btn_ok pressed do
	(
    	local fr, idx, p, r, s, nf, index
    	local sf = spn_startframe.value     -- start frame
    	local ef = spn_endframe.value       -- end frame
    	local tf = spn_targetframe.value    -- target frame
    	local span = ef - sf                -- source frames span (interval)
    	local target_obj
    	trgHierIndex = 1    -- set index to start of target hierarchy array
    	-- Perform sanity check on parameters
    	format "Sanity 1\r\n"
    	if (tf < ef) and (tf > (sf-span)) and \
    	    ((pck_source.object==pck_target.object)or(pck_target.object==undefined)) then
    	(
        	-- error message
        	local errortxt = "Target frame and resulting copy overlaps source range."
        	messagebox errortxt title:"Source/Target Range Error"
        	return 0
    	)
    	format "Sanity 2\r\n"
    	if pck_source.object == undefined then
    	(
        	-- error message
        	local errortxt = "There must at least be a source object identified."
        	messagebox errortxt title:"Source Undefined"
        	return 0
    	)
    	format "Sanity 3\r\n"
    	if chk_mirror.checked and not (chk_x.checked or chk_y.checked or chk_z.checked) then
    	(
        	-- error message
        	local errortxt = "You must select a mirror axis."
        	messagebox errortxt title:"Undefined Mirror Axis"
        	return 0
    	)
    	format "Sanity 4\r\n"
    	if (pck_source.object != pck_target.object) and \
    	    (pck_target.object != undefined) and \
    	    chk_children.checked then
    	(
        	-- make sure the source/target hierarchies are the same
        	trgHierCounts = #()
        	trgHierObjs = #()
        	srcHierCounts = #()
        	buildHierarchyShape pck_target.object trgHierCounts trgHierObjs
        	buildHierarchyShape pck_source.object srcHierCounts undefined
        	-- error message
        	local errortxt = "Target/Source hierarchies not the same."
        	-- start the sanity compares
        	if trgHierCounts.count != srcHierCounts.count then
        	(
            	format "array counts do not match\r\n"
            	trgHierCounts = #()
            	trgHierObjs = #()
            	srcHierCounts = #()
            	messagebox errortxt title:"Hierarchies not the same"
            	return 0
        	)
        	for index = 1 to srcHierCounts.count do
        	(
            	if srcHierCounts[index] != trgHierCounts[index] then
            	(
                	format "child counts do not match\r\n"
                	trgHierCounts = #()
                	trgHierObjs = #()
                	srcHierCounts = #()
                	messagebox errortxt title:"Hierarchies not the same"
                	return 0
            	)
        	)
    	)
    	-- Sanity checks finished
    	
    	-- Set the new animationRange if it's not big enough at the moment
    	if (animationRange.end < (tf+span)) then (
        	animationRange = (interval 0 (tf+span+100))
    	)
    	clearListener()
    	-- dump out the content of the arrays for debug purposes
    	format "trgHierCounts=%\r\n" trgHierCounts
    	format "srcHierCounts=%\r\n" srcHierCounts
    	format "trgHierObjs=%\r\n" trgHierObjs
    	
    	format "s=%; t=%\r\n" pck_source.object pck_target.object
    	if pck_target.object == undefined then
    		target_obj = pck_source.object
		else
			target_obj = pck_target.object
		
    	animkeycopy pck_source.object \
    				target_obj \
    				chk_pos.checked \
    				chk_rot.checked \
    				chk_scl.checked \
    				chk_children.checked
	)
	
	-- Recursive anim key copy
	fn animkeycopy node nodetrg dopos dorot doscale dochildren =
	(
		local fr, idx, p, r, s, nf, tchild
		local existingPosKey
		local existingPos
		local newPosKey
		local sf = spn_startframe.value     -- start frame
		local ef = spn_endframe.value       -- end frame
		local tf = spn_targetframe.value    -- target frame
		local span = ef - sf                -- source frames span (interval)
		format "+++++++++++++ Anim Key Copy ++++++++++++++\r\n"
		-- we have valid data so lets get into it.
		-- 1. Work through the selected nodes
		format "Node: %: Target:% Copy Type: %\r\n" node.name nodetrg.name rdo_abs.state
		-- work through the frames in the source interval and look for keys
		-- which are then copied to the target range
		
		-- Process the pos data
		local masterpos_src = undefined
		local masterpos_src_delta = undefined
		local masterpos_trg = undefined
		if dopos then
		(
			format "copy pos data\r\n"
			for fr = sf to ef do
			(
				idx = getKeyIndex node.pos.controller fr
				if idx > 0 then    -- we have an index!
				(
					in coordsys parent
					(
						-- check to see if is first pass on this obj.
						-- If so setup delta vars, else calc them as normal
						if masterpos_src == undefined then (
							at time sf
							(
								masterpos_src = node.pos
								masterpos_trg = nodetrg.pos
								masterpos_src_delta = masterpos_src - masterpos_src
							)
						) else
						(
							masterpos_src_delta = (at time fr node.pos) - masterpos_src
						)
						case rdo_abs.state of
						(
							1: at time fr p = node.pos
							2: p = nodetrg.pos + masterpos_src_delta
						)
						--p = node.pos.keys[idx].value
						
						-- deal with the mirroring calcs
						/*
						if chk_mirror.checked then
						(
							format "Processing mirror code: pos=%\r\n" r_euler
							if chk_x.checked then (
								-- mirror along the x axis
								r_euler.x *= -1
							)
							if chk_y.checked then (
								-- mirror along the y axis
								r_euler.y *= -1
							)
							if chk_z.checked then (
								-- mirror along the z axis
								r_euler.z *= -1
							)
							
							format "Finished mirror code: euler=%\r\n" r_euler
							-- turn the euler back to a quat
							r = eulerToQuat r_euler order:1
						)
						* /
						-- end mirroring code
						nf = tf+(fr-sf)
						format "Source fr:%  Target fr:%  Pos:%\r\n" fr nf p
						with animate on at time nf nodetrg.pos = p
					)
				)
			)   -- end for fr
		)
		
		-- process rotation data
		local masterrot_src = undefined
		local masterrot_src_delta = undefined
		local masterrot_trg = undefined
		if dorot then
		(
			format "copy rot data\r\n"
			for fr = sf to ef do
			(
				idx = getKeyIndex node.rotation.controller fr
				if idx > 0 then    -- we have an index!
				(
					in coordsys parent
					(
						-- check to see if is first pass on this obj.
						-- If so setup delta vars, else calc them as normal
						if masterrot_src == undefined then (
							at time sf
							(
								masterrot_src = node.rotation
								masterrot_trg = nodetrg.rotation
								masterrot_src_delta = masterrot_src - masterrot_src
							)
						) else
						(
							masterrot_src_delta = (at time fr node.rotation) - masterrot_src
						)
						case rdo_abs.state of
						(
							1: at time fr r = node.rotation
							2: r = nodetrg.rotation + masterrot_src_delta
						)
						-- deal with the mirroring calcs
						if chk_mirror.checked then
						(
							-- we need to convert to euler rots for easy mirroring.
							-- Might be a better way with quats??
							local r_euler = quatToEuler r order:1
							format "Processing mirror code: euler=%\r\n" r_euler
							if chk_x.checked then (
								-- mirror along the x axis
								r_euler.x *= -1
							)
							if chk_y.checked then (
								-- mirror along the y axis
								r_euler.y *= -1
							)
							if chk_z.checked then (
								-- mirror along the z axis
								r_euler.z *= -1
							)
							
							format "Finished mirror code: euler=%\r\n" r_euler
							-- turn the euler back to a quat
							r = eulerToQuat r_euler order:1
						)

						-- end mirroring code
						nf = tf+(fr-sf)
						existingPosKey = getKeyIndex nodetrg.pos.controller nf
						existingPos = [0,0,0]
						if (existingPosKey > 0) then
						(
							existingPos = nodetrg.pos.keys[existingPosKey].value
						)
						format "Source fr:%  Target fr:%  Pos:%\r\n" fr nf r
						with animate on at time nf nodetrg.rotation = r
						
						if (existingPosKey == 0) then
						(
							format "existingPosKey==0\n"
							newPosKey = getKeyIndex nodetrg.pos.controller nf
							if (newPosKey != 0) then deleteKey nodetrg.pos.controller newPosKey
						)
						else
						(
							format "existingPosKey!=0\n"
							format "existingPos=%; existingPosKey=%\n" existingPos existingPosKey
							nodetrg.pos.keys[existingPosKey].value = existingPos
						)
					)
				)
			)   -- end for fr
		)
			
		-- process scale data
		if doscale then
		(
			for fr = sf to ef do
			(
				idx = getKeyIndex node.scale.controller fr
				if idx != 0 then    -- we have an index!
				(
					in coordsys parent
					(
						at time fr s = node.scale
						nf = tf+(fr-sf)
						with animate on at time nf node.scale = s
					)
				)
			)   -- end for fr
		)
		format "\r\n"
		
		-- Q: how to deal with target children for different hierarchy?
		
		-- process the nodes children
		if dochildren then (
			for child in node.children do (
				trgHierIndex += 1
				format "trgHierIndex=%\r\n" trgHierIndex
				if trgHierObjs != undefined then
					tchild = trgHierObjs[trgHierIndex]
				else
					tchild = child
				
				animkeycopy child \
							tchild \
							dopos \
							dorot \
							doscale \
							dochildren
				
			)
		)
	)		
)

createDialog AnimKeyCopy
*/

fn kx_deletekeysinblock obj framestart frameend typeslist =
(
	/*
	work through the objects in the scene and delete the keys
	*/
	local cntrl
	local propnames
	local mctrl

	cntrl = #(undefined, undefined, undefined)
	-- get the standard controllers
	if typeslist[1] then (
		try (
			cntrl[1] = obj.pos.controller
		) catch (
			cntrl[1] = undefined
		)
	)
	if typeslist[2] then (
		try (
			cntrl[2] = obj.rotation.controller
		) catch (
			cntrl[2] = undefined
		)
	)

	if typeslist[3] then (
		try (
			cntrl[3] = obj.scale.controller
		) catch (
			cntrl[3] = undefined
		)
	)

	-- delete the standard controllers
	for c in cntrl do
	(
		if c != undefined then
		(
			for k = c.keys.count to 1 by -1 do
			(
				if (c.keys[k].time >= framestart) and (c.keys[k].time <= frameend) then
				(
					deleteKey c.keys k
				)
			)
		)
	)

	-- now deal with modifiers on obj with animated properties
	if typeslist[4] then
	(
		for m in obj.modifiers do
		(
			propnames = getPropNames m
			for prop in propnames do
			(
				mctrl = getPropertyController m prop
				-- if there is a controller then deal with its keys
				if mctrl != undefined then
				(
					for k = mctrl.keys.count to 1 by -1 do
					(
						if (mctrl.keys[k].time >= framestart) and (mctrl.keys[k].time <= frameend) then
						(
							deleteKey mctrl.keys k
						)
					)
				)
			)
		)
	)
)

fn kx_insertkeyspace obj framestart insertsize =
(
	/*
	work through the objects in the scene and move the keys defined at and
	beyond the specified location out the number of frame
	*/

	local cntrl
	local propnames
	local mctrl

	cntrl = #(undefined, undefined, undefined)
	-- get the standard controllers
	try (
		cntrl[1] = obj.pos.controller
	) catch (
		cntrl[1] = undefined
	)
	try (
		cntrl[2] = obj.rotation.controller
	) catch (
		cntrl[2] = undefined
	)

	try (
		cntrl[3] = obj.scale.controller
	) catch (
		cntrl[3] = undefined
	)

	-- move the standard controllers
	for c in cntrl do
	(
		if c != undefined then
		(
			for k in c.keys do
			(
				--print k.time
				--print framestart
				if k.time >= framestart then
				(
					k.time += insertsize
				)
			)
			sortKeys c
		)
	)

	-- now deal with modifiers on obj with animated properties
	for m in obj.modifiers do
	(
		propnames = getPropNames m
		for prop in propnames do
		(
			mctrl = getPropertyController m prop
			-- if there is a controller then deal with its keys
			if mctrl != undefined then
			(
				for k in mctrl.keys do
				(
					--print k.time
					--print framestart
					if k.time >= framestart then
					(
						k.time += insertsize
					)
				)
				sortKeys mctrl
			)
		)
	)
)

-- Anim key copy function
fn kx_copyanimkeys node nodetrg framestart frameend frametarget typeslist relabs mirror =
(
	local fr, idx, p, r, s, nf, tchild
	
	local dopos = typeslist[1]
	local dorot = typeslist[2]
	local doscale = typeslist[3]
	local domod = typeslist[4]
	
	local sf = framestart     -- start frame
	local ef = frameend       -- end frame
	local tf = frametarget    -- target frame
	local span = ef - sf      -- source frames span (interval)
	
	local existingPosKey
	local existingPos
	local newPosKey
	
	--format "+++++++++++++ Anim Key Copy ++++++++++++++\r\n"
	-- we have valid data so lets get into it.
	-- 1. Work through the selected nodes
	--format "Node: %: Target:% Copy Type: %\r\n" node.name nodetrg.name relabs
	-- work through the frames in the source interval and look for keys
	-- which are then copied to the target range
	
	-- Process the pos data
	local masterpos_src = undefined
	if dopos then
	(
		--format "copy pos data\r\n"
		for fr = sf to ef do
		(
			idx = getKeyIndex node.pos.controller fr
			if idx > 0 then    -- we have an index!
			(
				in coordsys parent
				(
					local masterpos_src_delta = undefined
					
					-- check to see if is first pass on this obj.
					-- If so setup delta vars, else calc them as normal
					if masterpos_src == undefined then (
						at time sf
						(
							masterpos_src = node.pos
							masterpos_src_delta = masterpos_src - masterpos_src
						)
					) else
					(
						masterpos_src_delta = (at time fr node.pos) - masterpos_src
					)
					case relabs of
					(
						1: at time fr p = node.pos
						2: p = nodetrg.pos + masterpos_src_delta
					)
					
					nf = tf+(fr-sf)
					--format "Source fr:%  Target fr:%  Pos:%\r\n" fr nf p
					with animate on at time nf nodetrg.pos = p
				)
			)
		)   -- end for fr
	)
	
	-- process rotation data
	local masterrot_src = undefined
	if dorot then
	(
		--format "copy rot data\r\n"
		for fr = sf to ef do
		(
			idx = getKeyIndex node.rotation.controller fr
			if idx > 0 then    -- we have an index!
			(
				in coordsys parent
				(
					local masterrot_src_delta = undefined
					
					-- check to see if is first pass on this obj.
					-- If so setup delta vars, else calc them as normal
					if masterrot_src == undefined then (
						at time sf
						(
							masterrot_src = node.rotation
							masterrot_src_delta = masterrot_src - masterrot_src
						)
					)
					else
					(
						masterrot_src_delta = (at time fr node.rotation) - masterrot_src
					)
					
					case relabs of
					(
						1: at time fr r = node.rotation
						2: r = nodetrg.rotation + masterrot_src_delta
					)
					
					-- deal with the mirroring calcs
					if mirror[1] then
					(
						-- we need to convert to euler rots for easy mirroring.
						-- Might be a better way with quats??
						local r_euler = quatToEuler r order:1
						--format "Processing mirror code: euler=%\r\n" r_euler
						if mirror[2] then (
							-- mirror along the x axis
							r_euler.x *= -1
						)
						if mirror[3] then (
							-- mirror along the y axis
							r_euler.y *= -1
						)
						if mirror[4] then (
							-- mirror along the z axis
							r_euler.z *= -1
						)
						
						--format "Finished mirror code: euler=%\r\n" r_euler
						-- turn the euler back to a quat
						r = eulerToQuat r_euler order:1
					)

					-- end mirroring code
					nf = tf+(fr-sf)
					existingPosKey = getKeyIndex nodetrg.pos.controller nf
					existingPos = [0,0,0]
					if (existingPosKey > 0) then
					(
						existingPos = nodetrg.pos.keys[existingPosKey].value
					)
					--format "Source fr:%  Target fr:%  Pos:%\r\n" fr nf r
					with animate on at time nf nodetrg.rotation = r
					
					if (existingPosKey == 0) then
					(
						--format "existingPosKey==0\n"
						newPosKey = getKeyIndex nodetrg.pos.controller nf
						if (newPosKey != 0) then deleteKey nodetrg.pos.controller newPosKey
					)
					else
					(
						--format "existingPosKey!=0\n"
						--format "existingPos=%; existingPosKey=%\n" existingPos existingPosKey
						nodetrg.pos.keys[existingPosKey].value = existingPos
					)
				)
			)
		)   -- end for fr
	)
		
	-- process scale data
	relabs = 1 -- Override supplied value - scale values are always to be taken absolute
	local masterscl_src = undefined
	if doscale then
	(
		for fr = sf to ef do
		(
			idx = getKeyIndex node.scale.controller fr
			if idx != 0 then    -- we have an index!
			(
				in coordsys parent
				(
					-- check to see if is first pass on this obj.
					-- If so setup delta vars, else calc them as normal
					local masterscl_src_delta = undefined
					if masterscl_src == undefined then 
					(
						at time sf
						(
							masterscl_src = node.scale
							masterscl_src_delta = masterscl_src - masterscl_src -- this is good because we get the 0 of any data type
						)
					)
					else masterscl_src_delta = (at time fr node.scale) - masterscl_src
					
					case relabs of
					(
						1: at time fr s = node.scale
						2: s = nodetrg.scale + masterscl_src_delta
					)
					
					nf = tf + fr - sf
					with animate on at time nf node.scale = s
				)
			)
		)   -- end for fr
	)
	
	relabs = 1 -- Override supplied value - modifier values are always to be taken absolute
	local masterval_src = undefined
	if domod then
	(
		-- now deal with modifiers on obj with animated properties
		for m in node.modifiers do
		(
			local m2 = nodetrg.modifiers[m.name]
			if m2 == undefined then continue
			
			propnames = getPropNames m
			for prop in propnames do
			(
				mctrl = getPropertyController m prop
				mctrl2 = getPropertyController m2 prop
				
				-- if there is a controller on both nodes then deal with its keys
				if mctrl != undefined and mctrl2 != undefined then
				(
					masterval_src = undefined
					for fr = sf to ef do
					(
						idx = getKeyIndex mctrl fr
						if idx > 0 then    -- we have an index!
						(
							in coordsys parent
							(
								-- check to see if is first pass on this obj.
								-- If so setup delta vars, else calc them as normal
								local masterval_src_delta = undefined
								if masterval_src == undefined then 
								(
									at time sf
									(
										masterval_src = mctrl.value
										masterval_src_delta = masterval_src - masterval_src -- this is good because we get the 0 of any data type
									)
								)
								else masterval_src_delta = (at time fr mctrl.value) - masterval_src
								
								case relabs of
								(
									1: at time fr p = mctrl.value
									2: p = mctrl2.value + masterval_src_delta
								)
								
								nf = tf + fr - sf
								with animate on at time nf mctrl2.value = p
							)
						)
					)   -- end for fr
				)
			)
		)
	)
)

fn kx_sceneContainsDplNames =
(
	for n = 1 to $objects.count do
	(
		for n2 = (n+1) to $objects.count do
		(
			if kx_lowercase $objects[n].name == kx_lowercase $objects[n2].name then return true
		)
	)
	return false
)

global kx_animkeycopy_flag
-- parts anim mapper gui
rollout kx_animkeycopy "Animation Key Ops" width:491 height:436
(
    local oTargetBase = undefined

	fn is_modelbase obj =
	(
    	(iskindof obj odysseybase)
	)

	GroupBox grp1 "Sources to Targets" pos:[4,7] width:154 height:430
	listbox lst_mdlobjs "Selected Nodes (Source)" pos:[13,26] width:136 height:13
	listbox lst_trgnobhs "Target Nodes" pos:[13,223] width:136 height:12
	button btn_refresh "Refresh Node Lists" pos:[20,408] width:121 height:21


	GroupBox grp4 "Animation Key Operation" pos:[163,7] width:323 height:233
	radiobuttons rdo_operation "Operation:" labels:#("Copy", "Move", "Delete", "Insert") default:1 columns:1 pos:[178,30] 
	radiobuttons rdo_operateon "Operate on:" labels:#("Keys", "Frames") default:1 columns:1 pos:[248,30]
    spinner spn_startframe "Start Frame:" range:[0,100000,0] type:#integer pos:[327,38] fieldwidth:80
    spinner spn_endframe  "End Frame:" range:[0,100000,0] type:#integer pos:[330,58] fieldwidth:80
    spinner spn_targetframe "Target Frame:" range:[0,100000,0] type:#integer pos:[318,78] fieldwidth:80
	label lbl_ctrltypes "Controller types:" pos:[173,110]
	checkbox chk_pos "Position" checked:true pos:[178,130]
	checkbox chk_rot "Rotation" checked:true pos:[248,130]
	checkbox chk_scl "Scale" checked:true pos:[178,150]
	checkbox chk_mod "Modifier" checked:true pos:[248,150]
	radiobuttons rdo_abs labels:#("Absolute","Relative") default:1 columns:2 pos:[173,170]
	checkbox chk_mirror "Mirror Rotation on Axes" checked:false enabled:true pos:[333,130]
	checkbox chk_x "X" enabled:true pos:[348,150]
	checkbox chk_y "Y" enabled:true pos:[388,150]
	checkbox chk_z "Z" enabled:true pos:[428,150]
	button btn_ok "Go" pos:[253,200] width:140 height:26
	
	GroupBox grp3 "Mappings: Source --> Target" pos:[163,247] width:324 height:190
	button btn_addmap ">>" pos:[173,270] width:40 height:28
	button btn_delmap "<<" pos:[173,300] width:40 height:28
	button btn_automap "Auto" pos:[173,330] width:40 height:28
	button btn_mapimport "Import" pos:[173,360] width:40 height:28
	button btn_mapexport "Export" pos:[173,390] width:40 height:28
	listbox lst_mappings "" pos:[220,263] width:259 height:12
	
	on rdo_operation changed val do
	(
		if val == 4 then 
		(
			rdo_operateon.state = 2
			chk_pos.state = true
			chk_rot.state = true
			chk_scl.state = true
			chk_mod.state = true
			chk_pos.enabled = false
			chk_rot.enabled = false
			chk_scl.enabled = false
			chk_mod.enabled = false
		)
		
		if val > 2 then 
		(
			rdo_abs.enabled = false
			chk_mirror.enabled = false
			chk_x.enabled = false
			chk_y.enabled = false
			chk_z.enabled = false
		)
		else
		(
			rdo_abs.enabled = true
			if chk_rot.checked then
			(
				chk_mirror.enabled = true
				chk_x.enabled = true
				chk_y.enabled = true
				chk_z.enabled = true
			)
		)
		
		if val > 2 then spn_targetframe.enabled = false
		else spn_targetframe.enabled = true
	)
	
	on rdo_operateon changed val do
	(
		if val == 1 then
		(
			if rdo_operation.state == 4 then
			(
				rdo_operation.state = 1
				spn_targetframe.enabled = true
				rdo_abs.enabled = true
				chk_mirror.enabled = true
				chk_x.enabled = true
				chk_y.enabled = true
				chk_z.enabled = true
			)
			chk_pos.enabled = true
			chk_rot.enabled = true
			chk_scl.enabled = true
			chk_mod.enabled = true
		)
		else
		(
			chk_pos.state = true
			chk_rot.state = true
			chk_scl.state = true
			chk_mod.state = true
			chk_pos.enabled = false
			chk_rot.enabled = false
			chk_scl.enabled = false
			chk_mod.enabled = false
			if rdo_operation.state < 3 then
			(
				chk_mirror.enabled = true
				chk_x.enabled = true
				chk_y.enabled = true
				chk_z.enabled = true
			)
		)
	)
	
	on chk_rot changed val do
	(
		if rdo_operation.state < 3 then
		(
			chk_mirror.enabled = val
			chk_x.enabled = val
			chk_y.enabled = val
			chk_z.enabled = val
		)
	)
	
	on spn_startframe changed val do
	(
    	if val > spn_endframe.value then (
    	    spn_endframe.value = val
	    )
	)
	
	on spn_endframe changed val do
	(
    	if val < spn_startframe.value then (
        	spn_startframe.value = val
    	)
	)

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
	)

	fn RefreshTargetNodes =
	(
		-- load scene objects
		local temp = #()
		for o in $objects do
		(
			append temp o.name
		)
		lst_trgnobhs.items = temp
		
		return true
    )

	fn RefreshSourceNodes =
	(
		-- load selected objects
		local temp = #()
		for o in $selection do
		(
			append temp o.name
		)
		lst_mdlobjs.items = temp
		
		return true
    )

	on kx_animkeycopy open do
	(
        RefreshSourceNodes()
        RefreshTargetNodes()

		kx_animkeycopy_flag = true
	)

	on kx_animkeycopy close do
	(
    	kx_animkeycopy_flag = false
	)

	on btn_refresh pressed do
	(
        RefreshSourceNodes()
        RefreshTargetNodes()
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
	
	on btn_ok pressed do
	(
    	local sf = spn_startframe.value     -- start frame
    	local ef = spn_endframe.value       -- end frame
    	local tf = spn_targetframe.value    -- target frame
    	local span = ef - sf                -- source frames span (interval)
		
    	-- Perform sanity check on parameters
		if kx_sceneContainsDplNames() then
		(
			messagebox "The scene contains several objects with the same name (case insensitive). Please make them different for this tool to work." title:"Duplicate object names"
        	return 0
		)
    	if (tf < ef) and (tf > (sf-span)) then
    	(
        	-- error message
        	local errortxt = "Target frame and resulting copy overlaps source range. Are you sure you want to continue?"
        	if querybox errortxt title:"Source/Target Range Overlap" then return 0
    	)
    	if chk_mirror.checked and not (chk_x.checked or chk_y.checked or chk_z.checked) then
    	(
        	-- error message
        	local errortxt = "You must select a mirror axis."
        	messagebox errortxt title:"Undefined Mirror Axis"
        	return 0
    	)
		
		if lst_mappings.items.count == 0 then
		(
			if rdo_operation.state > 2 then lst_mappings.items = for sel in lst_mdlobjs.items collect (sel + " --> " + sel)
			else
			(
				messagebox ("No mappings have been provided for this operation.") title:"No mappings"
				return false
			)
		)
    	-- Sanity checks finished
    	
    	-- Set the new animationRange if it's not big enough at the moment
		if rdo_operateon.state == 2 then
		(
			if rdo_operation.state != 3 then animationRange = (interval 0 (animationRange.end + span + 100))
		)
    	else if animationRange.end < (tf+span) then animationRange = (interval 0 (tf + span + 100))
		
    	clearListener()
		
		for m in lst_mappings.items do
		(
			local mtoks = filterString m " ->"
			
			local oSource = getNodeByName mtoks[1]
			if oSource == undefined then
			(
				messagebox ("Could not find source node: " + mtoks[1] + ".") title:"Could not find source node"
				continue
			)
			
			if rdo_operation.state == 1 or rdo_operation.state == 2 then
			(
				-- Copy the keys to the new node and frame
				local oTarget = getNodeByName mtoks[2]
				if oTarget == undefined then
				(
					messagebox ("Could not find target node: " + mtoks[2] + ".") title:"Could not find target node"
					continue
				)
				
				-- If we're operating on frames, then create frames for where we'll be inserting
				if rdo_operateon.state == 2 then kx_insertkeyspace oTarget tf span
				
				-- Copy animation keys from source to target
				local typeslist = #(chk_pos.state,chk_rot.state,chk_scl.state,chk_mod.state)
				kx_copyanimkeys oSource oTarget sf ef tf typeslist rdo_abs.state #(chk_mirror.checked,chk_x.checked,chk_y.checked,chk_z.checked)
			)
			
			if rdo_operation.state == 2 or rdo_operation.state == 3 then
			(
				-- Delete the keys in the source node
				local typeslist = #(chk_pos.state,chk_rot.state,chk_scl.state,chk_mod.state)
				if rdo_operateon.state == 2 then typeslist = #(true,true,true,true)
				kx_deletekeysinblock oSource sf ef typeslist
				
				-- If we're operating on frames, then delete frames as well
				if rdo_operateon.state == 2 then kx_insertkeyspace oSource ef (-span)
			)
			
			if rdo_operation.state == 4 and rdo_operateon.state == 2 then
			(
				-- We're inserting frames
				kx_insertkeyspace oSource sf (span)
			)
		)
	)
)

if kx_animkeycopy_flag != true then
(
	if kx_sceneContainsDplNames() then messagebox "The scene contains several objects with the same name (case insensitive). Please make them different for this tool to work." title:"Object name error"
    else createdialog kx_animkeycopy style:#(#style_titlebar, #style_border, #style_sysmenu, #style_minimizebox)
)