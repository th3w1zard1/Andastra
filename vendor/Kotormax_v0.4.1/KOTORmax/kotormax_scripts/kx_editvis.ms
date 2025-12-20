/*-----------------------------------------------------------------------------\

    Visibility editor
	
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

global kx_editvis_flag
rollout kx_editvis "Edit Visibility"
(
	listbox lst_rooms " Rooms:" pos:[5, 5] width:120 across:2
	listbox lst_vis " Visible:" pos:[130, 5] width:120
	local ObjectVisibility = #()
	
	fn getRooms =
	(
		-- by not getting the Rooms from the list we are making the assumption that the scene won't change during this dialog
		return for room in $helpers where iskindof room odysseybase and room.layouttype == 2 collect room
	)
	
	fn getSelectedRoom =
	(
		-- the returned room should be unique because otherwise its name shouldn't be included on the list in the first place
		return (for room in getRooms() where room.name == lst_rooms.selected collect room)[1]
	)
	
	fn updateVisibility allvisible:false =
	(	
		local Rooms = getRooms()
		
		if allvisible then for room in Rooms do kx_unhideSubtree room
		else 
		(
			-- unhide selected room
			local selectedroom = (for obj in Rooms where obj.name == lst_rooms.selected collect obj)[1]
			if selectedroom != undefined then kx_unhideSubtree selectedroom
		
			-- process unselected rooms
			for sItem in lst_vis.items do
			(
				-- filter listbox item into tokens
				local tokens = filterString sItem ": "
				
				-- get room and if it isn't valid, skip this item
				local room = (for obj in Rooms where obj.name == tokens[1] collect obj)[1]
				if room == undefined then continue
				
				-- do the hiding and unhiding 
				if tokens[2] == "yes" then kx_unhideSubtree room
				else kx_hideSubtree room
			)
		)
	)
	
	fn rebuildVisibilityList base =
	(
		if base == undefined then return false
		
		local Rooms = getRooms()
		
		-- first get rid of any invalid nodes that may be present in the base's data
		base.visrooms = for sName in base.visrooms where (getNodeByName sName) != undefined and iskindof (getNodeByName sName) odysseybase and (getNodeByName sName).layouttype == 2 collect sName
		
		-- now we write out the data to an array. 
		local sItems = #()
		for room in Rooms do
		(
			if room == base then continue
			if findItem base.visrooms room.name > 0 then append sItems (room.name + ": yes")
			else append sItems (room.name + ": no")
		)
		
		-- update visibility list
		lst_vis.items = sItems
		
		-- update visibility
		updateVisibility()
	)
	
	fn flipSelectedVisibility base =
	(
		if base == undefined then return false
	
		local Rooms = getRooms()
		local sItems = lst_vis.items
		
		-- filter listbox item into tokens
		local tokens = filterString sItems[lst_vis.selection] ": "
		
		-- get room and if it isn't valid, return
		local room = (for obj in Rooms where obj.name == tokens[1] collect obj)[1]
		if room == undefined then
		(
			messagebox "Error in flipSelectedVisibility(): variable for room with name from token is undefined."
			return false
		)
		
		local nIndex = findItem base.visrooms room.name
		
		-- do the hiding and unhiding 
		if tokens[2] == "yes" then
		(
			if nIndex > 0 then deleteItem base.visrooms nIndex
			kx_hideSubtree room
			sItems[lst_vis.selection] = room.name + ": no"
		)
		else
		(
			if nIndex == 0 then append base.visrooms room.name
			kx_unhideSubtree room
			sItems[lst_vis.selection] = room.name + ": yes"
		)
		
		-- update visibility list
		lst_vis.items = sItems
	)
	
	on lst_vis doubleclicked sel do
	(
		-- get currently selected room
		local base = getSelectedRoom()
		
		if base != undefined then
		(
			lst_vis.selection = sel
			flipSelectedVisibility base
		)
		else
		(
			messagebox ("Error: could not find room: "+lst_rooms.items[sel]+".")
			return false
		)
	)
	
	on lst_rooms selected sel do
	(
		-- get currently selected room
		local base = getSelectedRoom()
		
		if base != undefined then
		(
			lst_vis.selection = 0
			rebuildVisibilityList base
			select base
		)
		else
		(
			messagebox ("Error: could not find room: "+lst_rooms.items[sel]+".")
			return false
		)
	)
	
	on kx_editvis open do
	(
		kx_editvis_flag = true
		clearSelection()
		max create mode 
		lst_rooms.selection = 0
		lst_vis.selection = 0
		
		-- get all rooms
		local Rooms = getRooms()
		--print Rooms
		local RoomNames = #()
		local bDuplicates = false
		
		-- now append them to the array while checking for duplicates
		for room in Rooms do
		(
			-- check for duplicates
			if not bDuplicates then if findItem RoomNames room.name > 0 then bDuplicates = true
			if bDuplicates then exit
			
			-- append the names
			append RoomNames room.name
		)
		
		-- update the listbox if no duplicates were found
		if not bDuplicates then lst_rooms.items = RoomNames
		else messagebox "Two or more rooms in the scene have the exact same name! Make sure all names are unique and try again!"
		
		-- record visibility for all nodes
		ObjectVisibility = for obj in $objects collect #(obj, obj.isHidden)
	)
	
	on kx_editvis close do
	(
		updateVisibility allvisible:true
		for objvis in ObjectVisibility do
		(
			if objvis[2] then hide objvis[1]
			else unhide objvis[1]
		)
		ObjectVisibility = #()
		kx_editvis_flag = false
	)
)

if kx_editvis_flag != true then
(
	-- get all rooms
	local Rooms = for room in $helpers where iskindof room odysseybase and room.layouttype == 2 collect room
	if Rooms.count < 1 then
	(
		messagebox "Found no rooms in the current scene. Make sure your rooms use the 'Room' layout type!"
	)
	else
	(
		--format "Firing edit visibility dlg!\n"
		createDialog kx_editvis width:255 modal:true
	)
)