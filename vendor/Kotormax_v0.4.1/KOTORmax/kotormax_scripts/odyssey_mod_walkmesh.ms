/*-----------------------------------------------------------------------------\

    Odyssey Walkmesh Modifer

	KOTORmax
	by bead-v
	
	Derived from:
    NWmax
    by Joco (jameswalker@clear.net.nz)
	
    This modifier stores the information needed by the Odyssey engine for
    Walkmeshes. The actual application of the materials is done via the material
    navigator. This seems to be a very natural way to achieve this.

    BioWare noted in their original script that it would be better to create a
    NULL modifier in the SDK and them extend that.  Under GMAX we don't have
    that option unless you own the game developers SDK.  And I DON'T!   :-)

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

\-------------------------------------------------------------------------------------------------*/
format "load: odyssey_mod_walkmesh.ms\r\n"
kx_progressPlus()

plugin simpleMod odysseywalkmesh
name:"OdysseyWalkmesh"
classID:#(0x882d65e4, 0xcd666735)
(
	
	parameters paramblock rollout:mdlWalkmeshParams
	(
    	meshtype        type:#integer default:0
	)

	rollout mdlWalkmeshParams "Odyssey Walkmesh Params" width:162 height:335
    (
        group "Walkmesh Type"
        (
            radiobuttons radio_meshtype "" default:0 labels:#("Area (aabb)", "Placeable (pwk)", "Door (dwk, closed)", "Door (dwk, open1)", "Door (dwk, open2)") align:#left
            --button btn_meshtypeconvert "Set Up" toolTip:"Creates necessary dummies, changes name, moves under correct node."
        )
		button btn_roomlinker "Edit Room Links" width:155 enabled:false
		
		on btn_roomlinker pressed do ( try (filein (g_kotormaxPath + "kotormax_scripts/kx_roomlinker.ms")) catch() )

        on mdlWalkmeshParams open do
        (	
			-- get selected node, and make sure it's a mesh, otherwise return
            local node = selection[1]
			local mesh = undefined
			if iskindof node.baseobject editable_mesh or iskindof node.baseobject trimesh then mesh = node
			else return false

            -- make sure there is a walk material for this mesh
            local matName = "OdysseyWalkMaterial"
            if node.material == undefined or node.material.name != matName then
                node.material = kx_createWalkMaterial()

            if(meshtype > 0) then radio_meshtype.state = meshtype
			if meshtype == 1 then btn_roomlinker.enabled = true
        )

        on mdlWalkmeshParams close do
        (
        )

        on radio_meshtype changed nmeshtype do
        (
			-- get selected node, and make sure it's a mesh, otherwise return
            local node = selection[1]
			local mesh = undefined
			if iskindof node.baseobject editable_mesh or iskindof node.baseobject trimesh then mesh = node
			else return false
			
            if(nmeshtype > 0) then
            (
                local oOdysseyBase = undefined
                oParentBitchyOne = undefined
                if node != undefined then
                (
                    oParentBitchyOne = node.parent
                    while(oOdysseyBase == undefined and oParentBitchyOne != undefined) do
                    (
                        --format "Currently checking: % \n(class: %)\n" oParentBitchyOne (classof oParentBitchyOne)
                        if(iskindof oParentBitchyOne odysseybase) then
                        (
                            --format "Found odysseybase!\n"
                            oOdysseyBase = oParentBitchyOne
                        )
                        else
                         oParentBitchyOne = oParentBitchyOne.parent
                    )
                )
                if (oOdysseyBase == undefined) then messageBox "The mesh does not seem to be descended from an OdysseyBase. Make sure that the mesh or one of its parents/ancestors is linked to an OdysseyBase and try again." title:"Message" beep:false
                else
                (
                    meshtype = nmeshtype
                    local oWalkRoot = undefined
                    for oChild in oOdysseyBase.children do
                    (
                        if(matchPattern oChild.name pattern:"*_pwk" or matchPattern oChild.name pattern:"*_dwk") then
                        (
                            oWalkRoot = oChild
                            exit
                        )
                    )

                    if nmeshtype == 1 then
                    (
                        for oChild in oOdysseyBase.children do
                        (
                            if(oChild.modifiers["OdysseyWalkmesh"] != undefined and oChild.modifiers["OdysseyWalkmesh"].meshtype == 1 and oChild != node) then
                            (
                                messageBox "This model already has an area walkmesh. It will be unlinked from the model." title:"Warning"
                                oChild.parent = undefined
                                oChild.modifiers["OdysseyWalkmesh"].meshtype = 0
                                --exit
                            )
                        )

                        if node.parent != oOdysseyBase then
                        (
                            node.parent = oOdysseyBase
                        )

                        if oWalkRoot != undefined then
                        (
                            delete oWalkRoot
                        )

                        local oUse1 = undefined
                        local oUse2 = undefined
                        for oChild in node.children do
                        (
                            if(matchPattern oChild.name pattern:"*pwk_*01" or matchPattern oChild.name pattern:"*dwk_*01") then
                            (
                                oUse1 = oChild
                            )
                            if(matchPattern oChild.name pattern:"*pwk_*02" or matchPattern oChild.name pattern:"*dwk_*02") then
                            (
                                oUse2 = oChild
                            )
                            if(oUse1 != undefined and oUse2 != undefined) then exit
                        )
                        if(oUse1 != undefined) then
                        (
                            delete oUse1
                        )
                        if(oUse2 != undefined) then
                        (
                            delete oUse2
                        )

                        if(matchPattern node.name pattern:"*_wg*") then
                        (
                            node.name = "WALKMESH"
                        )

                    )
                    else if nmeshtype == 2 then
                    (
                        for oChild in oOdysseyBase.children do
                        (
                            if(oChild.modifiers["OdysseyWalkmesh"] != undefined and oChild.modifiers["OdysseyWalkmesh"].meshtype == 1 and oChild != node) then
                            (
                                messageBox "This model already has an area walkmesh. It will be unlinked from the model." title:"Warning"
                                oChild.parent = undefined
                                oChild.modifiers["OdysseyWalkmesh"].meshtype = 0
                                --exit
                            )
                        )

                        if(oWalkRoot == undefined) then
                        (
                            oWalkRoot = dummy pos:oOdysseyBase.pos
                            oWalkRoot.boxsize = [10,10,10]
                            oWalkRoot.parent = oOdysseyBase
                        )

                        if node.parent != oWalkRoot then
                        (
                            node.parent = oWalkRoot
                        )

                        for oChild in oWalkRoot.children do
                        (
                            if(oChild.modifiers["OdysseyWalkmesh"] != undefined and oChild.modifiers["OdysseyWalkmesh"].meshtype > 1 and oChild != node) then
                            (
                                messageBox "This model already has a pwk/dwk walkmesh. It will be unlinked from the model." title:"Warning"
                                oChild.parent = undefined
                                oChild.modifiers["OdysseyWalkmesh"].meshtype = 0
                                --exit
                            )
                        )

                        local oUse1 = undefined
                        local oUse2 = undefined
                        for oChild in node.children do
                        (
                            if(matchPattern oChild.name pattern:"*pwk_*01" or matchPattern oChild.name pattern:"*dwk_*01") then
                            (
                                oUse1 = oChild
                            )
                            if(matchPattern oChild.name pattern:"*pwk_*02" or matchPattern oChild.name pattern:"*dwk_*02") then
                            (
                                oUse2 = oChild
                            )
                            if(oUse1 != undefined and oUse2 != undefined) then exit
                        )
                        if(oUse1 == undefined) then
                        (
                            oUse1 = dummy name:"pwk_use01" pos:[0,85,0]
                            oUse1.boxsize = [10,10,10]
                            oUse1.parent = node
                        )
                        if(oUse2 == undefined) then
                        (
                            oUse2 = dummy name:"pwk_use02" pos:[0,-85,0]
                            oUse2.boxsize = [10,10,10]
                            oUse2.parent = node
                        )

                        oUse1.name = "pwk_use01"
                        oUse2.name = "pwk_use02"

                        oWalkRoot.name = (oOdysseyBase.name + "_pwk")

                        node.name = (oOdysseyBase.name + "_wg")
                    )
                    else if(nmeshtype == 3 or nmeshtype == 4 or nmeshtype == 5) then
                    (
                        for oChild in oOdysseyBase.children do
                        (
                            if(oChild.modifiers["OdysseyWalkmesh"] != undefined and oChild.modifiers["OdysseyWalkmesh"].meshtype ==1 and oChild != node) then
                            (
                                messageBox "This model already has an area walkmesh. It will be unlinked from the model." title:"Warning"
                                oChild.parent = undefined
                                oChild.modifiers["OdysseyWalkmesh"].meshtype = 0
                                --exit
                            )
                        )

                        if(oWalkRoot == undefined) then
                        (
                            oWalkRoot = dummy pos:oOdysseyBase.pos
                            oWalkRoot.boxsize = [10,10,10]
                            oWalkRoot.parent = oOdysseyBase
                        )

                        if node.parent != oWalkRoot then
                        (
                            node.parent = oWalkRoot
                        )

                        for oChild in oWalkRoot.children do
                        (
                            if(oChild.modifiers["OdysseyWalkmesh"] != undefined and oChild.modifiers["OdysseyWalkmesh"].meshtype > 1 and oChild != node) then
                            (
                                if(oChild.modifiers["OdysseyWalkmesh"].meshtype < 3 or oChild.modifiers["OdysseyWalkmesh"].meshtype == nmeshtype) then
                                (
                                    messageBox "This model already has a conflicting walkmesh. It will be unlinked from the model." title:"Warning"
                                    oChild.parent = undefined
                                    oChild.modifiers["OdysseyWalkmesh"].meshtype = 0
                                    --exit
                                )
                            )
                        )

                        local oUse1 = undefined
                        local oUse2 = undefined
                        for oChild in node.children do
                        (
                            if(matchPattern oChild.name pattern:"*pwk_*01" or matchPattern oChild.name pattern:"*dwk_*01") then
                            (
                                oUse1 = oChild
                            )
                            if(matchPattern oChild.name pattern:"*pwk_*02" or matchPattern oChild.name pattern:"*dwk_*02") then
                            (
                                oUse2 = oChild
                            )
                            if(oUse1 != undefined and oUse2 != undefined) then exit
                        )
                        if(oUse1 == undefined) then
                        (
                            oUse1 = dummy pos:[0,85,0]
                            oUse1.boxsize = [10,10,10]
                            oUse1.parent = node
                        )
                        if(oUse2 == undefined) then
                        (
                            oUse2 = dummy pos:[0,-85,0]
                            oUse2.boxsize = [10,10,10]
                            oUse2.parent = node
                        )

                        local sName
                        local sName1
                        local sName2
                        case nmeshtype of(
                            3:
                            (
                                sName1 = "DWK_dp_closed_01"
                                sName2 = "DWK_dp_closed_02"
                                sName = "DWK_wg_closed"
                            )
                            4:
                            (
                                sName1 = "DWK_dp_open1_01"
                                sName2 = "DWK_dp_open1_02"
                                sName = "DWK_wg_open1"
                            )
                            5:
                            (
                                sName1 = "DWK_dp_open2_01"
                                sName2 = "DWK_dp_open2_02"
                                sName = "DWK_wg_open2"
                            )
                        )

                        oUse1.name = sName1
                        oUse2.name = sName2

                        oWalkRoot.name = (oOdysseyBase.name + "_DWK")

                        node.name = sName
                    )
					if nmeshtype == 1 then btn_roomlinker.enabled = true
					else btn_roomlinker.enabled = false
                    --radio_meshtype.state = nmeshtype
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
