/*-----------------------------------------------------------------------------\

    Constraint Editor

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

global kx_constraintedit_flag
rollout kx_constraintedit "Constraint Editor"
(
	local ShapeArray = #()
    local nodesel = undefined
	local flexMod = undefined

	-- gui
	--multilistbox list_links pos:[5,5] height:10 width:140
	--button btn_removetrans "Remove Link(s)" pos:[5,145] width:140 toolTip:"Removes selected link(s)."

	--spinner spn_transition "Room:" range:[-1,100,-1] pos:[175,10] width:60 type:#integer
	--button btn_addtrans "Apply" pos:[239,7] width:50 toolTip:"Adds room link to the currently selected edge(s)."
	--listbox lst_rooms pos:[150,30] height:10 width:140 selection:0
	
	group "Relative"
	(
		spinner spn_strength "Strength:" range:[-100,100,10] type:#integer
		button btn_affect "Affect" toolTip:"Affect the constraints for the selected vertices by the value in Strength." width:154
	)
	group "Absolute"
	(
		slider sld_vertweight "Weight" range:[1,255,255] type:#integer ticks:20
		spinner spn_weight "Weight:" range:[1,255,255] type:#integer
		button btn_apply "Apply" across:2 toolTip:"Applies the selected constraint value to all the selected vertices." width:75
		button btn_remove "Remove" toolTip:"Removes the constraint value from the selected vertices." width:75
		checkbox chk_automatically "Apply automatically" checked:false
	)
	group "Displacement Boxes"
	(
		checkbox box_showdispl "Show displacement range" width:146 height:15
		radiobuttons radio_displtype "" width:108 height:32 labels:#("Max displacement", "Scaled by weight") columns:1
		button btn_refresh "Manual Refresh" toolTip:"Force the display boxes to refresh."
	)
	group "Current values"
	(
		button btn_read "Read Values" toolTip:"Reads the constraint values of the currently selected vertices." width:154
		listbox lst_values height:10
	)

	-- actions
	
	-- this function will make sure we are in the right node, its mesh baseobject and edge subobject
	-- if all checks out, then it returns the baseobject, otherwise it returns undefined
	fn GetValidBaseObject =
	(
		-- get selected node, and make sure it's a mesh, otherwise return
		if nodesel == undefined then return undefined
		if modpanel.getcurrentobject() == undefined then
		(
			messagebox "Error: Modify Panel not open or nothing is selected."
			return undefined
		)
		if nodesel != selection[1] then
		(
			messagebox "Error: The selected node is not the node for which the data is open."
			return undefined
		)
		if not iskindof nodesel.baseobject editable_mesh and not iskindof nodesel.baseobject trimesh then
		(
			messagebox "Error: Node base object is not an Editable Mesh."
			return undefined
		)
		if modpanel.getcurrentobject() != nodesel.baseobject then
		(
			messagebox "Error: A modifier is selected instead of the Editable Mesh base object."
			return undefined
		)
		if subobjectlevel != 1 then
		(
			messagebox "Error: Not in Vertex Editing Mode."
			return undefined
		)
		return nodesel
	)
	
	--
	-- Creates a set of boxes to show the degree of danglymesh
	-- flex there will be.
	--
	function redrawDangleGizmos = (
		local mesh = GetValidBaseObject()
		if mesh == undefined then return false
		
		--clearListener()
		--format "------ Danglymesh UI ------\r\n"
		
		-- if we're not supposed to show the boxes, or the mesh is invalid, delete the boxes if there are any
		if not box_showdispl.checked or mesh == undefined then
		(
			-- delete the array of existing boxes
			for e in ShapeArray do delete e
			ShapeArray = #()
			return 0
		)

		-- rebuild the array of shapes
		local radiusSize = flexMod.displacement * 10
		for v in mesh.verts do --mesh.selectedVerts do		-- old: $.Verts
		(
			try
			(
				local val = (((meshop.getMapVert mesh kx_constraintChannel v.index) * 255.0) as color).red --getvertcolor $ v.index
				
				local scale
				if radio_displtype.state == 1 then scale = 1
				else scale = 1 - val/255
				
				local boxcolor = color 0 0 0
				local ratio = (val - (val as integer / 100) * 100) / 100.0
				if val < 100 then
				(
					boxcolor.r = (0.0 + 0.5 * ratio) * 255.0
					boxcolor.g = (0.0 + 0.5 * ratio) * 255.0
					boxcolor.b = (1.0 - 0.5 * ratio) * 255.0
				)
				else if val < 200 then
				(
					boxcolor.r = (0.5 + 0.5 * ratio) * 255.0
					boxcolor.g = (0.5 + 0.5 * ratio) * 255.0
					boxcolor.b = (0.5 - 0.5 * ratio) * 255.0
				)
				else if val < 300 then
				(
					boxcolor.r = (1.0) * 255.0
					boxcolor.g = (1.0 - 1.0 * ratio) * 255.0
					boxcolor.b = (0.0) * 255.0
				)
				
				local boxside = scale * radiusSize * 10 * 2
				if val == 0 then boxside = 0
				
				local s
				if v.index > ShapeArray.count then
				(
					--s = dummy position:(v.position) boxsize:[boxside,boxside,boxside]
					local s = point position:v.position size:boxside axistripod:false centermarker:true cross:false box:true constantscreensize:false drawontop:false wirecolor:boxcolor
					
					--format "vertex#:% scale=% vertcolour=% radius=% boxside=%\r\n" (v.index) scale vertcolour radiusSize boxside
					--if s == undefined then format "dummy create failed\r\n"
					
					append ShapeArray s
				)
				else
				(
					s = ShapeArray[v.index]
					s.position = v.position
					s.size = boxside
					s.wirecolor = boxcolor
				)
			) 
			catch
			(
				--format "Danglymesh warning - no colour assigned to any vertex.\r\n"
			)
		)
		
		for n = ShapeArray.count to mesh.verts.count by -1 do
		(
			delete ShapeArray[n]
			deleteItem ShapeArray n
		)
	)
	
	on btn_read pressed do
	(
		local mesh = GetValidBaseObject()
		if mesh == undefined then return false
		
		local listItems = #()
		for v in mesh.selectedVerts do
		(
			local val = (((meshop.getMapVert mesh kx_constraintChannel v.index) * 255.0) as color).red
			local s = "Vertex " + v.index as string + ": " + (val as integer) as string
			append listItems s
		)
		lst_values.items = listItems
	)
	
	on lst_values doubleclicked ind do
	(
		local mesh = GetValidBaseObject()
		if mesh == undefined then return false
		
		local sTokens = #()
		sTokens = filterString lst_values.items[ind] " :"
		
		mesh.selectedVerts = #(sTokens[2] as integer)
	)
	
	on btn_affect pressed do
	(
		local mesh = GetValidBaseObject()
		if mesh == undefined then return false
		
		disableSceneRedraw() 
		for v in mesh.selectedverts do
		(
			local val = (((meshop.getMapVert mesh kx_constraintChannel v.index) * 255.0) as color).red
			local newval = val + spn_strength.value
			if newval > 255 then newval = 255
			if newval < 1 then newval = 1
			if val == 0 then newval = 0
			meshop.setMapVert mesh kx_constraintChannel v.index [newval/255.0,0,0]
			--meshop.setvertcolor mesh kx_constraintChannel v.index (color val 0 0)
		)
		enableSceneRedraw() 
		redrawDangleGizmos()
	)
	
	fn ApplyToSelection val =
	(
		local mesh = GetValidBaseObject()
		if mesh == undefined then return false
		
		-- update the colour of the selected verts in the current mesh
		disableSceneRedraw() 
		for v in mesh.selectedverts do
			meshop.setMapVert mesh kx_constraintChannel v.index [val/255.0,0,0]
			--meshop.setvertcolor mesh kx_constraintChannel v.index (color val 0 0)
		enableSceneRedraw() 

		redrawDangleGizmos()
	)
	
	on box_showdispl changed newval do
	(
		radio_displtype.enabled = newval
		flexMod.showdispl = newval
		redrawDangleGizmos()
	)
	
	on radio_displtype changed newval do
	(
		flexMod.displtype = newval
		redrawDangleGizmos()
	)
	
	on sld_vertweight changed val do
	(
		spn_weight.value = val
		
		if chk_automatically.checked then ApplyToSelection val
	)
	
	on spn_weight changed val do
	(
		sld_vertweight.value = val
		
		if chk_automatically.checked then ApplyToSelection val
	)
	
	on btn_apply pressed do
	(
		ApplyToSelection spn_weight.value
	)
	
	on btn_remove pressed do
	(
		ApplyToSelection 0
	)
	
	on btn_refresh pressed do
	(
		redrawDangleGizmos()
	)
		
	on kx_constraintedit open do
	(
		if modpanel.getcurrentobject() == undefined then return false
		nodesel = selection[1]
		mesh = nodesel
		flexMod = mesh.Modifiers["OdysseyFlex"]
		modpanel.setcurrentobject nodesel.baseobject
		subobjectlevel = 1
		
		-- setup the channel if needed
		kx_enableMapChannel mesh kx_constraintChannel
		kx_enableMapChannel mesh kx_constraintChannel
		meshop.setNumMapVerts mesh kx_constraintChannel mesh.verts.count keep:true
		/*
		if((meshop.getNumMapVerts mesh kx_constraintChannel) == 0 or (meshop.getNumMapFaces mesh kx_constraintChannel) == 0) then (
			meshop.setNumMapVerts mesh kx_constraintChannel 1
			meshop.setMapVert mesh kx_constraintChannel 1 [1, 1, 1]
			meshop.buildMapFaces mesh kx_constraintChannel
			for i = 1 to (meshop.getMapFace mesh kx_constraintChannel) do (
				meshop.setMapFace mesh kx_constraintChannel i [1, 1, 1]
			)
		)
		*/
		
		callbacks.addScript #selectionSetChanged "(DestroyDialog kx_constraintedit)" id:#selChange_constraintEdit
		
		box_showdispl.checked = flexMod.showdispl
		radio_displtype.state = flexMod.displtype
		radio_displtype.enabled = flexMod.showdispl
		
		redrawDangleGizmos()
		
    	kx_constraintedit_flag = true
	)

	on kx_constraintedit close do
	(
		callbacks.removeScripts #selectionSetChanged id:#selChange_constraintEdit
		for e in ShapeArray do delete e
		ShapeArray = #()
    	kx_constraintedit_flag = false
	)
)

if kx_constraintedit_flag != true then
(
    createDialog kx_constraintedit width:180
)
