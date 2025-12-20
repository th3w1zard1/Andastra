/*-----------------------------------------------------------------------------\

    Order Rooms

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

global kx_orderrooms_flag
rollout kx_orderrooms "Order Rooms"
(
	-- gui
	listbox lst_rooms pos:[5, 5] height:16 width:180
	button btn_up "Move Up" width:70 across:2
	button btn_down "Move Down" width:70

	on btn_up pressed do
	(
		local RoomNames = lst_rooms.items
		local nSel = lst_rooms.selection
		if nSel > 1 and nSel <= RoomNames.count then
		(
			-- swap items
			local sSelected = RoomNames[nSel]
			local sSwapped = RoomNames[nSel - 1]
			RoomNames[nSel - 1] = sSelected
			RoomNames[nSel] = sSwapped
			
			-- set the new room list and set the right selection
			lst_rooms.items = RoomNames
			lst_rooms.selection = nSel - 1
		)
	)
	
	on btn_down pressed do
	(
		local RoomNames = lst_rooms.items
		local nSel = lst_rooms.selection
		if nSel > 0 and nSel < RoomNames.count then
		(
			-- swap items
			local sSelected = RoomNames[nSel]
			local sSwapped = RoomNames[nSel + 1]
			RoomNames[nSel + 1] = sSelected
			RoomNames[nSel] = sSwapped
			
			-- set the new room list and set the right selection
			lst_rooms.items = RoomNames
			lst_rooms.selection = nSel + 1
		)
	)
	
	on kx_orderrooms open do
	(
		kx_orderrooms_flag = true
		clearSelection() 
		max create mode
		
		-- get all rooms
		local Rooms = for room in $helpers where iskindof room odysseybase and room.layouttype == 2 collect room
		--print Rooms
		local RoomNames = #()
		local bDuplicates = false
		
		-- find max room number
		local LayoutNumArray = #()
		for room in Rooms where room.layoutnum > -1 do append LayoutNumArray room.layoutnum
		sort LayoutNumArray
		
		-- first append all the rooms that have a non -1 number
		for i in LayoutNumArray do
		(
			-- get all rooms with this layoutnum
			local CurrentRoomNames = for room in Rooms where (room.layoutnum == i) collect room.name
			
			if CurrentRoomNames.count == 0 then continue
			else if CurrentRoomNames.count > 1 then
			(
				-- warn the user that there are several users with the same layoutnum
				local sMessage = stringstream ""
				format "Several rooms with room number % have been found: %. You will have to double check all the room links on the relevant walkmeshes after ordering." i CurrentRoomNames to:sMessage
				messagebox (sMessage as string)
			)
			
			-- check for duplicates
			if not bDuplicates then for sName in CurrentRoomNames do if findItem RoomNames sName > 0 then bDuplicates = true
			if bDuplicates then exit
			
			-- append the names
			join RoomNames CurrentRoomNames
		)
		
		-- now append those that do have a -1 num
		for room in Rooms where room.layoutnum == -1 do
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
	)
	
	on kx_orderrooms close do
	(
		kx_orderrooms_flag = false
		
		local Rooms = for room in $helpers where iskindof room odysseybase and room.layouttype == 2 collect room
		local RoomNames = lst_rooms.items
		local LinkArray = #()
		
		-- update bases and build link array
		for sName in RoomNames do
		(
			-- find new and old nums
			local room = (for room in Rooms where room.name == sName collect room)[1]
			if room != undefined then
			(
				local nOldNum = room.layoutnum
				local nNewNum = findItem RoomNames sName - 1
				
				-- append the pair to link array
				append LinkArray #(nOldNum, nNewNum)
				--format "New link pair: %\n" #(nOldNum, nNewNum)
				
				-- update layoutnum on room
				room.layoutnum = nNewNum
			)
		)
		
		-- update walkmeshes
		if RoomNames.count > 0 then
		(
			local Walkmeshes = for obj in objects where iskindof obj.baseobject editable_mesh and obj.modifiers["OdysseyWalkmesh"] != undefined and obj.modifiers["OdysseyWalkmesh"].meshtype == 1 collect obj
			for wm in Walkmeshes do
			(
				if not kx_isMapChannelEnabled wm kx_roomlinkChannel or meshop.getNumMapVerts wm kx_roomlinkChannel < 1 then continue -- skip this walkmesh
				for i = 1 to meshop.getNumMapVerts wm kx_roomlinkChannel do
				(
					local ColorVert = meshop.getMapVert wm kx_roomlinkChannel i
					if ColorVert.y != 1.0 then
					(
						for link in LinkArray do
						(
							local fOld = (100.0 + (link[1] as float))/255.0
							local fNew = (100.0 + (link[2] as float))/255.0
							--format "Comparing color % to % (new: %)\n" ColorVert.y fOld fNew
							if ColorVert.y == fOld then
							(
								meshop.setMapVert wm kx_roomlinkChannel i [0,fNew,0]
								--format "Found a match! This should be the new color vert: %\n" (meshop.getMapVert wm 0 i)
								exit
							)
						)
					)
				)
			)
		)
		
		-- if a base is currently open in the mod panel, update
		---- this should be obsolete now, as we clear the selection before this anyway
		if modpanel.getcurrentobject() != undefined and iskindof selection[1] odysseybase and selection[1].layouttype == 2 then 
			modpanel.setcurrentobject selection[1]
	)
)

if kx_orderrooms_flag != true then
(
	-- get all rooms
	local Rooms = for room in $helpers where iskindof room odysseybase and room.layouttype == 2 collect room
	if Rooms.count < 1 then
	(
		messagebox "Found no rooms in the current scene. Make sure your rooms use the 'Room' layout type!"
	)
	else
	(
		--format "Firing orderrooms dialog!\n"
		createDialog kx_orderrooms width:190 modal:true
	)
)
