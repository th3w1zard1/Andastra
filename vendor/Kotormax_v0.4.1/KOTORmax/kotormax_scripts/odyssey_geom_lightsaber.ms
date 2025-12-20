/*-----------------------------------------------------------------------------\

    MDL Lightsaber Mesh Object

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
format "load: odyssey_geom_lightsaber.ms\r\n"
kx_progressPlus()

plugin simpleobject odysseylightsaber
name:"OdysseyLightsaber"
classID:#(0x4951b4fa, 0xb33e8e47)
invisible:true
(
	parameters Points rollout:PointsRollout
	(
        saberscale type:#float animatable:true default:1.0 ui:spn_saberscale
        ctrlptx type:#float animatable:false default:10.0505 ui:spn_ctrlptx
        ctrlpty type:#float animatable:false default:0.0 ui:spn_ctrlpty
        ctrlptz type:#float animatable:false default:0.0 ui:spn_ctrlptz
        blade1p1x type:#float animatable:false default:0.0 ui:spn_blade1p1x
        blade1p1y type:#float animatable:false default:0.0 ui:spn_blade1p1y
        blade1p1z type:#float animatable:false default:-4.32471 ui:spn_blade1p1z
        blade1p2x type:#float animatable:false default:0.0 ui:spn_blade1p2x
        blade1p2y type:#float animatable:false default:0.0 ui:spn_blade1p2y
        blade1p2z type:#float animatable:false default:0.536507 ui:spn_blade1p2z
        blade1p3x type:#float animatable:false default:0.0 ui:spn_blade1p3x
        blade1p3y type:#float animatable:false default:0.0 ui:spn_blade1p3y
        blade1p3z type:#float animatable:false default:93.2362 ui:spn_blade1p3z
        blade1p4x type:#float animatable:false default:0.0 ui:spn_blade1p4x
        blade1p4y type:#float animatable:false default:0.0 ui:spn_blade1p4y
        blade1p4z type:#float animatable:false default:98.0982 ui:spn_blade1p4z
        blade2p1x type:#float animatable:false default:0.0 ui:spn_blade2p1x
        blade2p1y type:#float animatable:false default:0.0 ui:spn_blade2p1y
        blade2p1z type:#float animatable:false default:-4.32471 ui:spn_blade2p1z
        blade2p2x type:#float animatable:false default:0.0 ui:spn_blade2p2x
        blade2p2y type:#float animatable:false default:0.0 ui:spn_blade2p2y
        blade2p2z type:#float animatable:false default:0.536507 ui:spn_blade2p2z
        blade2p3x type:#float animatable:false default:0.0 ui:spn_blade2p3x
        blade2p3y type:#float animatable:false default:0.0 ui:spn_blade2p3y
        blade2p3z type:#float animatable:false default:93.2362 ui:spn_blade2p3z
        blade2p4x type:#float animatable:false default:0.0 ui:spn_blade2p4x
        blade2p4y type:#float animatable:false default:0.0 ui:spn_blade2p4y
        blade2p4z type:#float animatable:false default:98.0982 ui:spn_blade2p4z
	)

    fn UpdateMesh =
    (
        local cpx = ctrlptx
        local cpy = ctrlpty
        local cpz = ctrlptz

        --blade1
        if mesh.verts[1].pos.x != blade1p1x then mesh.verts[1].pos.x = blade1p1x
        if mesh.verts[1].pos.y != blade1p1y then mesh.verts[1].pos.y = blade1p1y
        if mesh.verts[1].pos.z != blade1p1z then mesh.verts[1].pos.z = blade1p1z
        if mesh.verts[2].pos.x != blade1p2x then mesh.verts[2].pos.x = blade1p2x
        if mesh.verts[2].pos.y != blade1p2y then mesh.verts[2].pos.y = blade1p2y
        if mesh.verts[2].pos.z != blade1p2z then mesh.verts[2].pos.z = blade1p2z
        if mesh.verts[3].pos.x != blade1p3x then mesh.verts[3].pos.x = blade1p3x
        if mesh.verts[3].pos.y != blade1p3y then mesh.verts[3].pos.y = blade1p3y
        if mesh.verts[3].pos.z != blade1p3z then mesh.verts[3].pos.z = blade1p3z
        if mesh.verts[4].pos.x != blade1p4x then mesh.verts[4].pos.x = blade1p4x
        if mesh.verts[4].pos.y != blade1p4y then mesh.verts[4].pos.y = blade1p4y
        if mesh.verts[4].pos.z != blade1p4z then mesh.verts[4].pos.z = blade1p4z

        --blade1 + control
        if mesh.verts[5].pos.x != blade1p1x+cpx then mesh.verts[5].pos.x = blade1p1x+cpx
        if mesh.verts[5].pos.y != blade1p1y+cpy then mesh.verts[5].pos.y = blade1p1y+cpy
        if mesh.verts[5].pos.z != blade1p1z+cpz then mesh.verts[5].pos.z = blade1p1z+cpz
        if mesh.verts[6].pos.x != blade1p2x+cpx then mesh.verts[6].pos.x = blade1p2x+cpx
        if mesh.verts[6].pos.y != blade1p2y+cpy then mesh.verts[6].pos.y = blade1p2y+cpy
        if mesh.verts[6].pos.z != blade1p2z+cpz then mesh.verts[6].pos.z = blade1p2z+cpz
        if mesh.verts[7].pos.x != blade1p3x+cpx then mesh.verts[7].pos.x = blade1p3x+cpx
        if mesh.verts[7].pos.y != blade1p3y+cpy then mesh.verts[7].pos.y = blade1p3y+cpy
        if mesh.verts[7].pos.z != blade1p3z+cpz then mesh.verts[7].pos.z = blade1p3z+cpz
        if mesh.verts[8].pos.x != blade1p4x+cpx then mesh.verts[8].pos.x = blade1p4x+cpx
        if mesh.verts[8].pos.y != blade1p4y+cpy then mesh.verts[8].pos.y = blade1p4y+cpy
        if mesh.verts[8].pos.z != blade1p4z+cpz then mesh.verts[8].pos.z = blade1p4z+cpz

        --blade2
        if mesh.verts[9].pos.x != blade2p1x then mesh.verts[9].pos.x = blade2p1x
        if mesh.verts[9].pos.y != blade2p1y then mesh.verts[9].pos.y = blade2p1y
        if mesh.verts[9].pos.z != blade2p1z then mesh.verts[9].pos.z = blade2p1z
        if mesh.verts[10].pos.x != blade2p2x then mesh.verts[10].pos.x = blade2p2x
        if mesh.verts[10].pos.y != blade2p2y then mesh.verts[10].pos.y = blade2p2y
        if mesh.verts[10].pos.z != blade2p2z then mesh.verts[10].pos.z = blade2p2z
        if mesh.verts[11].pos.x != blade2p3x then mesh.verts[11].pos.x = blade2p3x
        if mesh.verts[11].pos.y != blade2p3y then mesh.verts[11].pos.y = blade2p3y
        if mesh.verts[11].pos.z != blade2p3z then mesh.verts[11].pos.z = blade2p3z
        if mesh.verts[12].pos.x != blade2p4x then mesh.verts[12].pos.x = blade2p4x
        if mesh.verts[12].pos.y != blade2p4y then mesh.verts[12].pos.y = blade2p4y
        if mesh.verts[12].pos.z != blade2p4z then mesh.verts[12].pos.z = blade2p4z

        --blade2 + control
        if mesh.verts[13].pos.x != blade2p1x-cpx then mesh.verts[13].pos.x = blade2p1x-cpx
        if mesh.verts[13].pos.y != blade2p1y-cpy then mesh.verts[13].pos.y = blade2p1y-cpy
        if mesh.verts[13].pos.z != blade2p1z-cpz then mesh.verts[13].pos.z = blade2p1z-cpz
        if mesh.verts[14].pos.x != blade2p2x-cpx then mesh.verts[14].pos.x = blade2p2x-cpx
        if mesh.verts[14].pos.y != blade2p2y-cpy then mesh.verts[14].pos.y = blade2p2y-cpy
        if mesh.verts[14].pos.z != blade2p2z-cpz then mesh.verts[14].pos.z = blade2p2z-cpz
        if mesh.verts[15].pos.x != blade2p3x-cpx then mesh.verts[15].pos.x = blade2p3x-cpx
        if mesh.verts[15].pos.y != blade2p3y-cpy then mesh.verts[15].pos.y = blade2p3y-cpy
        if mesh.verts[15].pos.z != blade2p3z-cpz then mesh.verts[15].pos.z = blade2p3z-cpz
        if mesh.verts[16].pos.x != blade2p4x-cpx then mesh.verts[16].pos.x = blade2p4x-cpx
        if mesh.verts[16].pos.y != blade2p4y-cpy then mesh.verts[16].pos.y = blade2p4y-cpy
        if mesh.verts[16].pos.z != blade2p4z-cpz then mesh.verts[16].pos.z = blade2p4z-cpz

        update mesh
    )

    fn UpdateParams =
    (
        --blade1
        if blade1p1x != mesh.verts[1].pos.x then blade1p1x = mesh.verts[1].pos.x
        if blade1p1y != mesh.verts[1].pos.y then blade1p1y = mesh.verts[1].pos.y
        if blade1p1z != mesh.verts[1].pos.z then blade1p1z = mesh.verts[1].pos.z
        if blade1p2x != mesh.verts[2].pos.x then blade1p2x = mesh.verts[2].pos.x
        if blade1p2y != mesh.verts[2].pos.y then blade1p2y = mesh.verts[2].pos.y
        if blade1p2z != mesh.verts[2].pos.z then blade1p2z = mesh.verts[2].pos.z
        if blade1p3x != mesh.verts[3].pos.x then blade1p3x = mesh.verts[3].pos.x
        if blade1p3y != mesh.verts[3].pos.y then blade1p3y = mesh.verts[3].pos.y
        if blade1p3z != mesh.verts[3].pos.z then blade1p3z = mesh.verts[3].pos.z
        if blade1p4x != mesh.verts[4].pos.x then blade1p4x = mesh.verts[4].pos.x
        if blade1p4y != mesh.verts[4].pos.y then blade1p4y = mesh.verts[4].pos.y
        if blade1p4z != mesh.verts[4].pos.z then blade1p4z = mesh.verts[4].pos.z

        --blade2
        if blade2p1x != mesh.verts[9].pos.x then blade2p1x = mesh.verts[9].pos.x
        if blade2p1y != mesh.verts[9].pos.y then blade2p1y = mesh.verts[9].pos.y
        if blade2p1z != mesh.verts[9].pos.z then blade2p1z = mesh.verts[9].pos.z
        if blade2p2x != mesh.verts[10].pos.x then blade2p2x = mesh.verts[10].pos.x
        if blade2p2y != mesh.verts[10].pos.y then blade2p2y = mesh.verts[10].pos.y
        if blade2p2z != mesh.verts[10].pos.z then blade2p2z = mesh.verts[10].pos.z
        if blade2p3x != mesh.verts[11].pos.x then blade2p3x = mesh.verts[11].pos.x
        if blade2p3y != mesh.verts[11].pos.y then blade2p3y = mesh.verts[11].pos.y
        if blade2p3z != mesh.verts[11].pos.z then blade2p3z = mesh.verts[11].pos.z
        if blade2p4x != mesh.verts[12].pos.x then blade2p4x = mesh.verts[12].pos.x
        if blade2p4y != mesh.verts[12].pos.y then blade2p4y = mesh.verts[12].pos.y
        if blade2p4z != mesh.verts[12].pos.z then blade2p4z = mesh.verts[12].pos.z

        --control point
        if ctrlptx != mesh.verts[5].pos.x - mesh.verts[1].pos.x then ctrlptx = mesh.verts[5].pos.x - mesh.verts[1].pos.x
        if ctrlpty != mesh.verts[5].pos.y - mesh.verts[1].pos.y then ctrlpty = mesh.verts[5].pos.y - mesh.verts[1].pos.y
        if ctrlptz != mesh.verts[5].pos.z - mesh.verts[1].pos.z then ctrlptz = mesh.verts[5].pos.z - mesh.verts[1].pos.z
    )

	rollout PointsRollout "Odyssey Lightsaber Params"
   	(
        spinner spn_saberscale "Scale:" type:#float range:[0, 1000, 1]
        button btn_collapse_unwrap "Collapse Modifier Stack" tooltip:"Collapses the modifier stack without converting the OdysseyLightsaber to an Editable Mesh."
        group "Blade 1"
        (
            label lbl_1_x "X" align:#center across:3
            label lbl_1_y "Y" align:#center
            label lbl_1_z "Z" align:#center
            spinner spn_blade1p1x align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0] across:3
            spinner spn_blade1p1y align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
            spinner spn_blade1p1z align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
            spinner spn_blade1p2x align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0] across:3
            spinner spn_blade1p2y align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
            spinner spn_blade1p2z align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
            spinner spn_blade1p3x align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0] across:3
            spinner spn_blade1p3y align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
            spinner spn_blade1p3z align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
            spinner spn_blade1p4x align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0] across:3
            spinner spn_blade1p4y align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
            spinner spn_blade1p4z align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
        )
        group "Blade 2"
        (
            label lbl_2_x "X" align:#center across:3
            label lbl_2_y "Y" align:#center
            label lbl_2_z "Z" align:#center
            spinner spn_blade2p1x align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0] across:3
            spinner spn_blade2p1y align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
            spinner spn_blade2p1z align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
            spinner spn_blade2p2x align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0] across:3
            spinner spn_blade2p2y align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
            spinner spn_blade2p2z align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
            spinner spn_blade2p3x align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0] across:3
            spinner spn_blade2p3y align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
            spinner spn_blade2p3z align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
            spinner spn_blade2p4x align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0] across:3
            spinner spn_blade2p4y align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
            spinner spn_blade2p4z align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]

        )
        group "Control Point"
        (
            label lbl_3_x "X" align:#center across:3
            label lbl_3_y "Y" align:#center
            label lbl_3_z "Z" align:#center
            spinner spn_ctrlptx align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0] across:3
            spinner spn_ctrlpty align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
            spinner spn_ctrlptz align:#center fieldwidth:38 type:#float range:[-100000, 100000, 0]
        )

        on spn_blade1p1x changed val do UpdateMesh()
        on spn_blade1p1y changed val do UpdateMesh()
        on spn_blade1p1z changed val do UpdateMesh()
        on spn_blade1p2x changed val do UpdateMesh()
        on spn_blade1p2y changed val do UpdateMesh()
        on spn_blade1p2z changed val do UpdateMesh()
        on spn_blade1p3x changed val do UpdateMesh()
        on spn_blade1p3y changed val do UpdateMesh()
        on spn_blade1p3z changed val do UpdateMesh()
        on spn_blade1p4x changed val do UpdateMesh()
        on spn_blade1p4y changed val do UpdateMesh()
        on spn_blade1p4z changed val do UpdateMesh()

        on spn_blade2p1x changed val do UpdateMesh()
        on spn_blade2p1y changed val do UpdateMesh()
        on spn_blade2p1z changed val do UpdateMesh()
        on spn_blade2p2x changed val do UpdateMesh()
        on spn_blade2p2y changed val do UpdateMesh()
        on spn_blade2p2z changed val do UpdateMesh()
        on spn_blade2p3x changed val do UpdateMesh()
        on spn_blade2p3y changed val do UpdateMesh()
        on spn_blade2p3z changed val do UpdateMesh()
        on spn_blade2p4x changed val do UpdateMesh()
        on spn_blade2p4y changed val do UpdateMesh()
        on spn_blade2p4z changed val do UpdateMesh()

        on spn_ctrlptx changed val do UpdateMesh()
        on spn_ctrlpty changed val do UpdateMesh()
        on spn_ctrlptz changed val do UpdateMesh()

        on btn_collapse_unwrap pressed do
        (
            local node = selection[1]
            -- very interestingly, according to maxscript docs, if I get the mesh of a node, it's gonna give me the mesh
            -- with all the modifiers applied, but without replacing the source mesh. Sounds ideal!
            local tempmesh = node.mesh
            if meshop.getNumVerts tempmesh < 16 or meshop.getNumVerts tempmesh > 16 then
            (
                messagebox "Unable to collapse: some vertices have been added or deleted, which may break this object! Try to get rid of the offending modifiers."
                return false
            )
            if meshop.getNumTVerts tempmesh < 16 or meshop.getNumTVerts tempmesh > 16 then
            (
                messagebox "Unable to collapse: some texture vertices have been created or deleted, which may cause problems on export! Try to get rid of the offending modifiers."
                return false
            )
            if node.modifiers.count == 0 then return false

            for i = 1 to 16 do
            (
                mesh.verts[i].pos = (meshop.getVert tempmesh i)
                setTVert mesh i (getTVert tempmesh i)
            )

            UpdateParams()
            UpdateMesh()

            for mod in node.modifiers do
            (
                deleteModifier node mod
            )

            update mesh
        )

        on PointsRollout load do
        (
            UpdateMesh()
        )
	)

    on buildMesh do
    (
        local cpx = ctrlptx
        local cpy = ctrlpty
        local cpz = ctrlptz
        local verts = #()
        local tverts = #()
        local faces = #()

        join verts #([blade1p1x,blade1p1y,blade1p1z], [blade1p2x,blade1p2y,blade1p2z], [blade1p3x,blade1p3y,blade1p3z], [blade1p4x,blade1p4y,blade1p4z])
        join verts #([blade1p1x+cpx,blade1p1y+cpy,blade1p1z+cpz], [blade1p2x+cpx,blade1p2y+cpy,blade1p2z+cpz], [blade1p3x+cpx,blade1p3y+cpy,blade1p3z+cpz], [blade1p4x+cpx,blade1p4y+cpy,blade1p4z+cpz])
        join verts #([blade2p1x,blade2p1y,blade2p1z], [blade2p2x,blade2p2y,blade2p2z], [blade2p3x,blade2p3y,blade2p3z], [blade2p4x,blade2p4y,blade2p4z])
        join verts #([blade2p1x-cpx,blade2p1y-cpy,blade2p1z-cpz], [blade2p2x-cpx,blade2p2y-cpy,blade2p2z-cpz], [blade2p3x-cpx,blade2p3y-cpy,blade2p3z-cpz], [blade2p4x-cpx,blade2p4y-cpy,blade2p4z-cpz])

        join tverts #([0.5, 0.000119802, 0.0], [0.5, 0.518183, 0.0], [0.5, 0.517597, 0.0], [0.5, 0.999855, 0.0])
        join tverts #([0.998751, 0.000119802, 0.0], [0.998751, 0.213028, 0.0], [0.998751, 0.895558, 0.0], [0.998751, 0.999855, 0.0])
        join tverts #([0.5, 0.000119802, 0.0], [0.5, 0.518183, 0.0], [0.5, 0.517597, 0.0], [0.5, 0.999855, 0.0])
        join tverts #([1.76689e-005, 0.000119802, 0.0], [1.76689e-005, 0.213287, 0.0], [1.76689e-005, 0.896492, 0.0], [1.76689e-005, 0.999855, 0.0])

        join faces #([6, 5, 1], [1, 2, 6], [14, 9, 13], [9, 14, 10], [7, 6, 2], [2, 3, 7], [11, 10, 14], [14, 15, 11], [4, 7, 3], [7, 4, 8], [16, 12, 15], [11, 15, 12])
        join faces #([5, 6, 1], [2, 1, 6], [9, 14, 13], [14, 9, 10], [6, 7, 2], [3, 2, 7], [10, 11, 14], [15, 14, 11], [7, 4, 3], [4, 7, 8], [12, 16, 15], [15, 11, 12])

        setMesh mesh verts:verts faces:faces

        for i = 1 to 12 do setFaceSmoothGroup mesh i 1
        for i = 13 to 24 do setFaceSmoothGroup mesh i 2

		kx_enableMapChannel mesh 1
        setNumTVerts mesh tverts.count
        for i = 1 to tverts.count do setTVert mesh i tverts[i]
        buildTVFaces mesh
        for i = 1 to faces.count do setTVFace mesh i faces[i]
    )
)

fn createOdysseyLightsaber position =
(
    local oObject = odysseylightsaber pos:position
    return oObject
)
/*
plugin helper aurorasaberbox
name:"AuroraSaber"
(
    tool create
    (
        on mousePoint clickNo do
        (
            if modPanel.getCurrentObject() == undefined and clickNo > 1 then
            (
                local oObject = createOdysseyLightsaber gridPoint
                select oObject
                if not ctrlKey then
                (
                    max modify mode
                    #stop
                )
            )
        )
    )
)
*/