/*-----------------------------------------------------------------------------\

	MDL Flex Modifer
	
	KOTORmax
	by bead-v
	
	Derived from:
    NWmax
    by Joco (jameswalker@clear.net.nz)

	This modifier stores the information needed by the Odyssey engine for Trimeshes.
	NOTE: Flex == Displacement x 10.  So when exporting out the Flex we divide by 10.

	BioWare noted in their original script that it would be better to create a NULL modifier
	in the SDK and them extend that.  Under GMAX we don't have that option unless you
	own the game developers SDK.  And I DON'T!	 :-)

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
format "load: odyssey_mod_flex.ms\r\n"
kx_progressPlus()


/*
---------------------------------------------------------------------------
Original Flex Modifier.
---------------------------------------------------------------------------
*/
plugin SimpleMod odysseyflex
name:"OdysseyFlex"
classID:#(0x809d7b8a, 0xa426697d)
version:1
(
	local ShapeArray = #()
	parameters paramblock rollout:mdlFlexParams
	(
		/*
			All the attributes that we want to save AND export
			for a trimesh need to be stored in this plug in.
			These attributes are:
			- danglymesh constraints
		*/
		period			type:#float 	ui:spn_period		default:0.4
		tightness		type:#float 	ui:spn_tightness	default:0.6
		displacement	type:#float 	default:0.9		 --ui:spn_displacement
		showdispl		type:#boolean	/*ui:box_showdispl*/	default:true
		displtype		type:#integer	/*ui:radio_displtype*/	default:1
	)

	----------------------------------------------------------------------------
	rollout mdlFlexParams "Odyssey Flex Params" width:162 height:184
	(
		spinner spn_displacement "Flex:" width:92 height:16 range:[0.05,10,0.25] scale:0.05 default:9 -- pos:[57,49]
		spinner spn_tightness "Strength:"width:113 height:16 range:[0.1,100,1] -- pos:[36,28]
		spinner spn_period "Sway:" width:99 height:16 range:[0.1,100,1] -- pos:[50,7]
		button btn_constraints "Edit Constraints" width:150
		--checkbox box_showdispl "Show displacement range" width:146 height:15 -- pos:[13,70]
		--radiobuttons radio_displtype "" width:108 height:32 labels:#("Max displacement", "Scaled by weight") columns:1 -- pos:[27,90]
		--slider sld_vertweight "Weight" range:[0,255,255] type:#float ticks:20
		--spinner spn_weight "Weight:" range:[0,255,255] type:#float
		--label lbl1 "When coloring the verts only the RED colour is used. Blue and Green are ignored." offset:[120,0]width:136 height:48

		--button btn_refresh "Manual Refresh" toolTip:"Force the display boxes to refresh."

		on btn_constraints pressed do ( if kx_constraintedit_flag != true then try (filein (g_kotormaxPath + "kotormax_scripts/kx_constraintedit.ms")) catch() )

		on spn_displacement changed newval do
		(
			displacement = newval/10
			--redrawDangleGizmos(displacement)
		)
		
		on mdlFlexParams open do
		(
			--kx_enableMapChannel selection[1] kx_constraintChannel
			--meshop.setNumMapVerts selection[1] kx_constraintChannel selection[1].verts.count keep:true
		
    		-- just do a general cleanup, just in case for some reason the
    		-- close event did't get called properly
			--for e in ShapeArray do delete e
			--ShapeArray = #()

			--if (showdispl == true) then
			--	redrawDangleGizmos(displacement)

			spn_displacement.value = displacement * 10
		)
		on mdlFlexParams close do
		(
			--for e in ShapeArray do delete e
			--ShapeArray = #()
		)
		
		--- The following are moved to another script
		on box_showdispl changed newval do
		(
			redrawDangleGizmos(displacement)
			radio_displtype.enabled = newval
		)
		on radio_displtype changed newval do
		(
			redrawDangleGizmos(displacement)
		)
		on sld_vertweight changed val do
		(
    		spn_weight.value = val
    		-- update the colour of the selected verts in the current mesh
    		for v in selection[1].selectedverts do
    		    meshop.setvertcolor selection[1] kx_constraintChannel v (color val 0 0)

		    redrawDangleGizmos(displacement)
		)
		on spn_weight changed val do
		(
    		sld_vertweight.value = val
    		-- update the colour of the selected verts in the current mesh
    		for v in selection[1].selectedverts do
    		    meshop.setvertcolor selection[1] kx_constraintChannel v (color val 0 0)

		    redrawDangleGizmos(displacement)
		)
		on btn_refresh pressed do
		(
    		redrawDangleGizmos(displacement)
		)
		--
		-- Creates a set of boxes to show the degree of danglymesh
		-- flex there will be.
		--
		function redrawDangleGizmos radiusSize = (
			clearListener()
			format "------ Danglymesh UI ------\r\n"
			local s
			-- delete the array of existing boxes
			for e in ShapeArray do
				delete e
			ShapeArray = #()
			if (box_showdispl.checked == true) and (selection[1] != undefined) then
			(
				-- rebuild the array of shapes
				local vertcolour
				local scale
				local boxside
				for v in $.selectedVerts do		-- old: $.Verts
				(
					--try
					(
						vertcolour = ((meshop.getMapVert $ kx_constraintChannel v.index) * 255.0) as color --getvertcolor $ v.index
						if (radio_displtype.state == 1) then
							scale = 1
						else
						(
							scale = 1 - vertcolour.red/255
							-- if the vert colour is 0 then scale should be 0
							if vertcolour.red == 0 then scale = 0
						)
						local boxcolor = color 0 0 0
						local ratio = (vertcolour.red - (vertcolour.red as integer / 100) * 100) / 100.0
						if vertcolour.r < 100 then
						(
							boxcolor.r = (0.0 + 0.5 * ratio) * 255.0
							boxcolor.g = (0.0 + 0.5 * ratio) * 255.0
							boxcolor.b = (1.0 - 0.5 * ratio) * 255.0
						)
						else if vertcolour.r < 200 then
						(
							boxcolor.r = (0.5 + 0.5 * ratio) * 255.0
							boxcolor.g = (0.5 + 0.5 * ratio) * 255.0
							boxcolor.b = (0.5 - 0.5 * ratio) * 255.0
						)
						else if vertcolour.r < 300 then
						(
							boxcolor.r = (1.0) * 255.0
							boxcolor.g = (1.0 - 1.0 * ratio) * 255.0
							boxcolor.b = (0.0) * 255.0
						)

						boxside = (scale*radiusSize*10) * 2
						--s = dummy position:(v.position) boxsize:[boxside,boxside,boxside]
						s = point position:v.position size:boxside axistripod:false centermarker:true cross:false box:true constantscreensize:false drawontop:false wirecolor:boxcolor
                        format "vertex#:% scale=% vertcolour=% radius=% boxside=%\r\n" (v.index) scale vertcolour radiusSize boxside
						if s == undefined then format "dummy create failed\r\n"

						append ShapeArray s
					) --catch
					(
						--format "Danglymesh warning - no colour assigned to any vertex.\r\n"
					)
				)
			)
		)
	)

	on map i p do
	(
		-- We do nothing here
		-- Per BioWare:
		-- This is a simplemod so it expects us to be adjusting
		-- the point positions, but we dont want to do that.
	)
)
