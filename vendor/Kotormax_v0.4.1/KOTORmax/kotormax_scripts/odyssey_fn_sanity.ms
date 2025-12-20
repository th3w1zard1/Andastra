/*-----------------------------------------------------------------------------\

    Odyssey Sanity Check Functions
	
	KOTORmax
	by bead-v
	
	Derived from:
    NWmax
    by Joco (jameswalker@clear.net.nz)

    Credits:
    Joco :-
        Extends Joco's Import/Export script with rewrites as needed
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

global RootNodeName;
global FoundCollisionMesh;

-- CloseTo will take two vectors and a tolerance value and
-- verify that the first vector is within the tolerance
-- of the second.
function CloseTo a b e =
(
    if ((a.x < (b.x + e)) and (a.x > (b.x - e)) and
        (a.y < (b.y + e)) and (a.y > (b.y - e)) and
        (a.z < (b.z + e)) and (a.z > (b.z - e))) then
        return 1
    else return 0
)

-- returns true if checkName is the name of a descendant of node
function FindNodeName node checkName = (
    if node.name == checkName then return true
    for child in node.children do
        if FindNodeName child checkName then return true
    return false
)

-- This check determines if the animroot node for each animation actually
-- exists within the model
function AnimrootCheck inputNode = 
(
    if DEBUG then format "AnimrootCheck%" g_delim to:g_strBuffer
    local acceptable = 1

    if (kx_strclassof(inputNode) == "odysseybase") then
    (
        if (inputNode.animations != #undefined) then (
            local animStringArray
            local curAnimCount = 0
            progressStart "Animroot Check"
            for curAnim in inputNode.animations do
            (
                curAnimCount += 1

                -- update progress bar
                if not (progressUpdate ((curAnimCount*100)/(inputNode.animations.count))) then
                (
                    -- cancel out of anymore work
                    return acceptable
                )

                animStringArray = filterstring curAnim " "
                if (animStringArray.count > 5) then ( -- If animroot has been set for this animation
                    -- Now test if this node exists
                    local animroot = animStringArray[6]
                    local foundName = false

                    foundName = FindNodeName inputNode animroot

                    if (foundName == false) then ( -- If the node doesn't exist, kick & yell & scream until it's fixed
                        local result = yesNoCancelBox ("The animroot \"" + animroot + "\" for the \"" + animStringArray[1] + "\" animation doesn't exist.  Press 'Yes' to set the animroot as the OdysseyBase of the model; press 'No' to simply print a warning to the Listener Window and continue; press 'Cancel' to abort Sanity Check. If you press 'Yes', you must save the scene before exiting.") title:"Sanity Check Error"

                        if result == #yes then
						(
                            -- Set the animroot to be the OdysseyBase name
                            local animRootIndex = findString curAnim animStringArray[6]
                            inputNode.animations[curAnimCount] = (substring curAnim 1 (animRootIndex-1))+inputNode.name

                        )
						else if result == #cancel then
						(
							progressEnd()
							return 2 -- Signal to Sanity Check Function that we want to abort
						)
						else
						(
                            -- Dump the animation name to the listener window and let someone else deal with this
                            format "### Animroot [%] for % doesn't exist.%" animroot animStringArray[1] g_delim to:g_strBuffer
                            kx_FlushBuffer()
                            acceptable = 0
                        )
                    )
                )
            )----for curAnim in inputNode.animations
            progressEnd()
        ) ----if (inputNode.animations != #undefined)
    )----if (classof(object) == OdysseyBase)
    return acceptable
)

-- This check is for skinned characters - a vertex cannot be affected by more than
-- 4 bones; if it is, the offending bones are printed to the Listener Window
global gbl_sanity_weight_alwaysfix = 0
function BoneCountCheck node = (
    if DEBUG then format "BoneCountCheck%" g_delim to:g_strBuffer

    --local save_selection = selection[1]
    local save_selection = selection
    max modify mode
    select node

    local i,j,n
    local skinMod = node.modifiers["Skin"]
    try (
        n = skinops.getnumbervertices skinMod
    ) catch
    (
        format "DEBUG: BoneCountCheck::failure caught in 'try' block%" g_delim to:g_strBuffer
        kx_FlushBuffer()
        n = skinops.getnumbervertices skinMod
    )
    local boneNames = #()
    local zeroNames = #()
    local acceptable = true
    for i = 1 to n do
    (
        local w_num = skinops.getvertexweightcount skinMod i
        if (w_num == 0) then
            continue
        boneNames = #()
        zeroNames = #()
		local vertWeights = #()
        for j = 1 to w_num do -- for each of the weights on the current vertex...
        (
            local bone_id   = (skinops.getvertexweightboneid skinMod i j )
            local bone_name = "root"
            local weight_i_j = 1
            if(bone_id > 0) do
                bone_name = (skinops.getbonename skinMod bone_id 0) -- could be 1 or could be 0 UNSURE!!

            weight_i_j = (skinops.getvertexweight skinMod i j )
            if (weight_i_j != 0.0) then (
                append boneNames bone_name
                append boneNames weight_i_j
				append vertWeights #(bone_id, weight_i_j)
            )
            else
                append zeroNames bone_name
        )
        -- This is 8 because we're storing the bone name as well as it's weight
        if (boneNames.count > 8) then (
			if gbl_sanity_weight_alwaysfix == 0 then
			(
				local bReponse = QueryBox ("Your model contains skin vertices that are affected by more than four bones. The MDL format can only handle four bones and will deform the skin in the games if this is not fixed.\n\n" + \
					"Do you want to automatically delete the least significant extra weights for all the problematic skin vertices in this model?") \
					title:"Skin problem" beep:true
				if bReponse then gbl_sanity_weight_alwaysfix = 1
				else gbl_sanity_weight_alwaysfix = 2
			)
			if gbl_sanity_weight_alwaysfix == 1 then
			(
				qsort vertWeights sortBones
				local nTotal = 0
				for m = 1 to 4 where m <= vertWeights.count do nTotal += vertWeights[m][2]
				for m = 1 to 4 where m <= vertWeights.count do vertWeights[m][2] += (1 - nTotal) * (vertWeights[m][2] / nTotal)
				skinOps.ReplaceVertexWeights skinMod i \
					(for m = 1 to 4 where m <= vertWeights.count collect vertWeights[m][1]) \
					(for m = 1 to 4 where m <= vertWeights.count collect vertWeights[m][2]) 
			)
			else
			(
				acceptable = false
				format "More than 4 bones are affecting vertex %. Bones: [" i to:g_strBuffer
				for e in boneNames do (
					format "  %" e to:g_strBuffer
					kx_FlushBuffer()
				)
				format "  ]%" g_delim to:g_strBuffer
				kx_FlushBuffer()
			)
        )
        -- The following check really should be ignored, as the exporter already takes care of it.
        if (zeroNames.count == w_num) then (
            format "WARNING: The following bones [" to:g_strBuffer
            for e in zeroNames do (
                format "  %" e to:g_strBuffer
                kx_FlushBuffer()
            )
            format "  ] have no effect on vertex %%" i g_delim to:g_strBuffer
            kx_FlushBuffer()
        )
    )

    if(save_selection != undefined) do
        select save_selection
    if (acceptable == true) then
        return 1
    else (
        format "'Skinning: 4 Bones per Vert' Check failed.%" g_delim to:g_strBuffer
        kx_FlushBuffer()
        return 0
    )
)

-- BitMapNameCheck will verify that the name of the
-- texture is at most 16 characters long
function BitMapNameCheck object =
(
    if DEBUG then format "BitMapNameCheck%" g_delim to:g_strBuffer

    local mat = object.material
    if mat == undefined then return 1
	
    local bitmapname
	local bmchk_acceptable = true
	if classof mat == Multimaterial then 
	(
		for i = 1 to object.material.numsubs do
		(
			bitmapname = kx_materialdiffusebitmapname object.material[i]
			if bitmapname.count > 16 then
			(
				format "The diffuse bitmap name \"%\" is too long. Please reduce it to 16 or less characters%" bitmapname g_delim to:g_strBuffer
				kx_FlushBuffer()
				bmchk_acceptable = false
			)
			
			bitmapname = kx_materialambientbitmapname object.material[i]
			if bitmapname.count > 16 then
			(
				format "The ambient bitmap name \"%\" is too long. Please reduce it to 16 or less characters%" bitmapname g_delim to:g_strBuffer
				kx_FlushBuffer()
				bmchk_acceptable = false
			)
		)
		
		if not bmchk_acceptable then 
		(
			format "'Bitmap Name Validity' Check failed.%" g_delim to:g_strBuffer
			kx_FlushBuffer()
			return 0
		)
		else return 1
	)
	
	bitmapname = kx_materialdiffusebitmapname mat
	if bitmapname.count > 16 then
	(
		format "The diffuse bitmap name \"%\" is too long. Please reduce it to 16 or less characters%" bitmapname g_delim to:g_strBuffer
		kx_FlushBuffer()
		bmchk_acceptable = false
	)
	
	bitmapname = kx_materialambientbitmapname mat
	if bitmapname.count > 16 then
	(
		format "The ambient bitmap name \"%\" is too long. Please reduce it to 16 or less characters%" bitmapname g_delim to:g_strBuffer
		kx_FlushBuffer()
		bmchk_acceptable = false
	)
	
	if not bmchk_acceptable then
	(
		format "'Bitmap Name Validity' Check failed.%" g_delim to:g_strBuffer
		kx_FlushBuffer()
		return 0
	)
	else return 1
)

--
-- Part Texture Check: verifies that the material bitmap name is the same as the node name
--
function PartTextureCheck object =
(
    if DEBUG then format "PartTextureCheck%" g_delim to:g_strBuffer

    if (RootNodeName != undefined) then
    (
        local mat = object.material
        if (mat != undefined) then
        (
            if (kx_materialbitmapname(mat) != RootNodeName) then
            (
                local mbn = kx_materialbitmapname(mat) as string
                format "Node \"%\" has a texture \"%\" that doesn't have a matching name%" object.name mbn  g_delim to:g_strBuffer
                format "'Material Bitmap Name' Check failed.%" g_delim to:g_strBuffer
                kx_FlushBuffer()
                return 0
            )
        )
    )
)

--
-- Class Check: checks if a node is of type Boolean when it should be an Edit Mesh
--
function ClassCheck object =
(
    if DEBUG then format "ClassCheck%" g_delim to:g_strBuffer

    local classtype = classof(object)
    if (matchPattern (classtype as string) pattern:"Boolean*") then
    (
        format "Node \"%\" is a Boolean.  It needs an Edit Mesh modifier to be exported properly%"  object.name  g_delim to:g_strBuffer
        format "'Node type boolean' Check failed.%" g_delim to:g_strBuffer
        kx_FlushBuffer()
        return 0
    )
)

-- AnimationNameCheck will verify that the animation name
-- passed in is included in the set list of verified
-- animation names.  If an animation has a name that is
-- not included in the list, a warning will pop up and
-- as if they wish to add it to the list or not.  If they
-- choose not to, they may still export that animation.

-- *** This Check Isn't Called By The Sanity Checker But Is Still Called On Export ***--

function AnimationNameCheck root animname =
(
    if DEBUG then format "AnimationNameCheck%" g_delim to:g_strBuffer

    local curpath = getdir #scripts
    local namelist = ReadAnimationNameList (curpath+"\\Aura\\"+root.classification+".nam")
    local i = 1
    while (i <= namelist.count) do
    (
        if (animname == namelist[i]) then
        (
            return 1
        )
        i += 1;
    )

    local str = "The animation \"" + animname + "\" was \n" +
          "not found in the animation name list used.\n" +
          "Do you wish to add \"" + animname + "\" to \n" +
          "the list?"
    local res = yesNoCancelBox str title:"Animation Name Not Found" beep:true
    if (res == #yes) then
    (
        curpath = getdir #scripts
        local fstream = openFile (curpath+"\\Aura\\"+root.classification+".nam") mode:"a"
        if (fstream != undefined) then
        (
            format "%%" animname g_delim to:g_strBuffer
            kx_FlushBuffer()
            --close fstream
        )
        else
        (
            format "The file \"%\" has read only properties or could not be found%" root.classification g_delim to:g_strBuffer
            format "Animation Name Check failed.%" g_delim to:g_strBuffer
            kx_FlushBuffer()
            return 0
        )
    )
    if (res == #cancel) then
    (
        format "Animation Name Check failed.%" g_delim to:g_strBuffer
        kx_FlushBuffer()
        return 0
    )
    return 1
)

-- NameCheck will verify that all nodes to be exported
-- do not have spaces in their names.  If a space is found,
-- a warning will be issued to the user, and the user will
-- have be asked if they wish to replace the spaces with
-- underscores.  Consecutive spaces will be replaced with
-- a single underscore however.
--
-- Name Check: verifies all node names.  Node names cannot have spaces, tabs or be longer than 16
--                     characters.  A node cannot be named 'root'.
--
function NameCheck object =
(
    if DEBUG then format "NameCheck%" g_delim to:g_strBuffer

    if (object.name.count == 0) then
    (
        format "An object in the scene has a name that contains no characters. NameCheck cannot continue%" g_delim to:g_strBuffer
        format "'Node Name Validity' Check failed.%" g_delim to:g_strBuffer
        kx_FlushBuffer()
        return 0
    )

    if (kx_strclassof(object) == "odysseybase") then
    (
        if (object.name.count > 16) then
        (
            format "Model (odysseybase) name \"%\" is too long.  An object name can be up to 16 characters. NameCheck cannot continue%" object.name g_delim to:g_strBuffer
            format "'Node Name Validity' Check failed.%" g_delim to:g_strBuffer
            kx_FlushBuffer()
            return 0
        )
    )

    local test = filterString object.name " "
    if ((test.count > 1) or (matchPattern object.name pattern:"* *")) then
    (
        str = "The object name \"" + object.name + "\" is \n"+
            "invalid because it contains a space.\n\n"+
            "Do you wish to replace the space(s) with \"_\"?\n"+
            "NOTE: Leading spaces are removed.";

        if (queryBox str title:"Sanity Checker:Name Check" beep:true) then
        (
            name = test[1];
            for c = 2 to test.count do
                name += "_" + test[c]

            object.name = name
        )
        else (
            format "'Node Name Validity' Check failed.%" g_delim to:g_strBuffer
            kx_FlushBuffer()
            return 0
        )
    )

    test = filterString object.name "   "
    if ((test.count > 1) or (object.name[1]=="  ") or (matchPattern object.name pattern:"*  ")) then
    (
        str = "The object name \"" + object.name + "\" is \n"+
            "invalid because it contains a tab.\n\n"+
            "Do you wish to replace the tab(s) with \"_\"?\n"+
            "NOTE: Leading tab(s) are removed.";

        if (queryBox str title:"Sanity Checker:Name Check" beep:true) then
        (
            name = test[1];
            for c = 2 to test.count do
                name += "_" + test[c]

            object.name = name
        )
        else (
            format "'Node Name Validity' Check failed.%" g_delim to:g_strBuffer
            kx_FlushBuffer()
            return 0
        )
    )

    if (matchPattern object.name pattern:"root") then
    (
        if (object.name.count == 4) then
        (
            format "A node in this scene is invalid because it has the name \"%\" %" object.name g_delim to:g_strBuffer
            format "'Node Name Validity' Check failed.%" g_delim to:g_strBuffer
            kx_FlushBuffer()
            return 0
        )
    )

    return 1
)


--
-- RotationCheck will verify that the OdysseyBase nodes
-- that will be exported have zero rotation values.
--  This check is run only on OdysseyBases.
--
function RotationCheck baseNode =
(
    if DEBUG then format "RotationCheck%" g_delim to:g_strBuffer

    if (baseNode.rotation != (quat 0 0 0 1)) then
    (
        format "OdysseyBase \"%\" has a non-zero rotation%" baseNode.name g_delim to:g_strBuffer
        format "'Zero Rotation' Check failed.%" g_delim to:g_strBuffer
        kx_FlushBuffer()
        return 0
    )

    return 1
)


--
-- ScaleCheck will verify that all nodes to be exported
-- have world scale values of 1. Scaled values can still be entered
-- into the Aura rollouts
--
function ScaleCheck object =
(
    if DEBUG then format "ScaleCheck%" g_delim to:g_strBuffer

    res = CloseTo object.scale [1,1,1] 0.0005
    if (res == 0) then
    (
        format "Node \"%\" has a scale that is not 1%" (object.name) g_delim to:g_strBuffer
        format "'Zero World Scale' Check failed.%" g_delim to:g_strBuffer
        kx_FlushBuffer()
        return 0
    )

    return 1
)

--
-- ControllerCheck will verify that any node to be exported
-- with position or rotation keys implement only linear
-- controllers (or bezier controllers for position).
--
function ControllerCheck object =
(
    if DEBUG then format "ControllerCheck%" g_delim to:g_strBuffer

    if (object.position.controller.keys.count != 0) then
    (
        if ((classof object.position.controller != linear_position) and (classof object.position.controller != bezier_position)) then
        (
            format "The position controller for \"%\" is neither linear nor Bezier%" object.name g_delim to:g_strBuffer
            format "'Controllers on Keys' Check failed.%" g_delim to:g_strBuffer
            kx_FlushBuffer()
            return 0
        )
    )

    if (object.rotation.controller.keys.count != 0) then
    (
        if (classof object.rotation.controller != linear_rotation) then
        (
            format "The rotation controller for \"%\" is not linear: '%'%" object.name (classof object.rotation.controller) g_delim to:g_strBuffer
            format "'Controllers on Keys' Check failed.%" g_delim to:g_strBuffer
            kx_FlushBuffer()
            return 0
        )
    )
    return 1
)

--
-- AnimCheck will verify that for all animations this model has,
-- the end animation frame is greater than or equal to the
-- start animation frame.
--  This check is run only on OdysseyBases.
--
function AnimCheck OdysseyBaseNode =
(
    if DEBUG then format "AnimCheck%" g_delim to:g_strBuffer

    local animations = OdysseyBaseNode.animations
    local acceptable = true
    local foundPosStart, foundRGBStart, foundRotStart
    local foundPosEnd, foundRGBEnd, foundRotEnd
    local posInRange, rgbInRange, rotInRange
    local frame

    local animtotal = animations.count
    local animcount = 0

    progressStart "Valid Anim Key check"
    for anim in animations do
    (
        animcount += 1
        if not (progressUpdate ((animcount*100)/animtotal)) then
        (
            -- don't do anymore processing.
            return 1
        )

        local str = filterstring anim " "
        if ((str[5] as integer) == 1) then
        (
            local start = str[2] as integer - 1
            local end = str[3] as integer + 1
            if (end < start) then
            (
                format "The animation \"%\" has an end frame % less than it's start frame % %" str[1] str[3] str[2] g_delim to:g_strBuffer
                format "'Valid Anim Key' Check failed.%" g_delim to:g_strBuffer
                kx_FlushBuffer()
                return 0
            )
        ) --// if ((str[4] as integer) == 1) then
    ) --// for anim in animations do
    progressEnd()

    if (acceptable != true) then (
        format "'Valid Anim Key' Check failed.%" g_delim to:g_strBuffer
        kx_FlushBuffer()
        return 0
    )
    return 1

)

--
-- DoganCheck will check if the current node has
-- any dogan tvertices, ie: invalud numbers (1.#INF, 1.#QNAN,...)
-- If this happens then a warning will pop up specifying which node it is
--
function badfloat a =
(
    local b = (a as string)
    return( (b ==  "1.#INF")  or
            (b == "-1.#INF")  or
            (b ==  "1.#IND")  or
            (b == "-1.#IND")  or
            (b ==  "1.#QNAN")  or
            (b == "-1.#QNAN")
          )
)

function BadPoint v =
(
    return ((badfloat v.x) or (badfloat v.y) or (badfloat v.z))
)

function DoganCheck object =
(
    if DEBUG then format "DoganCheck%" g_delim to:g_strBuffer

    local acceptable = true
    if (superclassof(object) != GeometryClass) then
    (
        return 1;
    )
    local m = object.mesh
    if(m==undefined) then
    (
        return 1
    )
    local v,i
    for i=1 to m.numtverts do
    (
        if(badpoint(gettvert m i)) then
        (
            format "Texture vertex % in \"%\" has a bad value: %%" (i as string) object.name ((gettvert m i) as string) g_delim to:g_strBuffer
            kx_FlushBuffer()
            acceptable = false
        )
    )
    if (acceptable!=true) then (
        format "'Texvert Validity' Check failed.%" g_delim to:g_strBuffer
        kx_FlushBuffer()
        return 0
    )
    return 1
)


--
-- FaceCheck will check if the current node has
-- any faces.  If it has 0 faces, then a warning will
-- pop up specifying which node it is, and as if the
-- user wishes to delete that node.  If they agree and
-- that node has children, another warning will pop up
-- to inform the user of the consequences of deleting that
-- node.
--
function FaceCheck object =
(
    if DEBUG then format "FaceCheck%" g_delim to:g_strBuffer

    if (superclassof(object) == GeometryClass) then
    (
        if (object.mesh.numfaces == 0) then
        (
            local str = "The node \"" + object.name + "\" has \n"+
                  "0 faces and will not be seen in the engine.\n"+
                  "Do you wish to delete this node?"
            local res = queryBox str title:"Sanity Checker:Face Check" beep:true

            local canContinue = true
            if (res == true) then
            (
                if (object.children.count > 0) then
                (
                    msg = "The node \"" + object.name + "\" is about \n"+
                          "to be deleted but it has children.  If you \n"+
                          "delete this node, it's children will no longer \n"+
                          "be parented.  Do you wish to continue?"
                    res2 = queryBox msg title:"Sanity Checker:Face Check" beep:true

                    if (res2 == false) then
                    (
                        canContinue = false
                        format "'Zero Faces On a Node' Check failed.%" g_delim to:g_strBuffer
                        kx_FlushBuffer()
                        return 0
                    )
                )

                if (canContinue == true) then
                (
                    delete object
                    return 2
                )
            )
            else (
                format "'Zero Faces On a Node' Check failed.%" g_delim to:g_strBuffer
                kx_FlushBuffer()
                return 0
            )
        )
    )
    return 1
)

--
-- Collision Mesh Check: verifies that only one collision mesh was specified per node.
--
function CollisionMeshCheck node =
(
    if DEBUG then format "CollisionMeshCheck%" g_delim to:g_strBuffer

    if (node.modifiers["OdysseyWalkmesh"] != undefined and node.modifiers["OdysseyWalkmesh"].meshtype == 1) then
    (
        if (FoundCollisionMesh == true) then
        (
            format "Multiple collision meshes (aabb's) found on the current model.  The last mesh was found on %%" node.name g_delim to:g_strBuffer
            format "'Collision Mesh Validity' Check failed.%" g_delim to:g_strBuffer
            kx_FlushBuffer()
            return 0
        )
        else
        (
            FoundCollisionMesh = true
        )
    )

    return 1
)

--
-- WeldCheck is used to test if there are any vertices that are really close together
-- and could be welded into a single vertex.  It prints a list to the Listener window.
--
function WeldCheck object = (
    if DEBUG then format "WeldCheck%" g_delim to:g_strBuffer

    local i,vert1,vert2;
    local objMesh

   if (superclassof(object) == geometryclass) then
   (
    local objMesh = object.mesh
    local vertexList = #()
    local vert1, vert2

    for i = 1 to objMesh.numverts do
    (
        vert1 = getvert objMesh i
        for j = i to objMesh.numverts do
        (
            if (i==j) -- Don't weld a vertex to itself
                do continue
            vert2 = getvert objMesh j

            --- We want to weld anything within a radius of half a centimetre
            if ( (CloseTo vert1 vert2 0.5) == 1) then
            (
                append vertexList i
                append vertexList vert1
                append vertexList j
                append vertexList vert2
            )
        )
    )
    if (vertexList.count > 0) then
    (
        format "###### Vertices in % need to be welded%" object.name g_delim to:g_strBuffer
        kx_FlushBuffer()
        for i=1 to vertexList.count by 4 do
        (
            format "    Weld: Vert#:% [% % %]  and  " vertexList[i] vertexList[i+1].x vertexList[i+1].y vertexList[i+1].z to:g_strBUffer
            format "Vert#:% [% % %]%" vertexList[i+2] vertexList[i+3].x vertexList[i+3].y vertexList[i+3].z g_delim to:g_strBuffer
            kx_FlushBuffer()
        )
        format "Weld Check failed.%" g_delim to:g_strBuffer
        kx_FlushBuffer()
        return 0
    )
   )
   return 1
)


--
-- BumpyShinyCheck tests for a flipped bump-mapped texture, ie: if (du X dv).N < 0
--
function BumpyShinyAlignCheck part = (
    if DEBUG then format "BumpyShinyAlignCheck%" g_delim to:g_strBuffer

    local i
    local acceptable = true
    if (superclassof(part) != GeometryClass) then
        return 1
    local partMesh = part.mesh -- get a copy of the mesh
    local v0,v1,v2, t0,t1,t2
    local pxpv, pypv, pzpv
    local pxpu, pypu, pzpu
    local tx = point3 0.0 0.0 0.0
    local ty = point3 0.0 0.0 0.0
    local invlen
    -- These will store the 5 components: x,y,z,u,v
    local vec0 = #()
    local vec1 = #()
    local curFace = #()
    for i = 1 to partMesh.numFaces do (
        local returnVal = getFace partMesh i
        curFace[1] = returnVal.x
        curFace[2] = returnVal.y
        curFace[3] = returnVal.z

        -- The 'get*' functions return a point3 instead of a 3 element array
        v0 = getVert partMesh curFace[1]
        v1 = getVert partMesh curFace[2]
        v2 = getVert partMesh curFace[3]

        returnVal = getTVFace partMesh i

        -- t* will contain u,v,w, but MAX stores them as x,y,z
        t0 = getTVert partMesh returnVal.x
        t1 = getTVert partMesh returnVal.y
        t2 = getTVert partMesh returnVal.z

        -- Need difference vectors to find cross product.
        vec0[1] = v1.x - v0.x
        vec0[2] = v1.y - v0.y
        vec0[3] = v1.z - v0.z
        vec0[4] = t1.x - t0.x
        vec0[5] = t1.y - t0.y

        vec1[1] = v2.x - v0.x
        vec1[2] = v2.y - v0.y
        vec1[3] = v2.z - v0.z
        vec1[4] = t2.x - t0.x
        vec1[5] = t2.y - t0.y

        -- Take the partial derivative of {x,y,z} WRT v
        pxpv = (vec0[4] * vec1[1] - vec0[1] * vec1[4])
        pypv = (vec0[4] * vec1[2] - vec0[2] * vec1[4])
        pzpv = (vec0[4] * vec1[3] - vec0[3] * vec1[4])
        invlen = 1/( sqrt (pxpv*pxpv + pypv*pypv + pzpv*pzpv) )
        pxpv *= invlen
        pypv *= invlen
        pzpv *= invlen

        -- Take the partial derivative of {x,y,z} WRT u
        pxpu = (vec0[5] * vec1[1] - vec0[1] * vec1[5])
        pypu = (vec0[5] * vec1[2] - vec0[2] * vec1[5])
        pzpu = (vec0[5] * vec1[3] - vec0[3] * vec1[5])
        invlen = 1/( sqrt (pxpu*pxpu + pypu*pypu + pzpu*pzpu) )
        pxpu *= invlen
        pypu *= invlen
        pzpu *= invlen

        for j = 1 to 3 do (
            local u,v,uvCross

            -- tx,tx,tz will contain the components of the tangent space basis

            --getNormal doesn't return an index, it returns the actual normal
            tz = getNormal partMesh curFace[j]

            -- Calculate the tangent vector
            v = pxpv*tz.x+pypv*tz.y+pzpv*tz.z
            tx.x = pxpv-v*tz.x
            tx.y = pypv-v*tz.y
            tx.z = pzpv-v*tz.z

            -- Calculate the binormal vector
            u = pxpu*tz.x+pypu*tz.y+pzpu*tz.z
            ty.x = pxpu-u*tz.x
            ty.y = pypu-u*tz.y
            ty.z = pzpu-u*tz.z

            uvCross = dot (cross tx ty) tz
            if (uvCross < 0) then
                acceptable = false
        )
    )
    if (acceptable == false) then (
        format "Bumpy-shiny Alignment check failed on: %  Mirrored bumpmap textures must be 'unmirrored'%" part.name g_delim to:g_strBuffer
        kx_FlushBuffer()
        return 0
    )
    else
        return 1
)

fn DuplicateNamesCheck node nameArray =
(
	if findItem nameArray (kx_lowercase node.name) > 0 then(
        format "DuplicateNames check failed on: %. Remember, names have to be unique under a case insensitive check. %" node.name g_delim to:g_strBuffer
        kx_FlushBuffer()
		return 0
	)
	append nameArray (kx_lowercase node.name)
	for child in node.children where DuplicateNamesCheck child nameArray == 0 do return 0
	return 1
)

--------------------------------------------------------------------------------
-- SanityCheck is used to enforce various properties when
-- exporting models from 3D Studio Max and GMax
-- return 1 if it passes, else 0
--------------------------------------------------------------------------------
function SanityCheck node =
(
    if iskindof node odysseybase then
    (
	    -- check to see if this base wants us to check it and its children
	    if node.sanity_check == 0 then return 1
	)
	
    format "Checking node %%" node.name g_delim to:g_strBuffer
    kx_FlushBuffer()

    local sc_acceptable = true -- assume we're good

    --
    -- Name Check verifies all node names.  Node names cannot have spaces, tabs or be longer than 16
    -- characters.  A node cannot be named 'root'.
    --
    if (runNameCheck) then
        if (NameCheck(node) == 0) then sc_acceptable = false

    kx_FlushBuffer()

    --
    -- ScaleCheck will verify that all nodes to be exported
    -- have world scale values of 1. Scaled values can still be entered
    -- into the Odyssey rollouts
    --
    if (runScaleCheck) then
        if (ScaleCheck(node) == 0) then sc_acceptable = false

    kx_FlushBuffer()

    -- if this is the model base setup all the test type flags
    if iskindof node odysseybase then
    (
		--
		-- Go through the nodes and make sure there are no duplicate names
		--
		local nameArray = #()
		if (runDuplicateNamesCheck) then
			if (DuplicateNamesCheck node nameArray == 0) then sc_acceptable = false

		kx_FlushBuffer()
		
		-- make sure we restart looking for aabbs
		global FoundCollisionMesh = false
		
		-- ask the user whether we want to fix the weights for this one
		gbl_sanity_weight_alwaysfix = 0

        --
        -- RotationCheck will verify that the OdysseyBase nodes
        -- that will be exported have zero rotation values.
        --  This check is run only on OdysseyBases.
        --
        if (runRotationCheck) then
            if (RotationCheck(node) == 0) then sc_acceptable = false

		kx_FlushBuffer()

        -- This sets up the global for part name verification
        if (node.classification == "bodypart") then
            RootNodeName = node.name
        else
            RootNodeName = undefined

        --
        -- AnimCheck will verify that for all animations this model has,
        -- the end animation frame is greater than or equal to the
        -- start animation frame.
        --  This check is run only on OdysseyBases.
        --
        if (runAnimCheck) then
            if (AnimCheck(node) == 0) then sc_acceptable = false

        kx_FlushBuffer()
    )

    --
    -- This check is for skinned characters - a vertex cannot be affected by more than
    -- 4 bones; if it is, the offending bones are printed to the Listener Window
    --
    if (node.modifiers["skin"] != undefined) then
        if (runBoneCountCheck) then
            if (BoneCountCheck(node) == 0) then sc_acceptable = false

    kx_FlushBuffer()

    --
    -- Part Texture Check: verifies that the material bitmap name is the same as the node name
    --
    if (runPartTextureCheck) then
        if (PartTextureCheck(node) == 0) then sc_acceptable = false

    kx_FlushBuffer()

    --
    -- WeldCheck is used to test if there are any vertices that are really close together
    -- and could be welded into a single vertex.  It prints a list to the Listener window.
    --
    if (runWeldCheck) then
        if (WeldCheck(node) == 0) then sc_acceptable = false

    kx_FlushBuffer()

    --
    -- Class Check: checks if a node is of type Boolean when it should be an Edit Mesh
    --
    if (runClassCheck) then
        if (ClassCheck(node) == 0) then sc_acceptable = false

    kx_FlushBuffer()

    --
    -- ControllerCheck will verify that any node to be exported
    -- with position or rotation keys implement only linear
    -- controllers (or bezier controllers for position).
    --
    if (runControllerCheck) then
        if (ControllerCheck(node) == 0) then sc_acceptable = false

    --
    -- DoganCheck will check if the current node has
    -- any dogan tvertices, ie: invalud numbers (1.#INF, 1.#QNAN,...)
    -- If this happens then a warning will pop up specifying which node it is
    --
    kx_FlushBuffer()
    if (runDoganCheck) then
        if (DoganCheck(node) == 0) then sc_acceptable = false

    kx_FlushBuffer()
	
    --
    -- BitMapNameCheck will verify that the name of the
    -- texture is at most 16 characters long
    --
    if (runBitmapNameCheck) then
        if (BitMapNameCheck(node) == 0) then sc_acceptable = false

    kx_FlushBuffer()
	
    --
    -- This check determines if the animroot node for each animation actually
    -- exists within the model
    --
    if (runAnimrootCheck) then
	(
		local animRootState = AnimrootCheck(node)
        if animRootState == 0 then sc_acceptable = false
		else if animRootState == 2 then
		(
			kx_FlushBuffer force:true
			return 2
		)
	)
    kx_FlushBuffer()

    --
    -- FaceCheck will check if the current node has
    -- any faces.  If it has 0 faces, then a warning will
    -- pop up specifying which node it is, and as if the
    -- user wishes to delete that node.  If they agree and
    -- that node has children, another warning will pop up
    -- to inform the user of the consequences of deleting that
    -- node.
    --
    if (runFaceCheck) then (
        res = FaceCheck(node)
        if (res == 0) then
        (
            sc_acceptable = false
            try (
                node.sanityok = kx_btoi(sc_acceptable)
            ) catch()
        )
        -- a faceless node was found but was deleted so continue
        else if (res == 2) then
        (
            if (sc_acceptable != true) then (
                format "Face Check failed.%" g_delim to:g_strBuffer
                kx_FlushBuffer()
                return 0
            )
            kx_FlushBuffer()
            return 1
        )
    )

    kx_FlushBuffer()
	
	--
	-- Collision Mesh Check: verifies that only one collision mesh was specified per node.
	--
    if (runAabbCheck) then
        if (CollisionMeshCheck(node) == 0) then sc_acceptable = false

    kx_FlushBuffer()
	
	-- Run the check on all the children, but skip any model bases. If you want a check on those, go run it separately.
    for c in node.children where not iskindof c odysseybase and not matchPattern c.name pattern:"ignore_*" do
    (
		local childSanityResult = SanityCheck(c)
        if childSanityResult == 0 then sc_acceptable = false
        else if childSanityResult == 2 then return 2 -- We are aborting the Sanity Check
    )

    if iskindof node odysseybase then node.sanityok = kx_btoi(sc_acceptable)

    kx_FlushBuffer force:true

    if (not sc_acceptable) then return 0
	else return 1
)



-- listener command line command
-- Usage: massmdlsanity pattern:<pattern>
fn massmdlsanity pattern:undefined =
(
    if (pattern == undefined) then
    (
        format "Usage:  massmdlsanity pattern:<filepath\pattern>\r\n"
        format "example:  massmdlsanity pattern:\"d:\\\\mymdlfiles\\\\*.mdl\"\r\n"
        format "Limitations: The total size the accumulated sanity check data must be less than 3.6MB.\r\n"
        return 0
    )

    progressStart "Mass Sanity Check"
    -- snoop code to record the sanity check run.
    -- store this run in the place the mdls are stored
    clearListener()

    local savePath = getFilenamePath pattern

    if g_ismax then
        g_strBuffer = createFile (savePath + "mass_sanitycheck.txt")
    else
        format "<snoopstart file=%mass_sanitycheck.txt>%" savePath g_delim to:g_strBuffer

    kx_FlushBuffer()
    -- get all the files to load from the pattern provided
    local filesToLoad = getFiles pattern
    local doneCount = 0
    for file in filesToLoad do
    (
        format "FILE: %%" (filenameFromPath file) g_delim to:g_strBuffer
        -- import the file
        ImportOdysseyModel file true false
        local modelBase
        for h in $helpers do
        (
            if (kx_strclassof h) == "odysseybase" then
            (
                modelBase = h
                exit
            )
        )
        -- sanity check the file
        SanityCheck modelBase
        format "--------------------===--------------------%" g_delim to:g_strBuffer
        kx_FlushBuffer()
        -- reset scene
        resetMaxFile #noPrompt
        doneCount += 1
        progressUpdate ((doneCount*100)/filesToLoad.count)
   )
    if g_ismax then
    (
        flush g_strBuffer
        close g_strBuffer
    )
    else (
        format "</snoopstart>%" g_delim to:g_strBuffer
        format "</snoopend>%" g_delim to:g_strBuffer
    )
    kx_FlushBuffer force:true
    -- tell the user we have finished
    progressEnd()
    if not g_ismax then messageBox "Processing Complete.\nDO NOT CLOSE until\nSnoop has written out the checkfile"
)
