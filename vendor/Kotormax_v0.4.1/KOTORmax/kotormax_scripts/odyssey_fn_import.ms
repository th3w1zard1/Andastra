/*-----------------------------------------------------------------------------\

    Odyssey Import Functions

    The importing of data is based on reading in the model a line at a time
    and storing the data in appropriate variables. This code is integrated
    from Wayland's v0.3b script with some additional enhancements in the
    areas of animations, pwk and dwk processing.

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

struct model_def (
    newmodel,
    setsupermodel,
    classification,
    setanimationscale
)

--DEBUG = true

-----------------------------------
-- Get is descendant of object   --
-----------------------------------
fn isdescendantof oCheck oAncestor =
(
    if oAncestor == undefined or oCheck == undefined then return false
    if oCheck == oAncestor then return true

    local oParent = oCheck.parent
    while(oParent != undefined) do
    (
        if(oParent == oAncestor) then return true
        oParent = oParent.parent
    )
    return false
)

-----------------------------------
-- The Euler Filter              --
-----------------------------------
fn kx_eulerFilter curQuat prevQuat =
(
	if prevQuat == undefined then return curQuat
	
	local curAA = (curQuat) as AngleAxis
	local prevAA = (prevQuat) as AngleAxis
	local diffAA = (curAA as quat * (inverse (prevAA as quat))) as AngleAxis
	
	--if curAA.angle == 0.0 then curAA.axis = prevAA.axis
	
	local nTimes = 0
	while abs (diffAA.angle) > 180.0 and nTimes < 6 do
	(
		--if curAA.angle == 0.0 then curAA.angle += 360.0
		--else curAA.angle += (curAA.angle / abs (curAA.angle)) * -360.0
		nTimes += 1
		curAA.angle += ((-1.0) ^ nTimes) * nTimes * 360.0
		(curAA as quat * (inverse (prevAA as quat))) as AngleAxis
	)
	
	return (curAA as quat)
)

fn kx_translateRotation curQuat baseQuat1 baseQuat2 =
(
	-- diff * q1 = q2
	-- diff = q2 * inverse(q1)
	
	local diffQuat = curQuat * inverse baseQuat1
	return (diffQuat * baseQuat2)
)

fn ListKeys obj keyfirst keylast datatype =
(
	for i = keyfirst to keylast do
	(
		local qData = at time i in coordsys parent obj.rotation.controller.value
		if datatype == "angleaxis" then qData = qData as angleaxis
		else if datatype == "eulerangles" then qData = qData as eulerangles
		local sExtra = "      "
		if getkeyindex obj.rotation.controller i > 0 then sExtra = "[key] "
		format "% -> %%\n" i sExtra qData
	)
)

fn kx_applyOrientationKey obj nTime qValue =
(
	in coordsys parent with animate on at time nTime (obj.rotation.controller.value = inverse qValue)
)

global baseRots = #()
fn kx_getNodeBaseRot node_name =
(
	for entry in baseRots do
	(
		local tokens = filterstring entry " "
		if tokens[1] == node_name then
			return quat (tokens[2] as float) (tokens[3] as float) (tokens[4] as float) (tokens[5] as float)
	)
	return quat 0 0 0 0
)

-------------------------------
--- Get Next Non-Blank line ---
-------------------------------
fn getNextNonBlankLine fileHandle=
(
    local line = ""
    while ((line == "") and (not eof fileHandle)) do
    (
        line = trimright (readDelimitedString fileHandle "\n")
    )
    return line
)


----------------------------------------------------------
-- Adds an MDL skin using the data in the weightsBuffer --
----------------------------------------------------------
fn addMDLSkin node buf mdl_base =
(
    if DEBUG then format "%SKIN PROCESSING: addMDLSkin - start: node=%\r\n" strIndent3 node
    setCommandPanelTaskMode mode:#modify
    modPanel.setCurrentObject node.skin

    if not DEBUG then
        DisableSceneRedraw()

    local v = 0
    local i = 0
    local gmax_bug
    local bonelist = #()
    local weightlist = #()
    local i
    local b -- bone index
    local boneName
    local weight
    local tok

    if DEBUG then format "%SKIN PROCESSING: build up skin vert list\r\n" strIndent3
    local allVerts = #()
    for i = 1 to (skinops.getnumbervertices (node.skin)) do
    (
        append allVerts i
    )

    if DEBUG then format "%SKIN PROCESSING: Start loading weight data\r\n" strIndent3
    -- Work through the weight data
    for data in buf do
    (
        v += 1; -- increment vertex
        tok = kx_Tokenizer()
        tok.SetString(data)
        --format "v: % " v

        if DEBUG then format "%SKIN PROCESSING: build up the bone and weight lists for the vertex\r\n" strIndent4
        -- build up the bone and weight lists for the vertex
        while (tok.PeekToken() != undefined) do
        (
            b = 0   -- bone index
            boneName = tok.ReadString()
            weight = tok.ReadFloat()
			
			if matchPattern boneName pattern:"root" then continue

            if DEBUG then format "%SKIN PROCESSING: Finding bone in skin\r\n" strIndent4
            for i = 1 to (skinops.GetNumberBones node.skin) do
            (
                if ((skinops.GetBoneName node.skin i 0) == boneName) then
                (
                    b = i
                    exit
                )
            )

            if (b == 0) then    -- bone not yet linked to skin
            (
                if DEBUG then format "%SKIN PROCESSING: Start linking bone to skin\r\n" strIndent4
				
                --boneNode = getNodeByName boneName
				-- Get bone object, taking into account that it needs to be under the right odysseybase. This should uniquely identify the node (unless the ascii is faulty).
				boneNode = undefined
				if boneName != "" and boneName != undefined then boneNode = (for p in $objects where (p.name == boneName and isdescendantof p mdl_base) collect p)[1]

                if boneNode == undefined then
                (
                    format "ERROR! Unable to find the bone node '%' in the scene! Check bones are in mdl.\r\n" boneName
                )

                skinops.AddBone node.skin boneNode 0
                b = skinops.GetNumberBones node.skin

                -- also mark this node as a bone
                --format "Setting bone states for: %\r\n" boneNode.name
                boneNode.SetBoneEnable true 0
                boneNode.BoneAutoAlign = true
                boneNode.BoneFreezeLength = true
                boneNode.ShowLinks = true
                boneNode.ShowLinksOnly = false
                boneNode.BoneScaleType = #None

                -- exclude all the vertexes for this bone
                --format "Exclude all verts for bone: %\r\n" boneNode.name
                skinOps.SelectBone (node.skin) b
                skinOps.SelectVertices (node.skin) allVerts
                skinOps.buttonExclude (node.skin)
                skinOps.SetInnerRadius (node.skin) b 1 0.0
                skinOps.SetOuterRadius (node.skin) b 1 0.0
                skinOps.SetInnerRadius (node.skin) b 2 0.0
                skinOps.SetOuterRadius (node.skin) b 2 0.0

                -- set bone as absolute
                --skinops.selectBone (node.skin) b
                --skinops.setSelectedBonePropRelative (node.skin) 1
                if DEBUG then format "%SKIN PROCESSING: End linking bone to skin\r\n" strIndent4
            )


            gmax_bug = node.numVerts
            skinops.SelectVertices (node.skin) v
            skinops.buttonInclude (node.skin)
            --gmax_bug = node.numVerts
            --skinops.SetVertexWeights (node.skin) v b weight
            if DEBUG then (
                format "%addMDLSkin: vertex:% bone:%(%) weight=%\r\n" strIndent4 v boneName b weight
            )

            -- add to bone and weight lists
            append bonelist b
            append weightlist weight

        ) -- end While
        if DEBUG then format "%SKIN PROCESSING: done building up the bone and weight lists for the vertex\r\n" strIndent4

        -- add vertex weight data
		if bonelist.count > 0 and weightlist.count > 0 then skinops.SetVertexWeights (node.skin) v bonelist weightlist
        bonelist = #()
        weightlist = #()
    )
    if DEBUG then format "%SKIN PROCESSING: Stop loading weight data\r\n" strIndent3

    if not DEBUG then
        EnableSceneRedraw()

    if DEBUG then format "%SKIN PROCESSING: end\r\n" strIndent3
)

--------------------------------------------------------------------------------
-- Import DWK/PWK file
--------------------------------------------------------------------------------
fn ImportOdysseyWalkmesh pFile type =
(
    local currentPath = getFilenamePath pFile

    -- Open up the file and check to see if its a binary file
    local binMdlStream = fopen pFile "rb"
    local isNotBinary = ReadByte binMdlStream
    fclose binMdlStream

    if (isNotBinary == 0) then MessageBox("Invalid ascii MDL file!")

    -- Open up the file
    local mdlStream = openFile pFile mode:"r"
    local quitEarly = false
    lineNum = 0
    try (
        -- Go through the whole file
        while not (eof mdlStream) and not quitEarly do
        (
            local tok = kx_Tokenizer()
            local line = getNextNonBlankLine mdlStream
            tok.SetString (line)
            local idToken = tok.ReadToken()
            if (idToken[1] == "#") then idToken = "#"

            case idToken of
            (
                "#": () -- ignore comments
                "node": -- Node Struct
                (
                    -- Define initial node data
                    objType = tok.ReadToken()
                    objName = tok.ReadToken()

                    objParent = ""

                    objPos = [0, 0, 0]
                    objRot = quat 0 0 0 0
                    objScale = [1, 1, 1]
                    objMaterial = undefined

                    numVerts = 0
                    verts = #()
                    numFaces = 0
                    faces = #()
                    numTVerts = 0
                    TVerts = #()
                    TVfaces = #()
                    smoothGroups = #()
                    surfacemats = #()
                )

                "parent": objParent = tok.ReadToken()
                "position":
                (
                    objPos = tok.ReadPoint3()
                    objPos *= 100
                )
                "orientation":
                (
                    local axis = tok.ReadPoint3()
                    local angle = RadToDeg (tok.ReadFloat())
                    objRot = quat angle axis
                )
                "verts": -- Verticies Struct
                (
                    numVerts = tok.ReadInteger()
                    for i = 1 to numVerts do
                    (
                        tok.SetString (getNextNonBlankLine mdlStream)
                        lineNum += 1
                        local p3 = tok.ReadPoint3()
                        p3 *= 100
                        append verts p3
                    )
                )
                "faces": -- Faces Struct
                (
                    local p1
                    local p3
                    numFaces = tok.ReadInteger()
                    for i = 1 to numFaces do
                    (
                        tok.SetString (getNextNonBlankLine mdlStream)
                        lineNum += 1

                        -- Get the current Face
                        -- mdl file is zero-based index, max expects
                        -- one-based index into vert array
                        p3 = (tok.ReadPoint3() + [1,1,1])
                        append faces p3

                        -- Get Smoothing Group
                        p1 = tok.ReadInteger()
                        append smoothGroups p1

                        -- Get the current TVface
                        p3 = (tok.ReadPoint3() + [1,1,1])
                        append TVfaces p3

                        -- Get Surface Material
                        p1 = tok.ReadInteger()
                        append surfacemats p1
                    )
                )
                "endnode": -- Done parsing mesh, so create the object
                (
                    if DEBUG then format "%ImportOdysseyWalkmesh: BUILD MESH OBJECT: % %\r\n" strIndent1 objType objName
                    local newObj
                    case objType of
                    (
                        "trimesh":
                        (
                            -- Make material, if necessary
                            -- Texture is allways assumed to be "" as there is no purpose
                            -- in having a texture/bitmap for a pwk mesh
                            -- if we are in a trimesh in a pwk or dwk file it
                            -- needs a walkmesh material against it
                            objMaterial = kx_createWalkMaterial()

                            -- Make an editable mesh
                            local newObj = mesh name:objName \
                                            vertices:verts \
                                            faces:faces \
                                            material:objMaterial \
                                            materialIDS:surfacemats

                            -- special post processing to deal with some sort of odd Max vert position distortion
                            for verti = 1 to verts.count do
                            (
                                setVert newObj verti verts[verti]
                            )
                            update newObj

                            --set vertex colors to work
                            kx_enableMapChannel newObj 0 true
                            setNumCPVVerts newObj 1
                            meshop.setMapVert newObj 0 1 [1, 1, 1]
                            buildVCFaces newObj
                            for i = 1 to faces.count do
                            (
                                local cei = #(1, 1, 1)
                                setVCFace newObj i [cei[1], cei[2], cei[3]]
                            )

                            -- Assign smoothing groups
                            for i = 1 to smoothGroups.count do
                                setFaceSmoothGroup newObj i smoothGroups[i]

                            -- Assign Walkmesh modifier
                            odyssey_mod = odysseywalkmesh()
                            if(matchPattern objName pattern:"*_wg_closed") then odyssey_mod.meshtype = 3
                            else if(matchPattern objName pattern:"*_wg_open1") then odyssey_mod.meshtype = 4
                            else if(matchPattern objName pattern:"*_wg_open2") then odyssey_mod.meshtype = 5
                            else if(matchPattern objName pattern:"*_wg") then odyssey_mod.meshtype = 2
                            addModifier newObj odyssey_mod

                            -- Update the mesh
                            update newObj

                        ) --End trimesh

                        default:
                        (
                            newObj = dummy name:objName
                            newObj.boxsize = [5,5,5]
                        )
                    ) --End objType case

                    -- get the parent node, if we can
                    local itsParent = undefined

                    if objParent != "" and objParent != undefined and objParent != "NULL" then
                        itsParent = getNodeByName objParent exact:true
                    else objParent = "Walkmesh_Root"

                    if itsParent != undefined then
                    (
                        -- parent exists
                        newObj.parent = itsParent
                    )
                    else
                    (
                        -- Build the parent node as it doesn't exist
                        local newParent = dummy name:objParent
                        newObj.parent = newParent
                    )

                    -- Set orientation and position
                    in coordsys parent(
                        newObj.rotation = objRot
                        newObj.position = objPos
                    )

                ) -- End endnode case
            ) -- end iToken case
        ) -- end while
    )
    catch
    (
        messageBox ("Odyssey importer Script Error reading pwk/dwk file at line:" + lineNum as string)
        close mdlStream
        throw
    )
    close mdlStream
)

--------------------------------------------------------------------------------
-- Import MDL file
--------------------------------------------------------------------------------
fn ImportOdysseyModel pFile importAnims showWarnings \
                loadGeom:true \                    -- load geometry. If false, use the supplied modelbase as the destination for animations.
                destinationBase:undefined \           -- this is the base that gets the animations
                insertpoint:0 \                    -- time from which new animations are inserted (if they dont exist already)
                mappingtbl:undefined \             -- table of mappings for what node to load the animation keys to
                loadanimlist:undefined \           -- array of the animations that we are loading. If undefined load all
                animloadtype:0 \                   -- unused at this point
                showprogress:false \               -- shows import progress based on file size and chars read
                loadtiff:false \                   -- does something with the texture
                location:[0,0,0] \                 -- import location, only used if the following is false
                uselytposition:false               -- use layout position. If ascii does not specify it, then import at 0,0,0
                progressbar:undefined              -- progress bar to use
=
(
    -- Model heirarchy defs
    local MDL_DEF = 1
    local MDL_GEOM = 2
    local MDL_ANIM = 3
    local MDL_NOT_IN_MDL = 4

    --DEBUG = true

    -- temp modifier var
    local odyssey_mod
    -- temp helper var
    local odyssey_helper

    local currentPath = getFilenamePath pFile
	g_exportPath = currentPath

    -- only progress further if there IS a file available to process
    if not (kx_existFile pFile) then return false

    -- Open up the file and check to see if its a binary file
    local binMdlStream = fopen pFile "rb"
    local isNotBinary = ReadByte binMdlStream
    fclose binMdlStream

    if (isNotBinary == 0) then MessageBox("Invalid ascii MDL file!")

    -- make sure we reset base panel status
    setCommandPanelTaskMode mode:#create
	
	-- display prompt message
	pushPrompt ("Importing "+(getFilenameFile pFile)+". This may take a while.")

    -- Open up the file
    local mdlStream = openFile pFile mode:"r"

    --local numAnims = 0
    local bDoAnimation = false
    local objAnim = undefined
    local animGap = 10
    local lastFrame = insertpoint -- - animGap
    local firstFrame = 0
    local lineNum = 0
    local mdl_pos = MDL_DEF
    local objProps = ""
    local quitEarly = false
    local bAnimIsThere = false
    local zeroPos
	baseRots = #() -- bead-v: reset the base rots

    -- Setup basis for model header
    local newModelClassification = 0
	local newModelClassificationUnk1 = 0
    local newModelAnimscale = 1.0
    local newModelSupermodel = "NULL"
    local newModelIgnoreFog = 0
    local newModelCompressQuats = 0
    local newModelHeadLink = 0
    local newModelBMin = [-5,-5,-1]
    local newModelBMax = [5,5,10]
    local newModelRadius = 7.0

    -- handle to this files model base
    local model_base = undefined

    -- Joco 24-Aug-03
    -- to fix having to have all the skinmeshes at the end of the mdl file
    -- we store the relevant data in this array using the structure SkinWeights.
    -- We then post process this data against the skin modifiers after the mdl
    -- file has been completely read, and therefore all the "bones" will have been
    -- processed.
    local weightsList = #()
    local newObj
    local tgaFilename
    local bmpFilename
    local matName
    local bmp
    local texMap
    local texAlphaMap

    local objFadingLight

    try (
        -- Go through the whole mdl file
        if showprogress then progressStart "Importing MDL"
        local fsize = kx_filesize mdlStream
		local bNoSkipLine = false
		local NameArray = #()
		local line = ""
		local bFirstNode = false

        while not (eof mdlStream) and not quitEarly do
        (
            if showprogress then progressUpdate ((((filepos mdlStream) as float)/fsize) * 100)
            local tok = kx_Tokenizer()
			if not bNoSkipLine then
			(
				line = getNextNonBlankLine mdlStream
				lineNum += 1
			)
			else bNoSkipLine = false
			
			tok.SetString (line)

            -- Read the data ID tag
            local idToken = tok.ReadToken()

            -- if idToken is a string token the convert to lowercase
            if (isKindOf idToken string) then idToken = kx_lowercase idToken

            if (idToken[1] == "#") then idToken = "#"
            if (idToken == "donemodel") then
            (
                mdl_pos = MDL_NOT_IN_MDL
                quitEarly = true
            )

            case mdl_pos of (
                ----------------------------------------------------------------
                -- Model definition
                ----------------------------------------------------------------
                MDL_DEF:
                (
                    if DEBUG then format "MDL_DEF:  Line:% :: %\r\n" lineNum idToken
                    case idToken of
                    (
                        "#": () -- ignore comments
                        "newmodel": newModelName = tok.ReadString()
                        "setsupermodel":
                        (
                            tok.ReadToken();
                            if tok.PeekToken() != undefined then
	                            newModelSupermodel = tok.ReadString()
                            else --1
	                            newModelSupermodel = "NULL"
                        )
                        "classification":
                        (
                            if DEBUG then format "classification processing START\r\n"
                            local tempStr = kx_lowercase (tok.ReadString())
                            -- deal with case where effect comes through as effects
                            if tempStr == "effects" then tempStr = "effect"
                            newModelClassification = findItem odysseyClassTypes tempStr
                            if newModelClassification == 0 then newModelClassification = 1
                            if DEBUG then format "classification processing END\r\n"
                        )
                        "classification_unk1": newModelClassificationUnk1 = tok.ReadInteger()
                        "setanimationscale": newModelAnimscale = tok.ReadFloat()
                        "ignorefog": newModelIgnoreFog = tok.ReadInteger()
                        "compress_quaternions": newModelCompressQuats = tok.ReadInteger()
                        "headlink": newModelHeadLink = tok.ReadInteger()
                        "beginmodelgeom": mdl_pos = MDL_GEOM
                        -- trap anything not parsed
                        default: objProps += idToken + "=" + tok.ReadRemainingTokens() + "\r\n"
                    )
                ) -- End MDL_DEF case

                -- Model Geometry
                MDL_GEOM:
                (
                    if DEBUG then format "MDL_GEOM:  Line:% :: % :: %\r\n" lineNum idToken tok.tokens
					if not bFirstNode and idToken == "bmin" then newModelBMin = tok.ReadPoint3()
					else if not bFirstNode and idToken == "bmax" then newModelBMax = tok.ReadPoint3()
					else if not bFirstNode and idToken == "radius" then newModelRadius = tok.ReadFloat()
					else if not bFirstNode and idToken == "layoutposition" then objLayoutPos = tok.ReadPoint3() * 100.0
                    else case idToken of
                    (
                        "#": () -- ignore comments
						"name": -- name saving
						(
							append (NameArray) (tok.ReadToken())
						)
                        "node": -- Node Struct
                        (
							if not bFirstNode then bFirstNode = true
                            -- Define initial node data
                            objType = tok.ReadToken()
                            objName = tok.ReadToken()
                            objWireColor = color 255 255 255
                            objAmbient = color 255 255 255
                            objDiffuse = color 255 255 255
                            objSpecular = color 0 0 0
                            objSelfIllum = color 0 0 0
                            objAlpha = 1
                            objShine = 0

                            objDanglymesh = 0
                            objPeriod = 0.0
                            objTightness = 0.0
                            objDisplacement = 0.0

                            objPos = [0, 0, 0]
                            objRot = quat 0 0 0 0
                            objScale = [1, 1, 1]
                            objScale_float = 1.0
                            objMaterial = undefined
                            textureName = ""
                            textureName1 = ""
                            textureName2 = ""
                            textureName3 = ""


                            objRender = 1
                            objShadow = 1   -- it appears the default behaviour in the engine is for shadow unless explicitly turned off
                            objBeaming = undefined
                            objFadingLight = 0
                            objInheritcolor = 0
                            objRotatetexture = 0
                            objTransparencyhint = 0


                            numVerts = 0
                            verts = #()
                            numFaces = 0
                            faces = #()
                            numTVerts = 0
                            TVerts = #()
                            TVfaces = #()
                            numTVerts1 = 0
                            TVerts1 = #()
                            TVfaces1 = #()
                            numTVerts2 = 0
                            TVerts2 = #()
                            TVfaces2 = #()
                            numTVerts3 = 0
                            TVerts3 = #()
                            TVfaces3 = #()
                            numColors = 0
                            Colors = #()
                            Colorfaces = #()
                            smoothGroups = #()
                            surfacemats = #()
                            numConstraints = 0
                            danglyConstraints = #()
                            numWeights = 0
                            weightBuffer = #()
                            edgeIndices = #()
                            edgeTransitions = #()

                            -- lens flare var setup
                            a_texturenames = #() -- read multiple strings
                            a_flaresizes = #() -- read multiple floats
                            a_flarepositions = #() -- read multiple floats
                            a_flarecolorshifts = #() -- read multiple colors as point3

                            -- reset any emitter stuff
                            --objRender=undefined
                            --objRender_sel=undefined
                            --objSpawnType=undefined
                            --objIconSize=undefined
                            --objXsize=undefined
                            --objYsize=undefined
                            --objInherit=undefined
                            --objInherit_local=undefined
                            --objInheritvel=undefined
                            --objInherit_part=undefined
                            --objRenderorder=undefined
                            --objThreshold=undefined
                            --objCombinetime=undefined
                            --objDeadspace=undefined
                            --objColorStart=undefined
                            --objColorEnd=undefined
                            --objAlphaStart=undefined
                            --objAlphaEnd=undefined
                            --objSizeStart=undefined
                            --objSizeEnd=undefined
                            --objSizeStart_y=undefined
                            --objSizeEnd_y=undefined
                            --objBirthrate=undefined
                            --objLifeExp=undefined
                            --objMass=undefined
                            --objSpread=undefined
                            --objParticleRot=undefined
                            --objVelocity=undefined
                            --objRandvel=undefined
                            --objBounce_co=undefined
                            --objBlurlength=undefined
                            --objLoop=undefined
                            --objBounce=undefined
                            --objM_isTinted=undefined
                            --objSplat=undefined
                            --objAffectedbyWind=undefined
                            objTexture=undefined

                            --objTwosidedtext=undefined
                            --objXgrid=undefined
                            --objYgrid=undefined
                            --objFps=undefined
                            --objFrameStart=undefined
                            --objFrameEnd=undefined
                            --objRandom=undefined
                            objChunk=undefined
                            --objLightningDelay=undefined
                            --objLightningRadius=undefined
                            --objLightningSubDiv=undefined
                            --objLightningScale=undefined
                            --objBlastRadius=undefined
                            --objBlastLength=undefined
                            --objP2p=undefined
                            -- needs special processing
                            --objP2p_type=undefined
                            --objP2p_sel=undefined
                            --objP2p_bezier2=undefined
                            --objP2p_bezier3=undefined
                            --objGrav=undefined
                            --objDrag=undefined
                        )
                        "parent": parent = tok.ReadToken()
                        "position":
                        (
                            objPos = tok.ReadPoint3()
                            objPos *= 100
                        )
                        "orientation":
                        (
                            local axis = tok.ReadPoint3()
                            local angle = RadToDeg (tok.ReadFloat())
							if angle == 0.0 then objRot = quat 0 0 0 1
                            else objRot = (quat angle axis)
							append baseRots (objName + " " + objRot.x as string + " " + objRot.y as string + " " + objRot.z as string + " " + objRot.w as string)
                        )
                        "scale":
                        (
                            objScale_float = tok.ReadFloat()
                            --objScale = [objScale_float,objScale_float,objScale_float]
                            --format "Read scale prop: % \n" objScale_float
                        )
                        "aabb":
                        (
							line = getNextNonBlankLine mdlStream
							tok.SetString line
							lineNum += 1
							idToken = tok.PeekToken()
							while idToken as float != undefined do
							(
								--format "sToken = %\n" idToken
								line = getNextNonBlankLine mdlStream
                                tok.SetString line
                                lineNum += 1
								idToken = tok.PeekToken()
							)
							--format "Ending aabb = %\n" idToken
							bNoSkipLine = true
							
							/* 
							-- this is what code actually reading the aabb would look like, keep it.
                            local bSkipFirst = false
                            if(tok.PeekToken() != undefined) then bSkipFirst = true

                            for i = 1 to (numFaces * 2 - 1) do
                            (
                                if not bSkipFirst then (
                                    tok.SetString (getNextNonBlankLine mdlStream)
                                    lineNum += 1
                                )
                                else bSkipFirst = false

                                --- The data here is read, but it has nowhere to go
                                local bl = tok.ReadPoint3()
                                local tr = tok.ReadPoint3()
                                local face = tok.ReadInteger()
                                local leaf = (face != -1)
                                bl *= 100
                                tr *= 100
                                local box_pos = (bl+tr)/2
                                box_pos.z = bl.z
                                box_pos += objPos
                            )
							*/
                        )
                        "render":
                        (
                            if (kx_lowercase objType) == "emitter" then
                            (
                                objRender = tok.ReadString()
                                -- set render_sel
                                case kx_lowercase(objRender) of
                                (
                                    "normal": objRender_sel=1
                                    "linked": objRender_sel=2
                                    "billboard_to_local_z": objRender_sel=3
                                    "billboard_to_world_z": objRender_sel=4
                                    "aligned_to_world_z": objRender_sel=5
                                    "aligned_to_particle_dir": objRender_sel=6
                                    "motion_blur": objRender_sel=7
                                    default: objRender_sel=1
                                )
                            )
                            else -- is a normal mesh
                                objRender = tok.ReadInteger()
                        )
                        "wirecolor": objWireColor = tok.ReadColor()
                        "ambient": objAmbient = tok.ReadColor()
                        "diffuse": objDiffuse = tok.ReadColor()
                        "specular": objSpecular = tok.ReadColor()

                        "shininess": objShine = tok.ReadInteger()

                        "transparencyhint": objTransparencyhint = tok.ReadInteger()

                        "selfillumcolor": objSelfIllum = tok.ReadColor()

                        "danglymesh": objDanglymesh = tok.ReadInteger()
                        "period": objPeriod = tok.ReadFloat()
                        "tightness": objTightness = tok.ReadFloat()
                        "displacement": objDisplacement = tok.ReadFloat()

                        "alpha": objAlpha = tok.ReadFloat()
                -- bead-v modification: added kotor props
                        "animateuv": objAnimateuv = tok.ReadInteger()
                        "uvdirectionx": objUvdirectionx = tok.ReadFloat()
                        "uvdirectiony": objUvdirectiony = tok.ReadFloat()
                        "uvjitter": objUvjitter = tok.ReadFloat()
                        "uvjitterspeed": objUvjitterspeed = tok.ReadFloat()
                        "lightmapped": objLightmapped = tok.ReadInteger()
                        "m_bisbackgroundgeometry": objBackgroundgeometry = tok.ReadInteger()
                        "dirt_enabled": objDirt_enabled = tok.ReadInteger()
                        "dirt_texture": objDirt_texture = tok.ReadInteger()
                        "dirt_worldspace": objDirt_worldspace = tok.ReadInteger()
                        "hologram_donotdraw": objHologram_donotdraw = tok.ReadInteger()
                        "tangentspace": objTangentspace = tok.ReadInteger()
                -- end bead-v modification

                        "bitmap":
                        (
                            textureName = tok.ReadToken()
                            if (textureName == "NULL") then textureName = ""
                        )
                        "bitmap2":
                        (
                            textureName1 = tok.ReadToken()
                            if (textureName1 == "NULL") then textureName1 = ""
                        )
                        "texture0":
                        (
                            textureName2 = tok.ReadToken()
                            if (textureName2 == "NULL") then textureName2 = ""
                        )
                        "texture1":
                        (
                            textureName3 = tok.ReadToken()
                            if (textureName3 == "NULL") then textureName3 = ""
                        )
                        "rotatetexture": objRotatetexture = tok.ReadInteger()
                        "verts": -- Verticies Struct
                        (
                            numVerts = tok.ReadInteger()
                            for i = 1 to numVerts do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1
                                local p3 = tok.ReadPoint3()
                                p3 *= 100
                                append verts p3
                            )
                        )
                        "constraints": -- Dangly constraints
                        (
                            numConstraints = tok.ReadInteger()
                            for i = 1 to numConstraints do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1
                                local p1 = tok.ReadFloat()
                                append danglyConstraints p1
                            )
                        )
                        "weights": -- Weights for skin modifier
                        (
                            numWeights = tok.ReadInteger()
                            for i = 1 to numWeights do
                            (
                                append weightBuffer (getNextNonBlankLine mdlStream)
                            )
                            format "Loaded % weights\r\n" numWeights
                        )
                        "faces": -- Faces Struct
                        (
                            local p1
                            local p3
                            numFaces = tok.ReadInteger()
                            for i = 1 to numFaces do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1

                                -- Get the current Face
                                -- mdl file is zero-based index, max expects
                                -- one-based index into vert array
                                p3 = (tok.ReadPoint3() + [1,1,1])
                                append faces p3

                                -- Get Smoothing Group
                                p1 = tok.ReadInteger()
                                append smoothGroups p1

                                -- Get the current TVface
                                p3 = (tok.ReadPoint3() + [1,1,1])
                                append TVfaces p3

                                -- Get Surface Material
                                p1 = tok.ReadInteger()
                                append surfacemats p1
                            )
                        )

                        "texindices1": -- Faces Struct
                        (
                            local p3
                            --numFaces = tok.ReadInteger()
                            for i = 1 to numFaces do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1

                                -- Get the current TVface
                                p3 = (tok.ReadPoint3() + [1,1,1])
                                append TVfaces1 p3
                            )
                        )

                        "texindices2": -- Faces Struct
                        (
                            local p3
                            --numFaces = tok.ReadInteger()
                            for i = 1 to numFaces do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1

                                -- Get the current TVface
                                p3 = (tok.ReadPoint3() + [1,1,1])
                                append TVfaces2 p3
                            )
                        )

                        "texindices3": -- Faces Struct
                        (
                            local p3
                            --numFaces = tok.ReadInteger()
                            for i = 1 to numFaces do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1

                                -- Get the current TVface
                                p3 = (tok.ReadPoint3() + [1,1,1])
                                append TVfaces3 p3
                            )
                        )

                        "colorindices": -- Faces Struct
                        (
                            local p3
                            --numFaces = tok.ReadInteger()
                            for i = 1 to numFaces do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1

                                -- Get the current TVface
                                p3 = (tok.ReadPoint3() + [1,1,1])
                                append Colorfaces p3
                            )
                        )

                        "tverts": -- Verticies Struct
                        (
                            numTVerts = tok.ReadInteger()
                            for i = 1 to numTVerts do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1

                                local bv1 = tok.ReadFloat()
                                local bv2 = tok.ReadFloat()
                                local p3 = [bv1, bv2, 0]

                                append TVerts p3
                            )
                        )

                        "tverts1": -- Vertices Struct
                        (
                            numTVerts1 = tok.ReadInteger()
                            for i = 1 to numTVerts1 do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1

                                local bv1 = tok.ReadFloat()
                                local bv2 = tok.ReadFloat()
                                local p3 = [bv1, bv2, 0]

                                append TVerts1 p3
                            )
                        )
                        "tverts2": -- Vertices Struct
                        (
                            numTVerts2 = tok.ReadInteger()
                            for i = 1 to numTVerts2 do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1

                                local bv1 = tok.ReadFloat()
                                local bv2 = tok.ReadFloat()
                                local p3 = [bv1, bv2, 0]

                                append TVerts2 p3
                            )
                        )
                        "tverts3": -- Vertices Struct
                        (
                            numTVerts3 = tok.ReadInteger()
                            for i = 1 to numTVerts3 do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1

                                local bv1 = tok.ReadFloat()
                                local bv2 = tok.ReadFloat()
                                local p3 = [bv1, bv2, 0]

                                append TVerts3 p3
                            )
                        )
                        "colors": -- Vertices Struct
                        (
                            numColors = tok.ReadInteger()
                            for i = 1 to numColors do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1
								
                                local p3 = tok.ReadPoint3()

                                append Colors p3
                            )
                        )
                        "roomlinks": --Walkmesh room links
                        (
                            -- Get key count
                            local keyCount = undefined;
                            if (tok.PeekToken() != undefined) then keyCount = tok.ReadInteger()

                            -- Go to first row
                            tok.SetString (getNextNonBlankLine mdlStream)

                            while ((tok.PeekToken() != "endlist") and (keyCount != 0)) do (
                                lineNum += 1

                                -------- DO STUFF
                                local bv1 = tok.ReadInteger()
                                local bv2 = tok.ReadInteger()
                                append edgeIndices bv1
                                append edgeTransitions bv2
                                -------- DONE STUFF

                                -- read in next line from import data stream
                                if (keyCount != undefined) then(
                                    keyCount -= 1
                                    if keyCount > 0 then tok.SetString (getNextNonBlankLine mdlStream)
                                )
                                else tok.SetString (getNextNonBlankLine mdlStream)
                            )
                        )

                        --
                        -- Emitter key words
                        --
                        "update": objUpdate = tok.ReadString()
                        "update_sel": objUpdate_sel = tok.ReadInteger()
                        "blend": objBlend = tok.ReadString()
                        "blend_sel": objBlend_sel = tok.ReadInteger()
                        -- render is dealt with above.
                        "render_sel": objRender_sel = tok.ReadInteger()
                        "spawntype": objSpawnType = tok.ReadInteger()
                        "iconsize": objIconSize = tok.ReadFloat()
                        "xsize": objXsize = tok.ReadFloat()
                        "ysize": objYsize = tok.ReadFloat()
                        "inherit": objInherit = tok.ReadInteger()
                        "inherit_local": objInherit_local = tok.ReadInteger()
                        "inheritvel": objInheritvel = tok.ReadInteger()
                        "inherit_part": objInherit_part = tok.ReadInteger()
                        "renderorder": objRenderorder = tok.ReadInteger()
                        "threshold": objThreshold = tok.ReadFloat()
                        "combinetime": objCombinetime = tok.ReadFloat()
                        "deadspace": objDeadspace = tok.ReadFloat()
                        "colorstart": objColorStart = tok.ReadColor()
                        "colorend": objColorEnd = tok.ReadColor()
                        "alphastart": objAlphaStart = tok.ReadFloat()
                        "alphaend": objAlphaEnd = tok.ReadFloat()
                        "sizestart": objSizeStart = tok.ReadFloat()
                        "sizeend": objSizeEnd = tok.ReadFloat()
                        "sizestart_y": objSizeStart_y = tok.ReadFloat()
                        "sizeend_y": objSizeEnd_y = tok.ReadFloat()
                        "birthrate": objBirthrate = tok.ReadFloat()
                        "lifeexp": objLifeExp = tok.ReadFloat()
                        "mass": objMass = tok.ReadFloat()
                        "spread": objSpread = RadToDeg (tok.ReadFloat())
                        "particlerot": objParticleRot = tok.ReadFloat()
                        "velocity": objVelocity = tok.ReadFloat()
                        "randvel": objRandvel = tok.ReadFloat()
                        "bounce_co": objBounce_co = tok.ReadFloat()
                        "blurlength": objBlurlength = tok.ReadFloat()
                        "loop": objLoop = tok.ReadInteger()
                        "bounce": objBounce = tok.ReadInteger()
                        "m_istinted": objM_isTinted = tok.ReadInteger()
                        "splat": objSplat = tok.ReadInteger()
                        "affectedbywind": objAffectedbyWind = tok.ReadInteger()
                        "texture": objTexture = tok.ReadString()
                        "twosidedtext": objTwosidedtext = tok.ReadInteger()
                        "twosidedtex": objTwosidedtext = tok.ReadInteger()
                        "xgrid": objXgrid = tok.ReadInteger()
                        "ygrid": objYgrid = tok.ReadInteger()
                        "fps": objFps = tok.ReadInteger()
                        "framestart": objFrameStart = tok.ReadInteger()
                        "frameend": objFrameEnd = tok.ReadInteger()
                        "random": objRandom = tok.ReadInteger()
                        "chunkname": objChunk = tok.ReadString()
                        "lightningdelay": objLightningDelay = tok.ReadFloat()
                        "lightningradius": objLightningRadius= tok.ReadFloat()
                        "lightningsubdiv": objLightningSubDiv= tok.ReadFloat()
                        "lightningscale": objLightningScale= tok.ReadFloat()
                        "blastradius": objBlastRadius= tok.ReadFloat()
                        "blastlength": objBlastLength= tok.ReadFloat()
                        "p2p": objP2p = tok.ReadInteger()
                        "p2p_type": objP2p_type = tok.ReadString()
                        "p2p_sel": objP2p_sel = tok.ReadInteger()
                        "p2p_bezier2": objP2p_bezier2 = tok.ReadFloat()
                        "p2p_bezier3": objP2p_bezier3 = tok.ReadFloat()
                        "grav": objGrav = tok.ReadFloat()
                        "drag": objDrag = tok.ReadFloat()
             -- bead-v modification: add kotor props
                        "lightningzigzag": objLightningzigzag = tok.ReadFloat()
                        "colormid": objColorMid = tok.ReadColor()
                        "alphamid": objAlphaMid = tok.ReadFloat()
                        "sizemid": objSizeMid = tok.ReadFloat()
                        "sizemid_y": objSizeMid_y = tok.ReadFloat()
                        "percentstart": objPercentStart = tok.ReadFloat() * 100.0
                        "percentmid": objPercentMid = tok.ReadFloat() * 100.0
                        "percentend": objPercentEnd = tok.ReadFloat() * 100.0
                        "m_frandombirthrate": objRandomBirthRate = tok.ReadFloat()
                        "targetsize": objTargetsize = tok.ReadFloat()
                        "numcontrolpts": objNumcontrolpts = tok.ReadInteger()
                        "controlptradius": objControlptradius = tok.ReadFloat()
                        "controlptdelay": objControlptdelay = tok.ReadFloat()
                        "tangentspread": objTangentspread = tok.ReadFloat()
                        "tangentlength": objTangentlength = tok.ReadFloat()
                        "detonate": objDetonate = tok.ReadFloat()
                        "numbranches": objNumBranches = tok.ReadInteger()
                        "controlptsmoothing": objControlptsmoothing = tok.ReadFloat()
                        "m_bframeblending": objFrameBlending = tok.ReadInteger()
                        "depth_texture": objDepth_texture = tok.ReadInteger()
                        "m_sdepthtexturename": objDepthTextureName = tok.ReadString()
                        "emitterflag13": objEmitterFlag13 = tok.ReadInteger()
             -- end bead-v modification
                        -- Emitter key words end
						
                        -- Reference key words
                        "refmodel": objRefModel = tok.ReadString()
                        "reattachable": objReattachable = tok.ReadInteger()
                        -- Reference key words end

                        -- Light key words
                        "radius": objRadius = tok.ReadFloat()
                        "multiplier": objMultiplier = tok.ReadFloat()
             -- bead-v modification: add kotor props
                        "shadowradius": objShadowRadius = tok.ReadFloat()
                        "verticaldisplacement": objVerticalDisplacement = tok.ReadFloat()
             -- end bead-v modification
                        "lightpriority": objLightpriority = tok.ReadInteger()
                        "ndynamictype": objNdynamictype = tok.ReadInteger()
                        "color": objLight_color = tok.ReadPoint3()      -- was "light_color"
                        "ambientonly": objAmbientOnly = tok.ReadInteger()
                        "affectdynamic": objAffectDynamic = tok.ReadInteger()
                        "shadow": objShadow = tok.ReadInteger() -- is also a trimesh keyword
                        "beaming": objBeaming = tok.ReadInteger()
                        "fadinglight": objFadingLight = tok.ReadInteger()
                        -- Light key words end

                        -- Lens Flare key words
                        "lensflares": objLensflares = tok.ReadInteger()
		    -- bead-v modification: adding the "flare" keyword
                        "flare": objLensflares = tok.ReadInteger()
		    -- end bead-v modification
                        "flareradius": objFlareradius = tok.ReadInteger()
                        "texturenames": -- read multiple strings
                        (
                            numEntries = tok.ReadInteger()
                            for i = 1 to numEntries do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1

                                -- Get Lens texture
                                append a_texturenames (tok.ReadString())
                            )
                        )
                        "flaresizes": -- read multiple ints
                        (
                            numEntries = tok.ReadInteger()
                            for i = 1 to numEntries do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1

                                -- Get Lens texture
                                append a_flaresizes (tok.ReadFloat())
                            )
                        )
                        "flarepositions": -- read multiple floats
                        (
                            numEntries = tok.ReadInteger()
                            for i = 1 to numEntries do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1

                                -- Get Lens texture
                                append a_flarepositions (tok.ReadFloat())
                            )
                        )
                        "flarecolorshifts": -- read multiple colors as point3
                        (
                            numEntries = tok.ReadInteger()
                            for i = 1 to numEntries do
                            (
                                tok.SetString (getNextNonBlankLine mdlStream)
                                lineNum += 1

                                -- Get Lens texture
                                append a_flarecolorshifts (tok.ReadPoint3())
                            )
                        )
                        -- Lens Flare keywords end

                        "endnode": -- Done parsing mesh, so create the object
                        (
                            if loadGeom then
                            (
                                if DEBUG then format "%BUILD MESH OBJECT: % %\r\n" strIndent1 objType objName
                                newObj = undefined
                                case of
                                (
                                    -- modelbase
                                    (newModelName == objName):
                                    (
                                        if DEBUG then format "%BUILD OBJECT: Model Base\r\n" strIndent1
                                        -- this is the base
                                        if(destinationBase != undefined) then newObj = destinationBase
                                        else
                                        (
                                            local oNode = (for node in helpers where node.name == objName and iskindof node odysseybase collect node)[1]
                                            --format "Same named node: %, has content: %\n" oNode (kx_basehascontent oNode)
                                            if(oNode != undefined and not (kx_basehascontent oNode)) then newObj = oNode
                                            else
                                            (
                                                local vBasePosition
                                                if(not uselytposition) then vBasePosition = location
                                                else if(objLayoutPos != undefined) then vBasePosition = objLayoutPos
                                                else vBasePosition = [0,0,0]
                                                newObj = createOdysseyBase vBasePosition
                                            )
                                        )
                                        newObj.name = objName
                                        newObj.classification_sel = newModelClassification
										newObj.classification_unk1 = newModelClassificationUnk1
                                        newObj.setanimationscale = newModelAnimscale
                                        newObj.setsupermodel = newModelSupermodel
										newObj.radius = newModelRadius
										newObj.bmin = newModelBMin
										newObj.bmax = newModelBMax
										newObj.ignorefog = newModelIgnoreFog
										newObj.compressquats = newModelCompressQuats
										newObj.headlink = newModelHeadLink
                                        -- set the export path to import path
                                        newObj.export_path = currentPath

                                        -- trap handle to model base for later use
                                        model_base = newObj
                                    )

                                    (objType == "dummy"):
                                    (
                                        if DEBUG then format "%BUILD MESH OBJECT: a Dummy!\r\n" strIndent1
                                        newObj = createOdysseyDummy [0,0,0] --dummy name:objName
                                        newObj.name = objName
                                        --newObj.boxsize = [5,5,5]
                                    )

                                    -- Joco 23-Aug-03: Create light and make sure
                                    -- only previously defined attributes are applied.
                                    (objType == "light"):
                                    (
                                        newObj = createOdysseyLight [0,0,0] --odysseylight radius:1000 name:objName
                                        newObj.name = objName
                                        --newObj.delegate.boxsize = [10,10,10]
                                        newObj.wirecolor = objWireColor
                                        if objRadius!=undefined then newObj.radius = objRadius * 100
                                        -- bead-v modification: add kotor props
                                        if objShadowRadius!=undefined then newObj.shadowradius = objShadowRadius * 100
                                        if objVerticalDisplacement!=undefined then newObj.verticaldisplacement = objVerticalDisplacement * 100
                                        -- end bead-v modification
                                        if objMultiplier!=undefined then newObj.multiplier = objMultiplier -- bead-v: fixed multipler to multiplier
                                        if objLightpriority!=undefined then newObj.lightpriority = objLightpriority
                                        if objNdynamictype!=undefined then newObj.nDynamicType = objNdynamictype
                                        if objLight_color!=undefined then
                                        (
                                            newObj.rgb = (color (objLight_color.x*255) (objLight_color.y*255) (objLight_color.z*255))
                                            -- if objLight_color is < [0,0,0] then is a negtaive light
                                            if (objLight_color.x<0) or (objLight_color.y<0) or (objLight_color.z<0) then newObj.negativeLight = 1
                                        )
                                        if objAmbientOnly!=undefined then newObj.ambientOnly = objAmbientOnly
                                        if objAffectDynamic!=undefined then newObj.affectDynamic = objAffectDynamic
                                        if objShadow!=undefined then newObj.Shadow = objShadow
                                        format "#beaming=%\r\n" objBeaming
                                        if objBeaming != undefined then newObj.beaming = objBeaming
                                        if objLensflares!=undefined then newObj.lensflares = objLensflares
                                        if objFadingLight!=undefined then newObj.fadingLight = objFadingLight

                                        -- load the flare info if there is some
                                        if a_texturenames.count > 0 then
                                        (
                                            newObj.lens_flares = #()    -- just making sure is a blank array
                                            newObj.flareRadius = objFlareradius
                                            local flareIndex
                                            local flareAdd
                                            for flareIndex = 1 to a_texturenames.count do
                                            (
                                                flareAdd = a_texturenames[flareIndex]+" "+ \
                                                        (a_flaresizes[flareIndex] as string)+" "+ \
                                                        (a_flarepositions[flareIndex] as string)+" "+ \
                                                        (a_flarecolorshifts[flareIndex].x as string)+" "+ \
                                                        (a_flarecolorshifts[flareIndex].y as string)+" "+ \
                                                        (a_flarecolorshifts[flareIndex].z as string)
                                                append (newObj.lens_flares) flareAdd
                                            )
                                        )
                                        a_texturenames = #()
                                        a_flaresizes = #()
                                        a_flarepositions = #()
                                        a_flarecolorshifts = #()
                                    )

                                    -- Joco 23-Aug-03: Create emitter and make sure
                                    -- only previously defined attributes are applied
                                    (objType == "emitter"):
                                    (
                                        if DEBUG then format "%BUILD MESH OBJECT: Is an emitter!\r\n" strIndent1
                                        newObj = createOdysseyEmitter [0,0,0] --odysseyemitter name:objName
                                        newObj.name = objName
                                        objRot += quat 180 (point3 1 0 0) -- Emitters starts in +z, GMax cone manipulator starts in -z
                                        --newObj.delegate.Angle = 15;      -- make the cone narrow
                                        if objIconSize == undefined then objIconSize = 100
                                        newObj.delegate.Distance = objIconSize;  -- Should look to use Icon Size for this value, if available.
                                        newObj.wirecolor = objWireColor
                                        -- Odyssey emitters are actually square to make the "cone" a square one.
                                        newObj.delegate.UseSquare = true
                                        -- assign data values
                                        if objUpdate!=undefined then (
                                            newObj.update = objUpdate
                                            case kx_lowercase(objUpdate) of
                                            (
                                                "fountain":     objUpdate_sel=1
                                                "single":       objUpdate_sel=2
                                                "explosion":    objUpdate_sel=3
                                                "lightning":    objUpdate_sel=4
                                            )
                                        )
                                        if objUpdate_sel!=undefined then newObj.update_sel = objUpdate_sel
                                        if objBlend!=undefined then (
                                            newObj.blend = objBlend
                                            case kx_lowercase(objBlend) of
                                            (
                                                "normal": objBlend_sel = 1
                                                "punch-through":  objBlend_sel = 2
                                                "lighten": objBlend_sel = 3
                                            )
                                        )
                                        if objBlend_sel!=undefined then (
                                            --if objBlend_sel == 3 then objBlend_sel = 2
                                            newObj.blend_sel = objBlend_sel
                                        )
                                        if objRender!=undefined then newObj.render = objRender
                                        if objRender_sel!=undefined then newObj.render_sel = objRender_sel
                                        if objSpawnType!=undefined then newObj.spawntype = objSpawnType
                                        if objSpawnType!=undefined then newObj.spawntype_sel = objSpawnType + 1
                                        if objIconSize!=undefined then newObj.IconSize = objIconSize
                                        if objXsize!=undefined then newObj.xsize = objXsize
                                        if objYsize!=undefined then newObj.ysize = objYsize
                                        if objInherit!=undefined then newObj.inherit = objInherit
                                        if objInherit_local!=undefined then newObj.inherit_local = objInherit_local
                                        if objInheritvel!=undefined then newObj.inheritvel = objInheritvel
                                        if objInherit_part!=undefined then newObj.inherit_part = objInherit_part
                                        if objRenderorder!=undefined then newObj.renderorder = objRenderorder
                                        if objThreshold!=undefined then newObj.threshold = objThreshold
                                        if objCombinetime!=undefined then newObj.combinetime = objCombinetime
                                        if objDeadspace!=undefined then newObj.deadspace = objDeadspace
                                        if objColorStart!=undefined then newObj.colorStart = objColorStart
                                        if objColorEnd!=undefined then newObj.colorEnd = objColorEnd
                                        if objAlphaStart!=undefined then newObj.alphaStart = objAlphaStart
                                        if objAlphaEnd!=undefined then newObj.alphaEnd = objAlphaEnd
                                        if objSizeStart!=undefined then newObj.sizeStart = objSizeStart
                                        if objSizeEnd!=undefined then newObj.sizeEnd = objSizeEnd
                                        if objSizeStart_y!=undefined then newObj.sizeStart_y = objSizeStart_y
                                        if objSizeEnd_y!=undefined then newObj.sizeEnd_y = objSizeEnd_y
                                        if objBirthrate!=undefined then newObj.birthrate = objBirthrate
                                        if objLifeExp!=undefined then newObj.lifeExp = objLifeExp
                                        if objMass!=undefined then newObj.mass = objMass
                                        if objSpread!=undefined then newObj.spread = objSpread
                                        if objParticleRot!=undefined then newObj.particleRot = objParticleRot
                                        if objVelocity!=undefined then newObj.velocity = objVelocity
                                        if objRandvel!=undefined then newObj.randvel = objRandvel
                                        if objBounce_co!=undefined then newObj.bounce_co = objBounce_co
                                        if objBlurlength!=undefined then newObj.blurlength = objBlurlength
                                        if objLoop!=undefined then newObj.loop = objLoop
                                        if objBounce!=undefined then newObj.bounce = objBounce
                                        if objM_isTinted!=undefined then newObj.m_isTinted = objM_isTinted
                                        if objSplat!=undefined then newObj.Splat = objSplat
                                        if objAffectedbyWind!=undefined then newObj.affectedbyWind = objAffectedbyWind
                                        if objTexture!=undefined then newObj.texture = objTexture
                                        -- if objTwosidedtext!=undefined then newObj.twosidedtext = objTwosidedtext
                                        if objTwosidedtext!=undefined then newObj.twosidedtex = objTwosidedtext
                                        if objXgrid!=undefined then newObj.xgrid = objXgrid
                                        if objYgrid!=undefined then newObj.ygrid = objYgrid
                                        if objFps!=undefined then newObj.fps = objFps
                                        if objFrameStart!=undefined then newObj.frameStart = objFrameStart
                                        if objFrameEnd!=undefined then newObj.frameEnd = objFrameEnd
                                        if objRandom!=undefined then newObj.random = objRandom
                                        if objChunk!=undefined then newObj.chunkName = objChunk
                                        if objLightningDelay!=undefined then newObj.lightningDelay = objLightningDelay
                                        if objLightningRadius!=undefined then newObj.lightningRadius = objLightningRadius
                                        if objLightningSubDiv!=undefined then newObj.lightningSubDiv = objLightningSubDiv
                                        if objLightningScale!=undefined then newObj.lightningScale = objLightningScale
                                        -- bead-v modification: add kotor props
                                        if objLightningzigzag!=undefined then newObj.lightningzigzag = objLightningzigzag
                                        if objColorMid!=undefined then newObj.colorMid = objColorMid
                                        if objAlphaMid!=undefined then newObj.alphaMid = objAlphaMid
                                        if objSizeMid!=undefined then newObj.sizeMid = objSizeMid
                                        if objSizeMid_y!=undefined then newObj.sizeMid_y = objSizeMid_y
                                        if objPercentStart!=undefined then newObj.percentStart = objPercentStart
                                        if objPercentMid!=undefined then newObj.percentMid = objPercentMid
                                        if objPercentEnd!=undefined then newObj.percentEnd = objPercentEnd
                                        if objRandomBirthRate!=undefined then newObj.m_fRandomBirthRate = objRandomBirthRate
                                        if objTargetsize!=undefined then newObj.targetsize = objTargetsize
                                        if objNumcontrolpts!=undefined then newObj.numcontrolpts = objNumcontrolpts
                                        if objControlptradius!=undefined then newObj.controlptradius = objControlptradius
                                        if objControlptdelay!=undefined then newObj.controlptdelay = objControlptdelay
                                        if objTangentspread!=undefined then newObj.tangentspread = objTangentspread
                                        if objTangentlength!=undefined then newObj.tangentlength = objTangentlength
                                        if objDetonate!=undefined then newObj.detonate = objDetonate
                                        if objNumBranches!=undefined then newObj.numBranches = objNumBranches
                                        if objControlptsmoothing!=undefined then newObj.controlptsmoothing = objControlptsmoothing
                                        if objFrameBlending!=undefined then newObj.m_bFrameBlending = objFrameBlending
                                        if objDepth_texture!=undefined then newObj.depth_texture = objDepth_texture
                                        if objDepthTextureName!=undefined then newObj.m_sDepthTextureName = objDepthTextureName
                                        if objEmitterFlag13!=undefined then newObj.EmitterFlag13 = objEmitterFlag13
                                        -- end bead-v modification
                                        if objBlastRadius!=undefined then newObj.blastRadius = objBlastRadius
                                        if objBlastLength!=undefined then newObj.blastLength = objBlastLength
                                        if objP2p!=undefined then newObj.p2p = objP2p
                                        -- needs special processing
                                        if objP2p_type!=undefined then
                                        (
                                            newObj.p2p_type = objP2p_type
                                            case kx_lowercase(objP2p_type) of
                                            (
                                                "bezier": objP2p_sel = 1
                                                "gravity": objP2p_sel = 2
                                            )
                                        )
                                        if objP2p_sel!=undefined then newObj.p2p_sel = objP2p_sel
                                        if objP2p_bezier2!=undefined then newObj.p2p_bezier2 = objP2p_bezier2
                                        if objP2p_bezier3!=undefined then newObj.p2p_bezier3 = objP2p_bezier3
                                        if objGrav!=undefined then newObj.grav = objGrav
                                        if objDrag!=undefined then newObj.drag = objDrag
                                    )
                                    
                                    (objType == "reference"):
                                    (
                                        newObj = createOdysseyReference [0,0,0]
										newObj.name = objName
                                        --newObj.delegate.boxsize = [5,5,5]
                                        if objRefModel!=undefined then newObj.refModel = objRefModel
                                        if objReattachable!=undefined then newObj.reattachable = objReattachable
                                    )

                                    ((objType == "trimesh") or
                                     (objType == "danglymesh") or
                                     (objType == "skin") or
                                     (objType == "aabb") or
                                     (objType == "lightsaber")):
                                    (
                                        -- Make material, if necessary
                                        local texturepresent = false
                                        if ((textureName != "") and
                                            (textureName != "NULL") and
                                            (textureName != undefined)) then texturepresent = true

                                        local lightmappresent = false
                                        if ((textureName1 != "") and
                                            (textureName1 != "NULL") and
                                            (textureName1 != undefined)) then lightmappresent = true


                                        local NewMaterialName = undefined
                                        if(textureName != undefined and textureName != "") then NewMaterialName = textureName
                                        else NewMaterialName = "n/a"
                                        if(textureName1 != "" and textureName1 != undefined) then
                                            NewMaterialName += " "+textureName1

                                        objMaterial = sceneMaterials[NewMaterialName]

                                        if (objType == "aabb") then
                                        (
                                            if DEBUG then format "%BUILD MESH OBJECT: make aabb material\r\n" strIndent1
                                            objMaterial = kx_createWalkMaterial()
                                            --objProps = ""
                                        )

                                        if (objMaterial == undefined) and (texturepresent or lightmappresent) then
                                        (
                                            --format "objMaterial is %\n" objMaterial
                                            if DEBUG then format "%BUILD MESH OBJECT: make material and if possible find tga file\r\n" strIndent1
                                            objMaterial = standardmaterial name:NewMaterialName
                                            objMaterial.shaderType = 1 -- Blinn
                                            objMaterial.adLock = false
                                            objMaterial.adTextureLock = false
                                            objMaterial.ambient = objAmbient
                                            objMaterial.diffuse = objDiffuse
                                            objMaterial.specular = objSpecular
                                            objMaterial.glossiness = objShine
                                            objMaterial.selfillumcolor = objSelfIllum
                                            --format "Material skeleton done\n"
                                            -- alpha done on trimesh modifier only now
                                            --objMaterial.opacity = objAlpha
                                            if (texturepresent) then
                                            (
                                                -- We want to see where the texture file is.
                                                -- Look in local dir first then check in g_maps_path
                                                if DEBUG then format "Look in local dir for texture\r\n"
                                                --format "Currentpath: %, Texturename: %\n" currentPath textureName
                                                tgaFilename = currentPath + textureName + ".tga"
                                                bmpFilename = currentPath + textureName + ".bmp"
                                                --format "Texture files 1 done\n"
                                                if not ((kx_existFile tgaFilename) or (kx_existFile bmpFilename)) then
                                                (
                                                    -- search through the global bitmap paths taken from gmax
                                                    if DEBUG then format "Searching gmax global paths: %\r\n" g_maps_path
                                                    for i = 1 to (g_maps_path.count) do
                                                    (
                                                        tgaFilename = g_maps_path[i] + "\\" + textureName + ".tga"
                                                        bmpFilename = g_maps_path[i] + "\\" + textureName + ".bmp"
                                                        if ((kx_existFile tgaFilename) or (kx_existFile bmpFilename)) then exit
                                                    )
                                                )
                                                if ((kx_existFile tgaFilename) or (kx_existFile bmpFilename)) then
                                                (
                                                    if (kx_existFile tgaFilename) then
                                                        bmp = openBitMap(tgaFilename)
                                                    else
                                                        bmp = openBitMap(bmpFilename)

                                                    if loadtiff then
                                                    (
                                                        texMap = BitmapTexture bitmap:(kx_tiff_from_tex bmp) name:textureName
                                                    ) else
                                                    (
                                                        texMap = BitmapTexture bitmap:bmp name:textureName
                                                    )
                                                    objMaterial.mapAmounts[2] = 100
                                                    objMaterial.maps[2] = texMap
                                                    objMaterial.mapEnables[2] = true;
                                                    showTextureMap objMaterial texMap true
                                                )
                                                else
                                                (
                                                    format "#Missing texture file: %\r\n" textureName
                                                    if (showWarnings) then messagebox ("Missing texture file: " + textureName)
                                                )
                                            )
                                            --format "Texture done\n"
                                            if (lightmappresent) then
                                            (
                                                -- We want to see where the texture file is.
                                                -- Look in local dir first then check in g_maps_path
                                                if DEBUG then format "Look in local dir for texture1\r\n"
                                                tgaFilename = currentPath + textureName1 + ".tga"
                                                bmpFilename = currentPath + textureName1 + ".bmp"
                                                if not ((kx_existFile tgaFilename) or (kx_existFile bmpFilename)) then
                                                (
                                                    -- search through the global bitmap paths taken from gmax
                                                    if DEBUG then format "Searching gmax global paths: %\r\n" g_maps_path
                                                    for i = 1 to (g_maps_path.count) do
                                                    (
                                                        tgaFilename = g_maps_path[i] + "\\" + textureName1 + ".tga"
                                                        bmpFilename = g_maps_path[i] + "\\" + textureName1 + ".bmp"
                                                        if ((kx_existFile tgaFilename) or (kx_existFile bmpFilename)) then exit
                                                    )
                                                )
                                                if ((kx_existFile tgaFilename) or (kx_existFile bmpFilename)) then
                                                (
                                                    if (kx_existFile tgaFilename) then
                                                        bmp = openBitMap(tgaFilename)
                                                    else
                                                        bmp = openBitMap(bmpFilename)

                                                    if loadtiff then
                                                    (
                                                        texMap = BitmapTexture bitmap:(kx_tiff_from_tex bmp) name:textureName1
                                                    ) else
                                                    (
                                                        texMap = BitmapTexture bitmap:bmp name:textureName1
                                                    )
                                                    texMap.coords.mapChannel = 2
                                                    objMaterial.mapAmounts[1] = 100
                                                    objMaterial.maps[1] = texMap
                                                    objMaterial.mapEnables[1] = true;
                                                    --showTextureMap objMaterial texMap true
                                                )
                                                else
                                                (
                                                    format "#Missing texture file: %\r\n" textureName1
                                                    if (showWarnings) then messagebox ("Missing texture file: " + textureName1)
                                                )
                                            )
                                        )


                                        if (objType == "lightsaber") then
                                        (
                                            newObj = createOdysseyLightsaber [0,0,0] --OdysseyLightsaber name:objName wirecolor:objWireColor
                                            newObj.name = objName
                                            newObj.wirecolor = objWireColor

                                            -- Add in the data
                                            if verts.count >= 16 then
                                            (
                                                --blade1
                                                newObj.blade1p1x = verts[1].x
                                                newObj.blade1p1y = verts[1].y
                                                newObj.blade1p1z = verts[1].z
                                                newObj.blade1p2x = verts[2].x
                                                newObj.blade1p2y = verts[2].y
                                                newObj.blade1p2z = verts[2].z
                                                newObj.blade1p3x = verts[3].x
                                                newObj.blade1p3y = verts[3].y
                                                newObj.blade1p3z = verts[3].z
                                                newObj.blade1p4x = verts[4].x
                                                newObj.blade1p4y = verts[4].y
                                                newObj.blade1p4z = verts[4].z

                                                --control point
                                                newObj.ctrlptx = verts[5].x - verts[1].x
                                                newObj.ctrlpty = verts[5].y - verts[1].y
                                                newObj.ctrlptz = verts[5].z - verts[1].z

                                                --blade2
                                                if verts.count == 176 then
                                                (
                                                    newObj.blade2p1x = verts[89].x
                                                    newObj.blade2p1y = verts[89].y
                                                    newObj.blade2p1z = verts[89].z
                                                    newObj.blade2p2x = verts[90].x
                                                    newObj.blade2p2y = verts[90].y
                                                    newObj.blade2p2z = verts[90].z
                                                    newObj.blade2p3x = verts[91].x
                                                    newObj.blade2p3y = verts[91].y
                                                    newObj.blade2p3z = verts[91].z
                                                    newObj.blade2p4x = verts[92].x
                                                    newObj.blade2p4y = verts[92].y
                                                    newObj.blade2p4z = verts[92].z
                                                )
                                                else
                                                (
                                                    newObj.blade2p1x = verts[9].x
                                                    newObj.blade2p1y = verts[9].y
                                                    newObj.blade2p1z = verts[9].z
                                                    newObj.blade2p2x = verts[10].x
                                                    newObj.blade2p2y = verts[10].y
                                                    newObj.blade2p2z = verts[10].z
                                                    newObj.blade2p3x = verts[11].x
                                                    newObj.blade2p3y = verts[11].y
                                                    newObj.blade2p3z = verts[11].z
                                                    newObj.blade2p4x = verts[12].x
                                                    newObj.blade2p4y = verts[12].y
                                                    newObj.blade2p4z = verts[12].z
                                                )

                                                --newObj.UpdateMesh()
                                            )
                                            if tverts.count >= 16 then
                                            (
                                                for i = 1 to 16 do
                                                (
                                                    local index = i
                                                    if TVerts.count == 176 and index > 8 then index += 80
                                                    setTVert newObj.mesh i TVerts[i]
                                                )
                                            )

                                            -- apply material if defined
                                            if objMaterial != undefined then newObj.material = objMaterial

                                            -- apply scale
                                            newObj.saberscale = objScale_float
                                            --format "Scale value is %, it should be %.\n" newObj.saberscale objScale_float
                                        )
                                        else(
                                            -- Make an editable mesh
                                            if (objMaterial != undefined) then
                                            (
                                                if DEBUG then format "%BUILD MESH OBJECT: make the actual mesh with material\r\n" strIndent1
                                                newObj = mesh name:objName \
                                                                vertices:verts \
                                                                faces:faces \
                                                                wirecolor:objWireColor \
                                                                material:objMaterial \
                                                                materialIDS:surfacemats
                                            )
                                            else (
                                                if DEBUG then format "%BUILD MESH OBJECT: make the actual mesh with NO material\r\n" strIndent1
                                                newObj = mesh name:objName \
                                                                vertices:verts \
                                                                faces:faces \
                                                                wirecolor:objWireColor \
                                                                materialIDS:surfacemats
                                            )

                                            -- special post processing to deal with some sort of odd Max vert position distortion
                                            for verti = 1 to verts.count do
                                            (
                                                setVert newObj verti verts[verti]
                                            )
                                            update newObj
                                            if DEBUG then format "%BUILD MESH OBJECT: Created type: %\r\n" strIndent1 (classof newObj)

                                            -- Smoothing Groups
                                            if DEBUG then format "%BUILD MESH OBJECT: set up smoothing groups\r\n" strIndent1
                                            for i = 1 to smoothGroups.count do
                                            (
                                                setFaceSmoothGroup newObj i smoothGroups[i]
                                            )
											format "smoothing done\n"
											
											-- Tverts
                                            if TVerts.count > 0 then
                                            (
												if DEBUG then format "%BUILD MESH OBJECT: add diffuse tverts\r\n" strIndent1
                                                kx_enableMapChannel newObj kx_diffuseChannel
												--format "map channel 1 enabled\n"
                                                meshop.setNumMapVerts newObj kx_diffuseChannel TVerts.count
                                                for i = 1 to TVerts.count do meshop.setMapVert newObj kx_diffuseChannel i TVerts[i]
												meshop.setNumMapFaces newObj kx_diffuseChannel TVfaces.count
                                                for i = 1 to TVfaces.count do meshop.setMapFace newObj kx_diffuseChannel i TVfaces[i]
                                            )
											else kx_disableMapChannel newObj kx_diffuseChannel
											
											-- Tverts1
                                            if TVerts1.count > 0 then
                                            (
												if DEBUG then format "%BUILD MESH OBJECT: add ambient tverts\r\n" strIndent1
                                                kx_enableMapChannel newObj kx_ambientChannel
												--format "map channel 2 enabled\n"
                                                meshop.setNumMapVerts newObj kx_ambientChannel TVerts1.count
                                                for i = 1 to TVerts1.count do meshop.setMapVert newObj kx_ambientChannel i TVerts1[i]
												meshop.setNumMapFaces newObj kx_ambientChannel TVfaces1.count
                                                for i = 1 to TVfaces1.count do meshop.setMapFace newObj kx_ambientChannel i TVfaces1[i]
                                            )
											else kx_disableMapChannel newObj kx_ambientChannel
											
											-- Tverts2
                                            if TVerts2.count > 0 then
                                            (
												if DEBUG then format "%BUILD MESH OBJECT: add texture0 tverts\r\n" strIndent1
                                                kx_enableMapChannel newObj kx_texture0Channel
												--format "map channel 3 enabled\n"
                                                meshop.setNumMapVerts newObj kx_texture0Channel TVerts2.count
                                                for i = 1 to TVerts2.count do meshop.setMapVert newObj kx_texture0Channel i TVerts2[i]
												meshop.setNumMapFaces newObj kx_texture0Channel TVfaces2.count
                                                for i = 1 to TVfaces2.count do meshop.setMapFace newObj kx_texture0Channel i TVfaces2[i]
                                            )
											else kx_disableMapChannel newObj kx_texture0Channel
											
											-- Tverts3
                                            if TVerts3.count > 0 then
                                            (
												if DEBUG then format "%BUILD MESH OBJECT: add texture1 tverts\r\n" strIndent1
                                                kx_enableMapChannel newObj kx_texture1Channel
												--format "map channel 4 enabled\n"
                                                meshop.setNumMapVerts newObj kx_texture1Channel TVerts3.count
                                                for i = 1 to TVerts3.count do meshop.setMapVert newObj kx_texture1Channel i TVerts3[i]
												meshop.setNumMapFaces newObj kx_texture1Channel TVfaces3.count
                                                for i = 1 to TVfaces3.count do meshop.setMapFace newObj kx_texture1Channel i TVfaces3[i]
                                            )
											else kx_disableMapChannel newObj kx_texture1Channel

											-- Constraints
                                            if DEBUG then format "%BUILD MESH OBJECT: apply constraints if have them\r\n" strIndent1
                                            if objType == "danglymesh" then
                                            (
                                                -- we have a danglymesh so apply constraints
												kx_enableMapChannel newObj kx_constraintChannel
												--format "map channel 5 enabled\n"
												meshop.setNumMapVerts newObj kx_constraintChannel numConstraints keep:false
												for i = 1 to numConstraints do
												(
													local c = (color danglyConstraints[i] danglyConstraints[i] danglyConstraints[i])
													if DEBUG then format "%apply constraint: % %\r\n" strIndent2 i c
													meshop.SetVertColor newObj kx_constraintChannel i c
												)
                                            )

                                            -- Roomlinks
                                            if objType == "aabb" then
                                            (
												kx_enableMapChannel newObj kx_roomlinkChannel
												--format "map channel 6 enabled\n"
												meshop.setNumMapVerts newObj kx_roomlinkChannel (edgeIndices.count + 1)
                                                meshop.setMapVert newObj kx_roomlinkChannel 1 [1, 1, 1]
                                                for i = 2 to (edgeTransitions.count + 1) do meshop.setMapVert newObj kx_roomlinkChannel i [0, ((100 + edgeTransitions[i-1]) / 255.0), 0]
												meshop.setNumMapFaces newObj kx_roomlinkChannel numFaces
                                                for i = 1 to numFaces do
                                                (
                                                    local cei = #(1, 1, 1)
                                                    for nindex = 1 to 3 do(
                                                        for j = 1 to edgeIndices.count do
                                                        (
                                                            if((edgeIndices[j] / 3 + 1) == i and ((mod edgeIndices[j] 3) + 1) == nindex) then
                                                                cei[nindex] = j+1
                                                        )
                                                    )
													meshop.setMapFace newObj kx_roomlinkChannel i [cei[1], cei[2], cei[3]]
                                                )
                                            )

                                            -- Colors
                                            if objType != "lightsaber" and Colors.count > 0 then
                                            (
												kx_enableMapChannel newObj kx_colorChannel
                                                meshop.setNumMapVerts newObj kx_colorChannel Colors.count
                                                for i = 1 to Colors.count do meshop.setMapVert newObj kx_colorChannel i Colors[i]
												meshop.setNumMapFaces newObj kx_colorChannel Colorfaces.count
                                                for i = 1 to Colorfaces.count do meshop.setMapFace newObj kx_colorChannel i Colorfaces[i]
                                            )

                                            --
                                            -- We need to process for the appropriate
                                            -- modifiers to be applied to the mesh.
                                            --
                                            if DEBUG then format "%BUILD MESH OBJECT: process for modifiers\r\n" strIndent1
                                            if (objType == "trimesh" or objType == "danglymesh" or objType == "skin") then
                                            (
                                                if DEBUG then format "%--> OdysseyTrimesh\r\n" strIndent1
                                                -- not sure if should do this
                                                -- for all trimesh nodes or not.
                                                odyssey_mod = odysseytrimesh()
                                                odyssey_mod.scale = objScale_float
                                                odyssey_mod.render = objRender
                                                odyssey_mod.shadow = objShadow
                                                if objBeaming != undefined then odyssey_mod.beaming = objBeaming
                                                odyssey_mod.inheritcolor = objInheritcolor
                                                odyssey_mod.selfillumcolor = objSelfillum
                                                odyssey_mod.rotatetexture = objRotatetexture
                                                odyssey_mod.alpha = objAlpha
                                                odyssey_mod.transparencyhint = objTransparencyhint
                                                -- bead-v modification: added kotor props
                                                if objAnimateuv != undefined then odyssey_mod.animateuv = objAnimateuv
                                                if objUvdirectionx != undefined then odyssey_mod.uvdirectionx = objUvdirectionx
                                                if objUvdirectiony != undefined then odyssey_mod.uvdirectiony = objUvdirectiony
                                                if objUvjitter != undefined then odyssey_mod.uvjitter = objUvjitter
                                                if objUvjitterspeed != undefined then odyssey_mod.uvjitterspeed = objUvjitterspeed
                                                if objLightmapped != undefined then odyssey_mod.lightmapped = objLightmapped
                                                if objBackgroundgeometry != undefined then odyssey_mod.m_bIsBackgroundGeometry = objBackgroundgeometry
                                                if objDirt_enabled != undefined then odyssey_mod.dirt_enabled = objDirt_enabled
                                                if objDirt_texture != undefined then odyssey_mod.dirt_texture = objDirt_texture
                                                if objDirt_worldspace != undefined then odyssey_mod.dirt_worldspace = objDirt_worldspace
                                                if objHologram_donotdraw != undefined then odyssey_mod.hologram_donotdraw = objHologram_donotdraw
                                                if objTangentspace != undefined then odyssey_mod.tangentspace = objTangentspace
                                                -- end bead-v modification
                                                -- we now force extra data to always be true
                                                odyssey_mod.extra_mat_data = 1
                                                odyssey_mod.ambient = objAmbient
                                                odyssey_mod.diffuse = objDiffuse
                                                odyssey_mod.specular = objSpecular
                                                odyssey_mod.shininess = objShine

                                                addModifier newObj odyssey_mod
                                            )

                                            if objType == "danglymesh" then
                                            (
                                                if DEBUG then format "%--> OdysseyFlex\r\n" strIndent1
                                                -- create and apply the flex modifier
                                                odyssey_mod = odysseyflex()
                                                odyssey_mod.period = objPeriod
                                                odyssey_mod.tightness = objTightness
                                                odyssey_mod.displacement = objDisplacement
                                                addModifier newObj odyssey_mod
                                            )

                                            if objType == "aabb" then
                                            (
                                                if DEBUG then format "%--> OdysseyWalkmesh\r\n" strIndent1
                                                -- As the only aabb we have in an mdl file is a wok
                                                -- mesh there is only one possible meshtype.
                                                -- Remember that pwk/dwk are imported separately from
                                                -- their own files.
                                                odyssey_mod = odysseywalkmesh()
                                                odyssey_mod.meshtype = 1
                                                addModifier newObj odyssey_mod
                                            )

                                            -- Update the mesh
                                            update newObj
                                        )

                                    ) --End trimesh, danglymesh, skin, aabb CASE

                                    default:
                                    (
                                        if DEBUG then format "%BUILD MESH OBJECT: Make it a Dummy!\r\n" strIndent1
                                        newObj = dummy name:objName
                                        newObj.boxsize = [5,5,5]
                                    )
                                ) --End objType case

                                local itsParent = for p in objects where (p.name == parent and isdescendantof p model_base) collect p
                                if itsParent != undefined and itsParent.count > 0 then
                                (
                                    -- There should only be one parent at this point. If there are several, then the ascii is incorrectly built
                                    -- in that it does not have unique names, which makes it amiguous. Theres nothing we can do about that here
                                    -- because the information on which parent is the intended one is already lost.
                                    newObj.parent = itsParent[1]

                                    -- check the controller types are correct!
                                    --newObj.scale.controller = bezier_scale()
								--format "Rotation Debug 6\n"
                                    newObj.position.controller = bezier_position()
                                    newObj.rotation.controller = linear_rotation()
								--format "Rotation Debug 6 End\n"

                                    in coordsys parent
                                    (
                                        -- at time 0 newObj.scale = objScale
								--format "Rotation Debug 7\n"
                                        --at time 0 newObj.rotation = inverse objRot
										kx_applyOrientationKey newObj 0 objRot
                                        at time 0 newObj.position = objPos
								--format "Rotation Debug 7 End\n"
                                        --with animate off newObj.rotation = inverse objRot
                                        --with animate off newObj.position = objPos
                                    )
                                )

                                -- old style node data store - redundant
                                -- keeping it at the moment for debug purposes
                                setUserPropBuffer newObj objProps
                                setUserProp newObj "node_type" objType
                                objProps = ""   -- null object properties string

                                -- Skin modifier must be created after proper positioning.
                                --
                                -- PROBLEM!!! This assumes that the bones have been created
                                -- i.e. that the bones are defined in the mdl before the
                                -- the skin is.

                                -- Possible solutions:
                                -- 1. cache the weights info to an array and apply after mdl file is parsed
                                -- 2. do multiple passes across the mdl file
                                --      a. pass 1 does all normal stuff EXCEPT skins and Animations(?)
                                --      b. pass 2 does the skin and animation processing
                                --
                                -- Option 1 taken for time being.

                                if ((objType == "skin") and (numWeights == numVerts)) then
                                (
                                    if DEBUG then format "%SKIN PROCESSING\r\n" strIndent2
                                    -- Modifier must be applied here, rather than inside
                                    -- the addMDLSkin function where it should be.
                                    -- I think its a gmax maxscript bug.
                                    addmodifier newObj (skin()) -- Add skin modifier to created obj

                                    -- Joco 24-Aug-03
                                    -- Add data to the list of Weights structures
                                    -- for post processing once all the bones
                                    -- are in place
                                    append weightsList (kx_SkinWeights newObj weightBuffer)
                                )
                            ) -- End If for loadGeom
                        ) -- End endnode case and create mesh block

                        "endmodelgeom": -- End of model geometry
                        (
                            mdl_pos = MDL_ANIM
                            if (not importAnims) then quitEarly = true;

                            --
                            -- Joco 24-Aug-03
                            -- Now that all the model geometry is done (i.e. all the bones
                            -- should be built) we can post process the skin weights to their
                            -- skin mesh modifiers
                            if loadGeom then (
                                for i = 1 to weightsList.count do
                                (
									if (weightsList[i]).Object != undefined then
									(
										-- Apply weight info to the skin modifer
										addMDLSkin (weightsList[i]).Object (weightsList[i]).WeightsData model_base
										--(weightsList[i]).Object.skin.always_deform = false -- bead-v: this should allow the skin to move with the bones
										--(weightsList[i]).Object.skin.no_update = true -- bead-v: EDIT: and this.
									)
                                )
							
								-- bead-v: save collected names to base
								if model_base != undefined then model_base.extranames = NameArray
                            )
                            -- ideally let the garbage collector kill off this local var
                            weightsList = undefined
                        )

                        default:
                        (
                            objProps += idToken + "=" + tok.ReadRemainingTokens() + "\r\n"
                        )
                    ) -- end iktoken case statement
                ) -- End MDL_GEOM case


                ----------------------------------------------------------------
                -- Load model animations
                ----------------------------------------------------------------
                MDL_ANIM:
                (
                    if DEBUG then format "MDL_ANIM:  Line:% :: %\r\n" lineNum idToken

                    case idToken of
                    (
                        "#": () -- ignore comments

                        "newanim": -- New animation
                        (
                            animName = tok.ReadToken()
                            animRoot = undefined --getNodeByName(tok.ReadToken())
                            -- deal with the loading of anims only as the model base will
                            -- not likely be the same as the base refered to src anim mdl
                            if destinationBase != undefined then
                            (
                                -- make it use the one we supplied
                                animRoot = destinationBase
                            )
                            if animRoot == undefined then animRoot = model_base

                            if DEBUG then format "Anim name = %\r\n" animName

                            animLength = 0
                            numEvents = 0

                            -- tracks whether the animations we are adding actually already exists
                            bAnimIsThere = false


                            if (loadanimlist == undefined) or loadanimlist == #() or ((findItem loadanimlist animname) > 0) and animRoot != undefined then bDoAnimation = true
                            else bDoAnimation = false

                            if(bDoAnimation) then
                            (
                                -- the new first frame is the old last frame + gap
                                -- this already includes the insert point if it was specified
                                firstFrame = lastFrame + animGap

                                -- need to modify to cope with layering animation.
                                -- we dont want to add the anim name in if it already exists
                                -- only add it in if name not already there
                                for ani = 1 to animRoot.animations.count do
                                (
                                    if animRoot.animations[ani] == undefined then exit
                                    stran = filterString animRoot.animations[ani] " "
                                    if stran[1] == animName then (
                                        -- delete the animation. The keys will be left in place though
                                        deleteItem animRoot.animations ani
                                        ani -= 1
                                        /*
                                        bAnimIsThere = true
                                        -- if name is present we want to put the keys at that point
                                        -- unless insert mode 2 or 3
                                        if animloadtype == 1 then (
                                            firstFrame = stran[2] as integer
                                            -- that is: if the animation name already exists, the first frame is added to where the current first frame is.
                                            -- should we also delete the old keys ??
                                        )
                                        */
                                    )
                                )
								
								-- if we have no animation yet, then we need to make an initial boundary rotation key for all objects.
								-- lastFrame == insertpoint at this time
								if animRoot.animations.count == 0 then
								(
									for obj in $objects where isdescendantof obj animRoot and obj != animRoot and not matchPattern obj.name pattern:"ignore_*" do
									(
										if not loadGeom then
										(
											-- First, apply base rotation to insert point. In case we did geometry, then this will just be overwriting what we already did above
											kx_applyOrientationKey obj (insertpoint + 1) (kx_getNodeBaseRot obj.name)
											local zeroRot = (in coordsys parent at time (insertpoint + 1) obj.rotation) --kx_getNodeBaseRot obj.name -- 
											
											-- create boundary keys
											kx_applyOrientationKey obj (insertpoint + 2) zeroRot
											--kx_applyOrientationKey obj (lastFrame + 2) zeroRot
										)
										else
										(
											local zeroRot = (in coordsys parent at time 0 obj.rotation) --kx_getNodeBaseRot obj.name -- 
											kx_applyOrientationKey obj 1 zeroRot
										)
									)
								)

                                -- Add animation data to animation root
                                if not bAnimIsThere then (
                                    animRoot.animations[animRoot.animations.count+1] = \
                                        animName + " " + (firstFrame as string)
                                )
                            )
                        )
                        "length": -- animtion length
                        (
                            animLength = kx_round((tok.ReadToken() as float) * frameRate) as integer

                            if bDoAnimation then
                            (
                                lastFrame = firstFrame + animLength
                                if not bAnimIsThere then
                                (
                                    -- Add animation data to animation root
                                    animRoot.animations[animRoot.animations.count] = \
                                        animRoot.animations[animRoot.animations.count]+ \
                                        " " + (lastFrame as string)
                                )
								/*
								-- if we're writing out this animation, then we need to make sure that the nodes have boundary rotation keys in place. 
								-- at this point we know both the first and last frame, so might as well do it here.
								for obj in $objects where isdescendantof obj animRoot and obj != animRoot do
								(
									local zeroRot = (in coordsys parent at time 0 obj.rotation)
									
									-- create boundary keys
									kx_applyOrientationKey obj (firstFrame - 2) zeroRot
									kx_applyOrientationKey obj (lastFrame + 2) zeroRot
								)
								*/
                            )
                        )
                        "transtime": -- animtion length
                        (
                            animTrans = (tok.ReadToken() as float)

                            if bDoAnimation then
                            (
                                if not bAnimIsThere then
                                (
                                    -- Add animation data to animation root
                                    animRoot.animations[animRoot.animations.count] = \
                                        animRoot.animations[animRoot.animations.count]+ \
                                        " " + (animTrans as string) + " 1"
                                )
                            )
                        )
                        "animroot": -- animation play root
                        (
                           animRootPlay = tok.ReadToken()

                            if bDoAnimation then
                            (
                                if not bAnimIsThere then
                                (
									if animRootPlay != undefined and mappingtbl != undefined then
										for mapindex = 1 to mappingtbl.count by 2 do 
											if kx_lowercase(mappingtbl[mapindex]) == kx_lowercase(animRootPlay) then 
												animRootPlay = mappingtbl[mapindex + 1]
									
									if animRootPlay == undefined then
									(
										messagebox ("Animroot for animation " + animName + " not specified!")
										animRootPlay = "ERROR"
									)
									-- Add animation data to animation root
									animRoot.animations[animRoot.animations.count] = \
										animRoot.animations[animRoot.animations.count] + \
										" " + animRootPlay
                                )
                            )
                        )
                        "event": -- animation event
                        (
                            if bDoAnimation then
                            (
                                local event_frame = kx_round((tok.ReadFloat()*frameRate) + firstFrame)
                                local event_name = tok.ReadString()
                                local bDupEvent = false
                                -- test to see if this event is already at this frame
                                for ev in animRoot.events do
                                (
                                    strev = filterString ev " "
                                    if strev == (event_name + " " + (event_frame as string)) then (
                                        bDupEvent = true
                                    )
                                )
                                -- Add event data to animation root
                                if not bDupEvent then
                                (
									if event_name == undefined then
									(
										messagebox ("An event name in animation " + animName + " not specified!")
										event_name = "ERROR"
									)
                                    numEvents += 1 -- bead-v: moved this here, as it makes more sense
                                    animRoot.events[animRoot.events.count+1] = \
                                        event_name + " " + (event_frame as string)
                                )
                            )
                        )
                        "eventlist": -- bead-v modification
                        (
                            local keyCount = undefined;
                            if (tok.PeekToken() != undefined) then keyCount = tok.ReadInteger()

                            tok.SetString (getNextNonBlankLine mdlStream)

                            while ((tok.PeekToken() != "endlist") and (keyCount != 0)) do (
                                if bDoAnimation then
                                (
                                    local event_frame = kx_round((tok.ReadFloat()*frameRate) + firstFrame)
                                    local event_name = tok.ReadString()
                                    local bDupEvent = false
                                    -- test to see if this event is already at this frame
                                    for ev in animRoot.events do
                                    (
                                        strev = filterString ev " "
                                        if strev == (event_name + " " + (event_frame as string)) then (
                                            bDupEvent = true
                                        )
                                    )
                                    -- Add event data to animation root
                                    if not bDupEvent then
                                    (
                                        numEvents += 1
                                        animRoot.events[animRoot.events.count+1] = \
                                            event_name + " " + (event_frame as string)
                                    )
                                )

                                -- read in next line from import data stream
                                if (keyCount != undefined) then(
                                    keyCount -= 1
                                    if keyCount > 0 then tok.SetString (getNextNonBlankLine mdlStream)
                                )
                                else
                                    tok.SetString (getNextNonBlankLine mdlStream)
                            )
                        )
                        "node":
                        (
                            objType = tok.ReadString()
                            -- deal with part mapping at this point
                            -- see if the object name needs to be over-ridden. If there is no mapping table then we do nothing,
                            -- else we look for a part name to map to. no part name found means a blank string or undefined is
                            -- assigned. This should result in all the key building code being skipped until the next part is read in.
                            objName = tok.ReadString()
                            bMapped = false
                            -- process mapping table if it is provided
                            ---- this gets the object name according to the mapping table
                            if mappingtbl != undefined then
                            (
                                for mapindex = 1 to mappingtbl.count by 2 do
                                (
                                    if kx_lowercase(mappingtbl[mapindex]) == kx_lowercase(objName) then (
                                        --if DEBUG then 
										format "mapping: % --> %\r\n" mappingtbl[mapindex] objName
                                        objName = mappingtbl[mapindex + 1]
                                        bMapped = true
                                    )
                                )
                                if not bMapped then (
                                    objName = ""
                                )
                            )

                            -- Get anim object, taking into account that it needs to be under the right odysseybase. This should uniquely identify the node (unless the ascii is faulty).
                            objAnim = undefined
                            if objName != "" and objName != undefined then objAnim = (for p in $objects where (kx_lowercase(p.name) == kx_lowercase(objName) and isdescendantof p animRoot) collect p)[1]
							--if objAnim == undefined then messagebox ("Couldn't find objAnim: " + objName)
                            if (objAnim != undefined and objAnim != animRoot) then
                            (
								-- make sure the position and orientation controllers are bezier and linear
								if not iskindof objAnim.pos.controller bezier_position then objAnim.pos.controller = bezier_position()
								if not iskindof objAnim.rotation.controller linear_rotation then objAnim.rotation.controller = linear_rotation()
								
                                if bDoAnimation then
                                (
                                    -- as we do NOT want to be animating WOKs we need to supress this step in that instance
                                    if (objAnim.modifiers["OdysseyWalkmesh"] == undefined) then
                                    (
                                        -- We have an animation node obj
                                        in coordsys parent
                                        (
                                            -- collect some data on objAnim in timeslider time zero
                                            at time 0 zeroPos = objAnim.pos.controller.value
											local zeroRot = (in coordsys parent at time 0 objAnim.rotation)
											
											-- create boundary keys
											kx_applyOrientationKey objAnim (firstFrame - 2) zeroRot
											kx_applyOrientationKey objAnim (lastFrame + 2) zeroRot
                                        )
									)
                                )
                            ) -- end if

                        )
                        "parent": ()
						"extra_data":
						(
							setUserProp (objAnim) ("extra_" + animName) (tok.ReadInteger())
						)
                        "endnode":
                        (
							if DEBUG then format "endnode processing START\r\n"
                            if DEBUG then format "objName = % : objAnim = %\r\n" objName objAnim
                            objAnim = undefined
                            if DEBUG then format "endnode (in anim) processing END\r\n"
                        )
                        "doneanim":
                        (
                            bDoAnimation = false
                        )
                        default:
                        (
                            -- this traps all the other stuff in animations, including all the controller data

                            local str_token = idToken as string
                            if matchPattern str_token pattern:"*key" and objAnim != undefined then
                            (
                                -- if we get here, then we are definitely parsing a controller with a valid objAnim
                                str_token = substring str_token 1 (str_token.count - 3)
                                local bBezier = false

                                -- Determine whether we have a bezier controller on our hands
                                if(matchPattern str_token pattern:"*bezier") then
                                (
                                    str_token = substring str_token 1 (str_token.count - 6)
                                    bBezier = true
                                )

                                -- Get correct objAnim
                                local objAnimTemp = objAnim
                                if(str_token == "scale" or
                                   str_token == "alpha" or
                                   str_token == "selfillumcolor") then
                                (
                                    if objAnimTemp.modifiers["OdysseyTrimesh"] != undefined then (
                                        --format "Getting OdysseyTrimesh as source.\n"
                                        -- we have a trimesh and not a emitter
                                        objAnimTemp = objAnimTemp.modifiers["OdysseyTrimesh"]
                                    )
                                )

                                local ctrl_data = "float"
                                local objController
                                local defval
                                case str_token of
                                (
                                    -- pos & orient
                                    --- header
                                    "position": (
                                        ctrl_data = "position"
                                        if(objAnimTemp.position.controller == undefined) then objAnimTemp.position.controller = bezier_position()
                                        objController = objAnimTemp.position.controller
                                        defval = objAnimTemp.position
                                    )
                                    "orientation": (
								--format "Rotation Debug 3\n"
                                        ctrl_data = "rotation"
                                        if(objAnimTemp.rotation.controller == undefined) then objAnimTemp.rotation.controller = linear_rotation()
                                        objController = objAnimTemp.rotation.controller
                                        defval = (at time 0 objAnim.rotation.controller.value)
								--format "Rotation Debug 3 End\n"
                                    )

                                    -- colors
                                    --- mesh
                                    "selfillumcolor": (
                                        ctrl_data = "color"
                                        if(objAnimTemp.selfillumcolor.controller == undefined) then objAnimTemp.selfillumcolor.controller = bezier_color()
                                        objController = objAnimTemp.selfillumcolor.controller
                                        defval = objAnimTemp.selfillumcolor
                                    )
                                    --- light
                                    "color": (
                                        ctrl_data = "color"
                                        if(objAnimTemp.rgb.controller == undefined) then objAnimTemp.rgb.controller = bezier_color()
                                        objController = objAnimTemp.rgb.controller
                                        defval = objAnimTemp.rgb
                                    )
                                    --- emitter
                                    "colorstart": (
                                        ctrl_data = "color"
                                        if(objAnimTemp.colorStart.controller == undefined) then objAnimTemp.colorStart.controller = bezier_color()
                                        objController = objAnimTemp.colorStart.controller
                                        defval = objAnimTemp.colorStart
                                    )
                                    "colormid": (
                                        ctrl_data = "color"
                                        if(objAnimTemp.colorMid.controller == undefined) then objAnimTemp.colorMid.controller = bezier_color()
                                        objController = objAnimTemp.colorMid.controller
                                        defval = objAnimTemp.colorMid
                                    )
                                    "colorend": (
                                        ctrl_data = "color"
                                        if(objAnimTemp.colorEnd.controller == undefined) then objAnimTemp.colorEnd.controller = bezier_color()
                                        objController = objAnimTemp.colorEnd.controller
                                        defval = objAnimTemp.colorEnd
                                    )

                                    -- floats
                                    --- header
                                    "scale": (
                                        if classof objAnimTemp as string == "odysseylightsaber" then (
                                            --format "Will try to get Saber Scale (value is %).\n" objAnimTemp.saberscale
                                            if(objAnimTemp.saberscale.controller == undefined) then objAnimTemp.saberscale.controller = bezier_float()
                                            objController = objAnimTemp.saberscale.controller
                                            defval = objAnimTemp.saberscale
                                            --format "Will try to get Saber Scale (value is %).\n" objAnimTemp.saberscale
                                        )
                                        else (
                                            if(objAnimTemp.scale.controller == undefined) then objAnimTemp.scale.controller = bezier_float()
                                            objController = objAnimTemp.scale.controller
                                            defval = objAnimTemp.scale
                                        )
                                    )
                                    --- mesh
                                    "alpha": (
                                        if(objAnimTemp.alpha.controller == undefined) then objAnimTemp.alpha.controller = bezier_float()
                                        objController = objAnimTemp.alpha.controller
                                        defval = objAnimTemp.alpha
                                    )
                                    --- light
                                    "radius": (
                                        if(objAnimTemp.radius.controller == undefined) then objAnimTemp.radius.controller = bezier_float()
                                        objController = objAnimTemp.radius.controller
                                        defval = objAnimTemp.radius
                                    )
                                    "shadowradius": (
                                        if(objAnimTemp.shadowradius.controller == undefined) then objAnimTemp.shadowradius.controller = bezier_float()
                                        objController = objAnimTemp.shadowradius.controller
                                        defval = objAnimTemp.shadowradius
                                    )
                                    "verticaldisplacement": (
                                        if(objAnimTemp.verticaldisplacement.controller == undefined) then objAnimTemp.verticaldisplacement.controller = bezier_float()
                                        objController = objAnimTemp.verticaldisplacement.controller
                                        defval = objAnimTemp.verticaldisplacement
                                    )
                                    "multiplier": (
                                        if(objAnimTemp.multiplier.controller == undefined) then objAnimTemp.multiplier.controller = bezier_float()
                                        objController = objAnimTemp.multiplier.controller
                                        defval = objAnimTemp.multiplier
                                    )
                                    --- emitter
                                    "alphastart": (
                                        if(objAnimTemp.alphaStart.controller == undefined) then objAnimTemp.alphaStart.controller = bezier_float()
                                        objController = objAnimTemp.alphaStart.controller
                                        defval = objAnimTemp.alphaStart
                                    )
                                    "alphamid": (
                                        if(objAnimTemp.alphaMid.controller == undefined) then objAnimTemp.alphaMid.controller = bezier_float()
                                        objController = objAnimTemp.alphaMid.controller
                                        defval = objAnimTemp.alphaMid
                                    )
                                    "alphaend": (
                                        if(objAnimTemp.alphaEnd.controller == undefined) then objAnimTemp.alphaEnd.controller = bezier_float()
                                        objController = objAnimTemp.alphaEnd.controller
                                        defval = objAnimTemp.alphaEnd
                                    )
                                    "percentstart": (
                                        if(objAnimTemp.percentStart.controller == undefined) then objAnimTemp.percentStart.controller = bezier_float()
                                        objController = objAnimTemp.percentStart.controller
                                        defval = objAnimTemp.percentStart
                                    )
                                    "percentmid": (
                                        if(objAnimTemp.percentMid.controller == undefined) then objAnimTemp.percentMid.controller = bezier_float()
                                        objController = objAnimTemp.percentMid.controller
                                        defval = objAnimTemp.percentMid
                                    )
                                    "percentend": (
                                        if(objAnimTemp.percentEnd.controller == undefined) then objAnimTemp.percentEnd.controller = bezier_float()
                                        objController = objAnimTemp.percentEnd.controller
                                        defval = objAnimTemp.percentEnd
                                    )
                                    "sizestart": (
                                        if(objAnimTemp.sizeStart.controller == undefined) then objAnimTemp.sizeStart.controller = bezier_float()
                                        objController = objAnimTemp.sizeStart.controller
                                        defval = objAnimTemp.sizeStart
                                    )
                                    "sizemid": (
                                        if(objAnimTemp.sizeMid.controller == undefined) then objAnimTemp.sizeMid.controller = bezier_float()
                                        objController = objAnimTemp.sizeMid.controller
                                        defval = objAnimTemp.sizeMid
                                    )
                                    "sizeend": (
                                        if(objAnimTemp.sizeEnd.controller == undefined) then objAnimTemp.sizeEnd.controller = bezier_float()
                                        objController = objAnimTemp.sizeEnd.controller
                                        defval = objAnimTemp.sizeEnd
                                    )
                                    "sizestart_y": (
                                        if(objAnimTemp.sizeStart_y.controller == undefined) then objAnimTemp.sizeStart_y.controller = bezier_float()
                                        objController = objAnimTemp.sizeStart_y.controller
                                        defval = objAnimTemp.sizeStart_y
                                    )
                                    "sizemid_y": (
                                        if(objAnimTemp.sizeMid_y.controller == undefined) then objAnimTemp.sizeMid_y.controller = bezier_float()
                                        objController = objAnimTemp.sizeMid_y.controller
                                        defval = objAnimTemp.sizeMid_y
                                    )
                                    "sizeend_y": (
                                        if(objAnimTemp.sizeEnd_y.controller == undefined) then objAnimTemp.sizeEnd_y.controller = bezier_float()
                                        objController = objAnimTemp.sizeEnd_y.controller
                                        defval = objAnimTemp.sizeEnd_y
                                    )
                                    "framestart": (
                                        if(objAnimTemp.frameStart.controller == undefined) then objAnimTemp.frameStart.controller = bezier_float()
                                        objController = objAnimTemp.frameStart.controller
                                        defval = objAnimTemp.frameStart
                                    )
                                    "frameend": (
                                        if(objAnimTemp.frameEnd.controller == undefined) then objAnimTemp.frameEnd.controller = bezier_float()
                                        objController = objAnimTemp.frameEnd.controller
                                        defval = objAnimTemp.frameEnd
                                    )
                                    "fps": (
                                        if(objAnimTemp.fps.controller == undefined) then objAnimTemp.fps.controller = bezier_float()
                                        objController = objAnimTemp.fps.controller
                                        defval = objAnimTemp.fps
                                    )
                                    "lightningdelay": (
                                        if(objAnimTemp.lightningDelay.controller == undefined) then objAnimTemp.lightningDelay.controller = bezier_float()
                                        objController = objAnimTemp.lightningDelay.controller
                                        defval = objAnimTemp.lightningDelay
                                    )
                                    "lightningradius": (
                                        if(objAnimTemp.lightningRadius.controller == undefined) then objAnimTemp.lightningRadius.controller = bezier_float()
                                        objController = objAnimTemp.lightningRadius.controller
                                        defval = objAnimTemp.lightningRadius
                                    )
                                    "lightningscale": (
                                        if(objAnimTemp.lightningScale.controller == undefined) then objAnimTemp.lightningScale.controller = bezier_float()
                                        objController = objAnimTemp.lightningScale.controller
                                        defval = objAnimTemp.lightningScale
                                    )
                                    "lightningsubdiv": (
                                        if(objAnimTemp.lightningSubDiv.controller == undefined) then objAnimTemp.lightningSubDiv.controller = bezier_float()
                                        objController = objAnimTemp.lightningSubDiv.controller
                                        defval = objAnimTemp.lightningSubDiv
                                    )
                                    "lightningzigzag": (
                                        if(objAnimTemp.lightningzigzag.controller == undefined) then objAnimTemp.lightningzigzag.controller = bezier_float()
                                        objController = objAnimTemp.lightningzigzag.controller
                                        defval = objAnimTemp.lightningzigzag
                                    )
                                    "birthrate": (
                                        if(objAnimTemp.birthrate.controller == undefined) then objAnimTemp.birthrate.controller = bezier_float()
                                        objController = objAnimTemp.birthrate.controller
                                        defval = objAnimTemp.birthrate
                                    )
                                    "m_frandombirthrate": (
                                        if(objAnimTemp.m_fRandomBirthRate.controller == undefined) then objAnimTemp.m_fRandomBirthRate.controller = bezier_float()
                                        objController = objAnimTemp.m_fRandomBirthRate.controller
                                        defval = objAnimTemp.m_fRandomBirthRate
                                    )
                                    "lifeexp": (
                                        if(objAnimTemp.lifeExp.controller == undefined) then objAnimTemp.lifeExp.controller = bezier_float()
                                        objController = objAnimTemp.lifeExp.controller
                                        defval = objAnimTemp.lifeExp
                                    )
                                    "mass": (
                                        if(objAnimTemp.mass.controller == undefined) then objAnimTemp.mass.controller = bezier_float()
                                        objController = objAnimTemp.mass.controller
                                        defval = objAnimTemp.mass
                                    )
                                    "spread": (
                                        if(objAnimTemp.spread.controller == undefined) then objAnimTemp.spread.controller = bezier_float()
                                        objController = objAnimTemp.spread.controller
                                        defval = objAnimTemp.spread
                                    )
                                    "particlerot": (
                                        if(objAnimTemp.particleRot.controller == undefined) then objAnimTemp.particleRot.controller = bezier_float()
                                        objController = objAnimTemp.particleRot.controller
                                        defval = objAnimTemp.particleRot
                                    )
                                    "velocity": (
                                        if(objAnimTemp.velocity.controller == undefined) then objAnimTemp.velocity.controller = bezier_float()
                                        objController = objAnimTemp.velocity.controller
                                        defval = objAnimTemp.velocity
                                    )
                                    "randvel": (
                                        if(objAnimTemp.randvel.controller == undefined) then objAnimTemp.randvel.controller = bezier_float()
                                        objController = objAnimTemp.randvel.controller
                                        defval = objAnimTemp.randvel
                                    )
                                    "bounce_co": (
                                        if(objAnimTemp.bounce_co.controller == undefined) then objAnimTemp.bounce_co.controller = bezier_float()
                                        objController = objAnimTemp.bounce_co.controller
                                        defval = objAnimTemp.bounce_co
                                    )
                                    "blurlength": (
                                        if(objAnimTemp.blurlength.controller == undefined) then objAnimTemp.blurlength.controller = bezier_float()
                                        objController = objAnimTemp.blurlength.controller
                                        defval = objAnimTemp.blurlength
                                    )
                                    "targetsize": (
                                        if(objAnimTemp.targetsize.controller == undefined) then objAnimTemp.targetsize.controller = bezier_float()
                                        objController = objAnimTemp.targetsize.controller
                                        defval = objAnimTemp.targetsize
                                    )
                                    "numcontrolpts": (
                                        if(objAnimTemp.numcontrolpts.controller == undefined) then objAnimTemp.numcontrolpts.controller = bezier_float()
                                        objController = objAnimTemp.numcontrolpts.controller
                                        defval = objAnimTemp.numcontrolpts
                                    )
                                    "controlptradius": (
                                        if(objAnimTemp.controlptradius.controller == undefined) then objAnimTemp.controlptradius.controller = bezier_float()
                                        objController = objAnimTemp.controlptradius.controller
                                        defval = objAnimTemp.controlptradius
                                    )
                                    "controlptdelay": (
                                        if(objAnimTemp.controlptdelay.controller == undefined) then objAnimTemp.controlptdelay.controller = bezier_float()
                                        objController = objAnimTemp.controlptdelay.controller
                                        defval = objAnimTemp.controlptdelay
                                    )
                                    "tangentspread": (
                                        if(objAnimTemp.tangentspread.controller == undefined) then objAnimTemp.tangentspread.controller = bezier_float()
                                        objController = objAnimTemp.tangentspread.controller
                                        defval = objAnimTemp.tangentspread
                                    )
                                    "tangentlength": (
                                        if(objAnimTemp.tangentlength.controller == undefined) then objAnimTemp.tangentlength.controller = bezier_float()
                                        objController = objAnimTemp.tangentlength.controller
                                        defval = objAnimTemp.tangentlength
                                    )
                                    "detonate": (
                                        if(objAnimTemp.detonate.controller == undefined) then objAnimTemp.detonate.controller = bezier_float()
                                        objController = objAnimTemp.detonate.controller
                                        defval = objAnimTemp.detonate
                                    )
                                    "p2p_bezier2": (
                                        if(objAnimTemp.p2p_bezier2.controller == undefined) then objAnimTemp.p2p_bezier2.controller = bezier_float()
                                        objController = objAnimTemp.p2p_bezier2.controller
                                        defval = objAnimTemp.p2p_bezier2
                                    )
                                    "p2p_bezier3": (
                                        if(objAnimTemp.p2p_bezier3.controller == undefined) then objAnimTemp.p2p_bezier3.controller = bezier_float()
                                        objController = objAnimTemp.p2p_bezier3.controller
                                        defval = objAnimTemp.p2p_bezier3
                                    )
                                    "grav": (
                                        if(objAnimTemp.grav.controller == undefined) then objAnimTemp.grav.controller = bezier_float()
                                        objController = objAnimTemp.grav.controller
                                        defval = objAnimTemp.grav
                                    )
                                    "drag": (
                                        if(objAnimTemp.drag.controller == undefined) then objAnimTemp.drag.controller = bezier_float()
                                        objController = objAnimTemp.drag.controller
                                        defval = objAnimTemp.drag
                                    )
                                    "xsize": (
                                        if(objAnimTemp.xsize.controller == undefined) then objAnimTemp.xsize.controller = bezier_float()
                                        objController = objAnimTemp.xsize.controller
                                        defval = objAnimTemp.xsize
                                    )
                                    "ysize": (
                                        if(objAnimTemp.ysize.controller == undefined) then objAnimTemp.ysize.controller = bezier_float()
                                        objController = objAnimTemp.ysize.controller
                                        defval = objAnimTemp.ysize
                                    )
                                    "threshold": (
                                        if(objAnimTemp.threshold.controller == undefined) then objAnimTemp.threshold.controller = bezier_float()
                                        objController = objAnimTemp.threshold.controller
                                        defval = objAnimTemp.threshold
                                    )
                                    "combinetime": (
                                        if(objAnimTemp.combinetime.controller == undefined) then objAnimTemp.combinetime.controller = bezier_float()
                                        objController = objAnimTemp.combinetime.controller
                                        defval = objAnimTemp.combinetime
                                    )
                                )

                                if(objController != undefined) then(
                                    --format "About to read % as % in %\n" str_token ctrl_data objName
                                    case ctrl_data of
                                    (
                                        "position":(
                                            local keyCount = undefined;
                                            if (tok.PeekToken() != undefined) then keyCount = tok.ReadInteger()

                                            in coordsys parent(
                                                /*
                                                -- first, create the default value at time 0 if controller has no keys
                                                if (numKeys objController == 0) then
                                                (
                                                    local nullkey = addnewkey objController 0
                                                    nullkey.value = defval
                                                )
                                                */

                                                tok.SetString (getNextNonBlankLine mdlStream)
                                                lineNum += 1

                                                local bKeysPresent = false
                                                if ((tok.PeekToken() != "endlist") or (keyCount != 0)) then bKeysPresent = true

                                                local newkey  -- bead-v: defined here so that the last key is saved after the while is done

                                                if bDoAnimation and bKeysPresent then
                                                (
                                                    -- Make sure we have the 0th key down in case we do have keys
                                                    newkey = addNewKey objAnimTemp.pos.controller (firstFrame - 2)
                                                    newkey.value = zeroPos
                                                    newkey.inTangentType = #linear
                                                    newkey.outTangentType = #linear
                                                )
												local bInitial = false

                                                while ((tok.PeekToken() != "endlist") and (keyCount != 0)) do (

                                                    local keytime = kx_round(firstFrame + (frameRate*tok.ReadFloat()))

                                                    local p3 = tok.ReadPoint3()
                                                    p3 *= 100
                                                    local p3in
                                                    local p3out
                                                    if(bBezier) then (
                                                        local p3in = tok.ReadPoint3()
                                                        local p3out = tok.ReadPoint3()
                                                        if p3in == undefined then p3in = [0, 0, 0]
                                                        if p3out == undefined then p3out = [0, 0, 0]
                                                    )
                                                    if DEBUG then format "line:% keytime:%   p3:% in:% out:%\r\n" lineNum keytime p3 p3in p3out

                                                    if bDoAnimation and bKeysPresent then (
														if not bInitial then
														(
															newkey = addnewkey objAnimTemp.pos.controller (firstFrame - 1)
															newkey.value = p3
															newkey.inTangentType = #linear
															newkey.outTangentType = #linear
															
															bInitial = true
														)
														
                                                        newkey = addnewkey objAnimTemp.pos.controller keytime
                                                        newkey.value = p3
                                                        if(bBezier) then (
                                                            newkey.inTangentType = #custom
                                                            newkey.outTangentType = #custom
                                                            newkey.x_locked = false
                                                            newkey.y_locked = false
                                                            newkey.z_locked = false
                                                            newkey.inTangent = p3in
                                                            newkey.outTangent = p3out
                                                        )
                                                        else(
                                                            newkey.inTangentType = #linear
                                                            newkey.outTangentType = #linear
                                                        )
                                                    )

                                                    -- read in next line from import data stream
                                                    if (keyCount != undefined) then (
                                                        keyCount -= 1
                                                        if keyCount > 0 then
                                                        (
                                                            tok.SetString (getNextNonBlankLine mdlStream)
                                                         )
                                                    ) else
                                                    (
                                                        tok.SetString (getNextNonBlankLine mdlStream)
                                                    )
                                                    lineNum += 1
                                                ) -- end while

                                                if bDoAnimation then (
                                                    with animate on(
                                                        -- Write last two extra keys
                                                        local lastkey = addNewKey objAnimTemp.pos.controller (lastFrame + 1)
                                                        lastkey.value = newkey.value
                                                        lastkey.inTangentType = newkey.inTangentType
                                                        lastkey.outTangentType = newkey.outTangentType
                                                        lastkey.x_locked = newkey.x_locked
                                                        lastkey.y_locked = newkey.y_locked
                                                        lastkey.z_locked = newkey.z_locked
                                                        lastkey.inTangent = newkey.inTangent
                                                        lastkey.outTangent = newkey.outTangent

                                                        lastkey = addNewKey objAnimTemp.pos.controller (lastFrame + 2)
                                                        lastkey.value = zeroPos
                                                        lastkey.inTangentType = #linear
                                                        lastkey.outTangentType = #linear
                                                    )
                                                )
                                            )
                                        )
                                        "rotation":(
                                            local keyCount = undefined
                                            if (tok.PeekToken() != undefined) then keyCount = tok.ReadInteger()

											tok.SetString (getNextNonBlankLine mdlStream)

											local bKeysPresent = false
											if ((tok.PeekToken() != "endlist") or (keyCount != 0)) then bKeysPresent = true

											local zeroRot = (in coordsys parent at time 0 objAnim.rotation)
											local zeroRot2 = (in coordsys parent at time (insertpoint + 2) objAnim.rotation)
											local qPrev = zeroRot
											
											local bInitial = false
											
											while ((tok.PeekToken() != "endlist") and (keyCount != 0)) do (
												-- next line
												lineNum += 1
												local keytime = kx_round(firstFrame + (frameRate*tok.ReadFloat()))

												-- read data
												local axis = tok.ReadPoint3()
												local angle = RadToDeg (tok.ReadFloat())
												local objRotNew = ((angleaxis angle axis) as quat)
												
												if DEBUG then format "line:% keytime:%  axis:%  angle:% quaternion:%\r\n" lineNum keytime axis angle objRotNew
												
												-- The Euler Filter
												--if not loadGeom then objRotNew = kx_translateRotation objRotNew zeroRot2 zeroRot
												objRotNew = kx_eulerFilter objRotNew qPrev

												if bDoAnimation then (
													-- check if we still need to do the initial key
													if not bInitial then
													(
														kx_applyOrientationKey objAnimTemp (firstFrame - 1) objRotNew
														bInitial = true
													)
													
													kx_applyOrientationKey objAnimTemp keytime objRotNew
												)
												
												qPrev = copy objRotNew

												if (keyCount != undefined) then 
												(
													keyCount -= 1
													if keyCount > 0 then tok.SetString (getNextNonBlankLine mdlStream)
												)
												else tok.SetString (getNextNonBlankLine mdlStream)
											)
											
											-- write last key
											if bDoAnimation and bKeysPresent then (
												kx_applyOrientationKey objAnimTemp (lastFrame + 1) qPrev
											)											
                                        )
                                        "color":(
                                            -- first, create the default value at time 0 if controller has no keys
                                            if (numKeys objController == 0) then
                                            (
                                                local nullkey = addnewkey objController 0
                                                nullkey.value = defval
                                            )

                                            -- Get key count
                                            local keyCount = undefined;
                                            if (tok.PeekToken() != undefined) then keyCount = tok.ReadInteger()

                                            -- Go to first row
                                            tok.SetString (getNextNonBlankLine mdlStream)

                                            while ((tok.PeekToken() != "endlist") and (keyCount != 0)) do (
                                                lineNum += 1

                                                local keytime = kx_round(firstFrame + (frameRate*tok.ReadFloat()))

                                                local this_value = tok.ReadColor()
                                                local in_value
                                                local out_value
                                                if(bBezier) then(
                                                    in_value = tok.ReadPoint3()
                                                    out_value = tok.ReadPoint3()
                                                )

                                                if DEBUG then format "line:% keytime:%   color:%\r\n" lineNum keytime this_value

                                                if bDoAnimation then (
                                                    with animate on(
                                                        newkey = addNewKey objController keytime
                                                        newkey.value = this_value
                                                        if(bBezier) then(
                                                            newkey.inTangentType = #custom
                                                            newkey.outTangentType = #custom
                                                            newkey.x_locked = false
                                                            newkey.inTangent = in_value
                                                            newkey.outTangent = out_value

                                                        )
                                                        else(
                                                            newkey.inTangentType = #linear
                                                            newkey.outTangentType = #linear
                                                        )
                                                    )
                                                )

                                                if (keyCount != undefined) then (
                                                    keyCount -= 1
                                                    if (keyCount > 0) then tok.SetString (getNextNonBlankLine mdlStream)
                                                )
                                                else
                                                    tok.SetString (getNextNonBlankLine mdlStream)
                                            )
                                        )
                                        "float":(
                                            -- first, create the default value at time 0 if controller has no keys
                                            if (numKeys objController == 0) then
                                            (
                                                local nullkey = addnewkey objController 0
                                                nullkey.value = defval
                                            )

                                            -- Get key count
                                            local keyCount = undefined;
                                            if (tok.PeekToken() != undefined) then keyCount = tok.ReadInteger()

                                            -- Go to first row
                                            tok.SetString (getNextNonBlankLine mdlStream)

                                            local newkey
                                            while ((tok.PeekToken() != "endlist") and (keyCount != 0)) do (
                                                lineNum += 1
                                                local keytime = kx_round(firstFrame + (frameRate*tok.ReadFloat()))

                                                local this_value = tok.ReadFloat()
                                                if str_token == "radius" then this_value *= 100
                                                if str_token == "shadowradius" then this_value *= 100
                                                if str_token == "verticaldisplacement" then this_value *= 100
                                                local in_value
                                                local out_value
                                                if(bBezier) then(
                                                    in_value = tok.ReadFloat()
                                                    out_value = tok.ReadFloat()
                                                )
                                                --format "Adding float % (bezier % %) at time %\n" this_value in_value out_value keytime

                                                if bDoAnimation then (
                                                    with animate on(
                                                        newkey = addnewkey objController keytime
                                                        newkey.value = this_value
                                                        if(bBezier) then(
                                                            newkey.inTangentType = #custom
                                                            newkey.outTangentType = #custom
                                                            newkey.x_locked = false
                                                            newkey.inTangent = in_value
                                                            newkey.outTangent = out_value

                                                        )
                                                        else
                                                        (
                                                            newkey.inTangentType = #linear
                                                            newkey.outTangentType = #linear
                                                        )
                                                    )
                                                )

                                                -- read in next line from import data stream
                                                if (keyCount != undefined) then(
                                                    keyCount -= 1
                                                    if keyCount > 0 then tok.SetString (getNextNonBlankLine mdlStream)
                                                )
                                                else
                                                    tok.SetString (getNextNonBlankLine mdlStream)
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    ) -- end idtoken case
                ) -- End MDL_ANIM case

                MDL_NOT_IN_MDL:
                (
	                --
	                -- MDL is finished being loaded. Do any mandatory post processing that is needed
	                --
			        if (importAnims) then
			        (
			            if lastFrame > 0 then animationRange = (interval 0 lastFrame)
			            else animationRange = (interval 0 60)
						
						for obj in $objects where isdescendantof obj animRoot and obj != animRoot and not matchPattern obj.name pattern:"ignore_*" do
						(
							if getkeyindex obj.rotation.controller (insertpoint + 1) > 0 then deleteKey obj.rotation.controller (getkeyindex obj.rotation.controller (insertpoint + 1))
							if getkeyindex obj.rotation.controller (insertpoint + 2) > 0 then deleteKey obj.rotation.controller (getkeyindex obj.rotation.controller (insertpoint + 2))
						)
			        )
					else lastFrame = animationRange.end
                )

            ) -- end mdl_pos case statement
        ) -- end while not eof
     )
    catch
    (
        messageBox ("Odyssey importer Script Error reading mdl file at line:" + lineNum as string)
        close mdlStream
        throw
    )
    close mdlStream
    kx_FlushBuffer()
    if showprogress then progressEnd()
	popPrompt()

	kx_setinivalue #(#("init", "lastexport", g_exportPath, (g_kotormaxPath + "kotormax.ini")))
)


--------------------------------------------------------------------------------
-- Import Layout file
--------------------------------------------------------------------------------
fn ImportOdysseyLayout pFile showprogress:false withvis:true withmodels:false doanims:true suffix:"" =
(
    local currentPath = getFilenamePath pFile

    -- only progress further if there IS a file available to process
    if not (kx_existFile pFile) then return false

    -- make sure we reset base panel status
    setCommandPanelTaskMode mode:#create -- same as: max mode create ;)

    -- Open up the file
    local mdlStream = openFile pFile mode:"r"

    local lineNum = 0
    local quitEarly = false

    try (
        -- Go through the whole mdl file
        if showprogress and not withmodels then progressStart "Importing LYT"
        local fsize = kx_filesize mdlStream

        local sModuleName = (getFilenameFile pFile)
        while not (eof mdlStream) and not quitEarly do
        (
            if showprogress and not withmodels then progressUpdate ((((filepos mdlStream) as float)/fsize) * 100)
            local tok = kx_Tokenizer()
            local line = getNextNonBlankLine mdlStream
            tok.SetString (line)
            lineNum += 1

            -- Read the data ID tag
            local idToken = tok.ReadToken()

            -- if idToken is a string token then convert to lowercase
            if (isKindOf idToken string) then idToken = kx_lowercase idToken

            if (idToken[1] == "#") then idToken = "#"
            if (idToken == "donelayout") then quitEarly = true

            case idToken of
            (
                "#": () -- ignore comments
                "filedependancy": () -- nothing to do on filedependancy
                "beginlayout": () -- nothing to do on beginlayout
                "roomcount":
                (
                    -- Get count
                    local nCount = undefined;
                    if (tok.PeekToken() != undefined) then nCount = tok.ReadInteger()

                    -- Go to first row
                    tok.SetString (getNextNonBlankLine mdlStream)

                    local nLayoutNumCounter = 0

                    while (tok.PeekToken() != "endlist" and nCount != 0) do
                    (
                        lineNum += 1

                        ---- DO STUFF
                        local sNode = tok.ReadToken()
                        local vPosition = tok.ReadPoint3() * 100.0
                        local oNode = (for node in helpers where node.name == sNode and iskindof node odysseybase collect node)[1]
                        /*
                        local oNodes = #()
                        for node in helpers do
                        (
                            format "checking node: % vs name % (isBase:%)\n" node sNode (iskindof node odysseybase)
                            if node.name == sNode and iskindof node odysseybase then append oNodes Node
                        )
                        local oNode = oNodes[1]
                        */
                        if(oNode == undefined and withmodels) then
                        (
                            -- couldnt find node, try importing
                            local sFile = currentPath+sNode+suffix+".mdl.ascii"
							if (not kx_existFile sFile) then sFile = currentPath+sNode+suffix+".mdl"
                            ImportOdysseyModel \
                                sFile \                   -- filename
                                doanims \                -- import anims
                                false \                   -- dont show warnings
                                showprogress:true \      -- dont show progress
                                uselytposition:true       -- use layout position
                            oNode = (for node in helpers where node.name == sNode and iskindof node odysseybase collect node)[1]
                        )
                        if(oNode == undefined) then
                        (
                            oNode = createOdysseyBase vPosition
                            oNode.name = sNode
                        )
                        else oNode.pos = vPosition

                        oNode.export_path = currentPath
                        oNode.layouttype = 2
                        oNode.layoutnum = nLayoutNumCounter
                        nLayoutNumCounter +=1
                        -- DONE WITH STUFF

                        -- read in next line from import data stream
                        if (nCount != undefined) then
                        (
                            nCount -= 1
                            if nCount > 0 then tok.SetString (getNextNonBlankLine mdlStream)
                        )
                        else tok.SetString (getNextNonBlankLine mdlStream)
                    )
                )
                "trackcount":
                (
                    -- Get count
                    local nCount = undefined;
                    if (tok.PeekToken() != undefined) then nCount = tok.ReadInteger()

                    -- Go to first row
                    tok.SetString (getNextNonBlankLine mdlStream)

                    while (tok.PeekToken() != "endlist" and nCount != 0) do
                    (
                        lineNum += 1

                        ---- DO STUFF
                        local sNode = tok.ReadToken()
                        local vPosition = tok.ReadPoint3() * 100.0
                        local oNode = (for node in helpers where node.name == sNode and iskindof node odysseybase collect node)[1]
                        if(oNode == undefined and withmodels) then
                        (
                            -- couldnt find node, try importing
                            local sFile = currentPath+sNode+suffix+".mdl.ascii"
							if (not kx_existFile sFile) then sFile = currentPath+sNode+suffix+".mdl"
							
                            ImportOdysseyModel \
                                sFile \                   -- filename
                                doanims \                -- import anims
                                false \                   -- dont show warnings
                                showprogress:true \      -- dont show progress
                                uselytposition:true       -- use layout position
                            oNode = (for node in helpers where node.name == sNode and iskindof node odysseybase collect node)[1]
                        )
                        if(oNode == undefined) then
                        (
                            oNode = createOdysseyBase vPosition
                            oNode.name = sNode
                        )
                        else oNode.pos = vPosition

                        oNode.export_path = currentPath
                        oNode.layouttype = 3
                        -- DONE WITH STUFF

                        -- read in next line from import data stream
                        if (nCount != undefined) then
                        (
                            nCount -= 1
                            if nCount > 0 then tok.SetString (getNextNonBlankLine mdlStream)
                        )
                        else tok.SetString (getNextNonBlankLine mdlStream)
                    )
                )
                "obstaclecount":
                (
                    -- Get count
                    local nCount = undefined;
                    if (tok.PeekToken() != undefined) then nCount = tok.ReadInteger()

                    -- Go to first row
                    tok.SetString (getNextNonBlankLine mdlStream)

                    while (tok.PeekToken() != "endlist" and nCount != 0) do
                    (
                        lineNum += 1

                        ---- DO STUFF
                        local sNode = tok.ReadToken()
                        local vPosition = tok.ReadPoint3() * 100.0
                        local oNode = (for node in helpers where node.name == sNode and iskindof node odysseybase collect node)[1]
                        if(oNode == undefined and withmodels) then
                        (
                            -- couldnt find node, try importing
                            local sFile = currentPath+sNode+suffix+".mdl.ascii"
							if (not kx_existFile sFile) then sFile = currentPath+sNode+suffix+".mdl"
							
                            ImportOdysseyModel \
                                sFile \                   -- filename
                                doanims \                -- import anims
                                false \                   -- dont show warnings
                                showprogress:true \      -- dont show progress
                                uselytposition:true       -- use layout position
                            oNode = (for node in helpers where node.name == sNode and iskindof node odysseybase collect node)[1]
                        )
                        if(oNode == undefined) then
                        (
                            oNode = createOdysseyBase vPosition
                            oNode.name = sNode
                        )
                        else oNode.pos = vPosition

                        oNode.export_path = currentPath
                        oNode.layouttype = 4
                        -- DONE WITH STUFF

                        -- read in next line from import data stream
                        if (nCount != undefined) then
                        (
                            nCount -= 1
                            if nCount > 0 then tok.SetString (getNextNonBlankLine mdlStream)
                        )
                        else tok.SetString (getNextNonBlankLine mdlStream)
                    )
                )
                "doorhookcount":
                (
                    -- Get count
                    local nCount = undefined;
                    if (tok.PeekToken() != undefined) then nCount = tok.ReadInteger()

                    -- Go to first row
                    tok.SetString (getNextNonBlankLine mdlStream)

                    while (tok.PeekToken() != "endlist" and nCount != 0) do
                    (
                        lineNum += 1

                        ---- DO STUFF
                        -- these guys are imported as dummies from the .mdls
                        local sParent = tok.ReadToken()
                        local sName = tok.ReadToken()
                        local nUnknown = tok.ReadInteger()
                        local position = tok.ReadPoint3() * 100
						local qFloat4 = tok.ReadFloat()
						local qFloat1 = tok.ReadFloat()
						local qFloat2 = tok.ReadFloat()
						local qFloat3 = tok.ReadFloat()
						local objRot = quat qFloat1 qFloat2 qFloat3 qFloat4

                        local oParent = getNodeByName sParent exact:true
                        if(oParent != undefined) then
                        (
                            local oNode = (for node in helpers where node.name == sName and iskindof node dummy and node.parent == oParent collect node)[1]
                            if(oNode == undefined) then
                            (
                                oNode = createOdysseyDummy position
                                oNode.name = sName
								--format "Rotation Debug 4\n"
                                in coordsys world ( oNode.Pos = position )
                                in coordsys local ( oNode.Rotation = objRot )
								--format "Rotation Debug 4 End\n"
                                oNode.parent = oParent
                            )
                        )
                        
                        -- DONE WITH STUFF

                        -- read in next line from import data stream
                        if (nCount != undefined) then
                        (
                            nCount -= 1
                            if nCount > 0 then tok.SetString (getNextNonBlankLine mdlStream)
                        )
                        else tok.SetString (getNextNonBlankLine mdlStream)
                    )
                )
                "donelayout":()

            ) -- END case of
        ) -- END while()
    ) -- END try()
    catch
    (
        messageBox ("Odyssey Layout Importer Script Error reading LYT file at line:" + lineNum as string)
        close mdlStream
        throw
    )
    close mdlStream
	
    if showprogress and not withmodels then progressEnd()
	
	-- check if we also need to import the .vis file
	local visfile = getFilenamePath pFile + getFilenameFile pFile + ".vis"
    if withvis and kx_existFile visfile then
	(
		-- Open up the file
		local mdlStream = openFile visfile mode:"r"

		local lineNum = 0
		local quitEarly = false

		try (
			-- Go through the whole vis file
			if showprogress then progressStart "Importing VIS"
			local fsize = kx_filesize mdlStream

			while not (eof mdlStream) and not quitEarly do
			(
				if showprogress then progressUpdate ((((filepos mdlStream) as float)/fsize) * 100)
				local tok = kx_Tokenizer()
				local line = getNextNonBlankLine mdlStream
				tok.SetString (line)
				lineNum += 1

				-- Read the data ID tag
				local idToken = tok.ReadToken()

				-- if idToken is a string token then convert to lowercase
				if (isKindOf idToken string) then idToken = kx_lowercase idToken

				if (idToken[1] == "#") then idToken = "#"
				
				if idToken != "#" then
				(
					local sRoom = idToken
					local room = (for obj in $helpers where (kx_lowercase obj.name) == (kx_lowercase sRoom) and iskindof obj odysseybase and obj.layouttype == 2 collect obj)[1]
					
					--messagebox sRoom
					-- clear existing list, no turning back after this
					if room != undefined then room.visrooms = #()
					
                    -- Get count
                    local nCount = undefined;
                    if (tok.PeekToken() != undefined) then nCount = tok.ReadInteger()

                    -- Go to first row
                    if nCount > 0 then tok.SetString (getNextNonBlankLine mdlStream)

                    while (tok.PeekToken() != "endlist" and nCount != 0) do
                    (
                        lineNum += 1

                        ---- DO STUFF
						local sVisible = tok.ReadToken()
						--messagebox ("Visible: "+sVisible)
						local visible = (for obj in $helpers where (kx_lowercase obj.name) == (kx_lowercase sVisible) and iskindof obj odysseybase and obj.layouttype == 2 collect obj)[1]
						if room != undefined and visible != undefined then append room.visrooms (kx_lowercase sVisible)
                        -- DONE WITH STUFF

                        -- read in next line from import data stream
                        if (nCount != undefined) then
                        (
                            nCount -= 1
                            if nCount > 0 then tok.SetString (getNextNonBlankLine mdlStream)
                        )
                        else tok.SetString (getNextNonBlankLine mdlStream)
                    )
				)
			) -- END while()
		) -- END try()
		catch
		(
			messageBox ("Odyssey Layout Importer Script Error reading VIS file at line:" + lineNum as string)
			close mdlStream
			throw
		)
		close mdlStream
		
		if showprogress then progressEnd()
	)
	
	clearSelection()
	max tool zoomextents all
	max views redraw
)


-- listener command line command
-- Usage: massload <file pattern> <target> {<anims:true>}
fn massload pattern:undefined target:undefined anims:true =
(
    if (pattern == undefined) or (target==undefined) then
    (
        format "Usage:  massload pattern:<filepath\pattern> target:<target dir> {<anims:true|false>}\r\n"
        return 0
    )

    if not (kx_existDir target) then
    (
        messageBox "Target directory does not exist"
        return 0
    )

    resetMaxFile #noPrompt

    -- get all the files to load from the pattern provided
    local filesToLoad = getFiles pattern
    if target[target.count] != "\\" then target += "\\"
    for file in filesToLoad do
    (
		local StrictFilename = file
		while matchPattern StrictFilename pattern:"*.*" do StrictFilename = getFilenameFile  StrictFilename
        local pwk_file = (getFilenamePath file)+StrictFilename+".pwk.ascii"
		if (not kx_existFile pwk_file) then pwk_file = (getFilenamePath file)+StrictFilename+".pwk"
        local dwk_file = (getFilenamePath file)+StrictFilename+".dwk.ascii"
		if (not kx_existFile dwk_file) then dwk_file = (getFilenamePath file)+StrictFilename+".dwk"

        -- import the file
        ImportOdysseyModel file anims false

         -- import pwk file if available
        if ( kx_existFile pwk_file ) then
            ImportOdysseyWalkmesh pwk_file "pwk"

        -- import dwk file if available
        if ( kx_existFile dwk_file ) then
            ImportOdysseyWalkmesh dwk_file "dwk"

		local modelBase
        for h in $helpers do
        (
           if (iskindof h odysseybase) then
           (
               modelBase = h
               exit
           )
        )
        -- save the file using the files model name
        saveMaxFile (target + modelBase.name)
        -- reset scene
        resetMaxFile #noPrompt
    )
)
