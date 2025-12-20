/*-----------------------------------------------------------------------------\

    KOTORmax General Functions
    by Joco (jameswalker@clear.net.nz)
	expanded and modified by bead-v
	
	KOTORmax
	by bead-v
	
	Derived from:
    NWmax
    by Joco (jameswalker@clear.net.nz)

    Credits:
    Wayland Reid :-
        Extends Wayland's (wreid@spectrumanalytic.com) Import/Export script
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
    4. In the case of code used from the Bioware scripts their intellectual
       property rights still hold.
    5. No code has been knowingly included from other sources where that code
       is sold for commercial purposes.

\-----------------------------------------------------------------------------*/

--------------------------------------------------------------------------------
---
--- Structures
---
/*
	STRUCTURE LIST
	kx_Tokenizer - the file reader structure
	kx_MultiArray - a 2d array with handlers?
	kx_SkinWeights - stores skin object and weight data
*/
--------------------------------------------------------------------------------
format "Define structs and utility fns\r\n"
struct kx_Tokenizer
(
    --
    -- used to parse out the imported mdl file
    --
    tokens = #(),

    fn SetString str =
    (
        str = trimright(trimleft(str))
        tokens = filterString str "\t ,"
    ),

    fn ReadToken =
    (
        if tokens.count > 0 then
        (
            local tok = tokens[1]
            deleteItem tokens 1
            if DEBUG then format "ReadToken: %\r\n" tok
            tok
        )
        else
        (
            undefined
        )
    ),

    fn PeekToken =
    (
        if tokens.count > 0 then tokens[1] else undefined
    ),

    fn ReadPoint3 =
    (
        local x = ReadToken() as float;
        local y = ReadToken() as float;
        local z = ReadToken() as float;
        if DEBUG then format "ReadPoint3: %\r\n" (point3 x y z)
        (point3 x y z)
    ),

    fn ReadFloat =
    (
        (ReadToken() as float)
    ),

    fn ReadInteger =
    (
        (ReadToken() as integer)
    ),

    fn ReadString =
    (
        (ReadToken())
    ),

    fn ReadColor =
    (
        local r = 255 * (ReadToken() as float);
        local g = 255 * (ReadToken() as float);
        local b = 255 * (ReadToken() as float);
        (color r g b)
    ),

    fn ReadBoolean =
    (
        local b = ReadToken()
        if (kx_lowercase b) == "true" then
            (true)
        else
            (false)
    ),

    fn ReadRemainingTokens =
    (
        local str = ""
        while (PeekToken() != undefined) do str += ReadToken() + " "
        substring str 1 (str.count-1)
    )
)

struct kx_MultiArray
(
    -- base array store
    ArrayData = #(),
    width = 1,

    fn append row =
    (
        if row.count != width then
            return false
        /*
        for e in row do (
            print e
            append ArrayData e
        )
        */

        join ArrayData row

        true
    ),

    fn delete row =
    (
        local realIndex, i
        realIndex = row * width
        if ArrayData.count < (realIndex+width-1) then
            return false

        for i = realIndex to (realIndex+width-1) do
        (
            deleteItem ArrayData i
        )
        true
    ),

    fn get row col =
    (
        local realIndex, i
        realIndex = ((row-1) * width) + col
        if ArrayData.count < realIndex then
            return false

        ArrayData[realIndex]
    ),

    fn size =
    (
        ArrayData.count / width
    )
)

struct kx_SkinWeights
(
    -- used to store the skin object and weights data
    Object,
    WeightsData = #()
)

--------------------------------------------------------------------------------
---
--- Misc functions
---
/*
	FUNCTION LIST
	dump node = dumps all props with the syntax "<property_name> = <value>\n"
	kx_push stk e = appends e to array stk
	kx_pop stk = deletes last element of array stk
	kx_hierarchy node level:0 = prints the node hierarchy by indentation
	kx_startStopWatch txt:"" = prints "#Timing Start <txt>\r\n"
	kx_stopStopWatch txt:"" = prints "#Timing Stop: <time> secs <txt>\r\n"
	kx_FlushBuffer force:false reset:false = a gmax-only function, because:
	                   "[In gmax, s]trings are limited to 15000 characters."
					   So if our buffer, g_strBuffer, is approaching two thirds
					   of the maximum string capacity, this function will print
					   it to the listener to make room for more data. While the
					   number of selected characters is limited for the Listener,
					   there seems to be no other gmax-specific limit on it, so
					   it should be possible to print data of any length to it.
					   If force==true, then buffer will flush even if the count
					   is not yet at two thirds. It will also print a new line
					   if g_delim is "~".
					   If reset==true, then instead of printing the buffer to
					   the Listener, we reset it to "".
	kx_edgeVerts theObj theEdge = gets the vert indices of theEdge as a point2 value
	kx_strclassof obj = gets the object's class as a string
	kx_btoi b = converts boolean to integer
	kx_itob i = converts integer to boolean
	kx_read_tga_bit f_file = read the depth byte in the .tga that is f_file.
							"Depth of 32 means an alpha channel."
	kx_progressPlus = updates the progressBar based on com_load and com_load_total
	kx_MakeUnqiueNodeName strName = creates unique node name by appending an integer
	kx_round x = returns rounded (to?) integer
	kx_roundp3 pIn = returns rounded point3
	kx_lowercase instring = returns the string in lowercase
	kx_existFile fname = returns true if file exists, else false
	kx_existDir dname = returns true if directory exists, else false
	kx_filesize fh = gets the file size
	kx_RotatePivotOnly obj rotation =
	kx_copy_modifiers dst src ignore:#() only:#() =
	kx_reset_transforms obj = ?
	kx_UpdateMaterialEditor MatLib = ???
	fn kx_createWalkMaterial = creates the walkmesh surface multimaterial
	kx_setinivalue datalist = Sets .ini settings. Data list is an array of arrays.
						Each sub array contains the section, attrib, value, file.
						Uses snoop in gmax, so it's safe to use.
	kx_reduct obj start end pos:false rot:false sca:false \
            thresholdRot:0 thresholdPos:0 safeframes:#() = anim key reduction
	kx_tiff_from_tex bmp = creates alpha map from tga file, a .tif file.
										Not sure whether this works in gmax.
	kx_mat_from_tga tgaFile matName index:0 = creates a material for tga texture
															
	-- ADDED BY bead-v:
	kx_iskindof obj string = a custom version of iskindof, that will work on those modifier scripts that are being difficult
							the reason the original didn't work was because of the order of the imported scripts, which left
							some things undefined.
	fn kx_hideSubtree node = hides the entire subtree starting with node
	fn kx_unhideSubtree node = unhides the entire subtree starting with node
*/
--------------------------------------------------------------------------------

----------------------------------------
--- Dump all properties on an object ---
----------------------------------------
fn dump node =
(
    -- A generic debug routine for dumping all a nodes properties
    for p in (getPropNames node) do
        format "% = %\n" p (getProperty node p)
)

-----------------------------------------
--- generic stack routines            ---
-----------------------------------------
fn kx_push stk e =
(
   append stk e
)

fn kx_pop stk =
(
   local e
   e = stk[stk.count]
   deleteItem stk stk.count
   e
)



----------------------------------------
--- Dump hierarchy starting at node  ---
----------------------------------------
fn kx_hierarchy node level:0 =
(
    if node != undefined then
    (
        local indent = ""
        for i = 1 to level do
        (
            indent += "  "
        )
        format "%%\r\n" indent node.name
        for c in node.children do
        (
            kx_hierarchy c level:(level+1)
        )
    ) else (
        format "kx_hierarchy: object not found\r\n"
    )
)

------------------------------
--- Timer analysis code    ---
------------------------------
global g_stopwatch = #()    -- fake stack of floats
fn kx_startStopWatch txt:"" =
(
    append g_stopwatch (timeStamp())
    format "#Timing Start %\r\n" txt
)

fn kx_stopStopWatch txt:"" =
(
    local l_stopwatch = timeStamp() - g_stopwatch[g_stopwatch.count]
    -- pop value off the stack
    deleteItem g_stopwatch (g_stopwatch.count)
    format "#Timing Stop: % secs %\r\n" (l_stopwatch/1000.0) txt
)


------------------------------
--- export buffer flusher  ---
------------------------------
fn kx_FlushBuffer force:false reset:false =
(
   if not g_ismax then
   (
    -- Test that the string stream in g_strBuffer is greater than out max size.
    -- If it is then flush the string stream to the Listener
    if ( ((filepos g_strBuffer) > 9800) or force ) and (not reset) then
    (
        -- flush the buffer to listener
        format "%" (g_strBuffer as string)
        -- reset the buffer
        g_strBuffer = undefined
        g_strBuffer = stringStream ""
        if force then
        (
            if g_delim == "~" then (
				format "\r\n"
			)
        )
    )
	else if reset then
    (
        g_strBuffer = undefined
        g_strBuffer = stringStream ""
    )
   )
)

-------------------------------
--- edges verts as a point2 ---
-------------------------------
fn kx_edgeVerts theObj theEdge =
(
    if not (classof theObj == Editable_mesh or
    classof theObj == triMesh) do return undefined
    if theEdge < 1 or theEdge > (theObj.numfaces*3) do return undefined
    local theFace = ((theEdge-1)/3)+1
    local theVerts = getFace theObj theFace
    case ((mod (theEdge-1) 3) as integer) of
    (
        0: point2 theVerts.x theVerts.y
        1: point2 theVerts.y theVerts.z
        2: point2 theVerts.z theVerts.x
    )
)

------------------------------
--- ClassOf node as String ---
------------------------------
fn kx_strclassof obj =
(
    --
    -- Get the objects class as a string
    --
    return ( (classof obj) as string )
)

--------------------------
--- Boolean to Integer ---
--------------------------
fn kx_btoi b =
(
    local r
    if b then
        r = 1
    else
        r = 0

    r
)

--------------------------
--- Integer to Boolean ---
--------------------------
fn kx_itob i =
(
    local r
    if i == 0 then
        r = false
    else
        r = true

    r
)


------------------------------
--- Read TGA bit for depth ---
------------------------------
fn kx_read_tga_bit f_file =
(
    -- depth of 32 means an alpha channel
    local fh = undefined
    local depth = 0
    fh = fopen f_file "rb"
    if fh != undefined then
    (
        fseek fh 16 #seek_set
        depth = readByte fh
        fclose fh
    )
    else print "!! ERROR !!"
    depth
)


-----------------------------------
--- Do plug in loading progress ---
-----------------------------------
fn kx_progressPlus =
(
    com_load = com_load + 1
    progressUpdate ((com_load*100)/com_load_total)
)

------------------------
--- Unique node name ---
------------------------
fn kx_MakeUnqiueNodeName strName=
(
    local i = 0
    while (true) do
    (
        i += 1
        local obj_name = strName + (i as string)
        local node = getNodeByName(obj_name)
        if node == undefined then
            return obj_name
    )
)

---------------------
--- round integer ---
---------------------
fn kx_round x =
(
    local correction = 0 as integer
    local tenth = ((mod x 1) * 10) as integer
    if (tenth >= 5) then correction = 1 as integer
    return (((x as integer) + correction) as integer)
)

--------------------
--- round point3 ---
--------------------
fn kx_roundp3 pIn =
(
    p = copy pIn
    local rn
    -- round 'x'
    rn = p.x - (floor p.x)
    if (rn > 0.5) then
       p.x = ceil p.x
    else
      p.x = floor p.x
    -- round 'y'
    rn = p.y - (floor p.y)
    if (rn > 0.5) then
        p.y = ceil p.y
    else
        p.y = floor p.y
    -- round 'z'
    rn = p.z - (floor p.z)
    if (rn > 0.5) then
        p.z = ceil p.z
    else
        p.z = floor p.z
    -- return rounded point
    return p
)

------------------
--- Lower case ---
------------------
fn kx_lowercase instring =
(
    -- Joco 8-Aug-03: needed to cope with some keywords in mdl
    -- not being consistently cased when compared to peoples docs.
    -- May be made redundant very soon!
    local upper, lower, outstring

    upper="ABCDEFGHIJKLMNOPQRSTUVWXYZ"
    lower="abcdefghijklmnopqrstuvwxyz"

    -- if 'undefined' then return null string
    if instring == undefined then return ""

    outstring=copy instring -- operate on copy of instring
    for i=1 to outstring.count do
    (
        j=findString upper outstring[i]
        if j != undefined do outstring[i]=lower[j]
    )
    return outstring
)


----------------------------
--- Check if file exists ---
----------------------------
fn kx_existFile fname = (getfiles fname).count != 0

---------------------------------
--- Check if directory exists ---
---------------------------------
fn kx_existDir dname =
(
    local dirstr
    if (dname[dname.count] == "\\") or (dname[dname.count] == "/") then (
        dirstr = substring dname 1 (dname.count - 1)
    ) else (
        dirstr = dname
    )
    (getDirectories dirstr).count != 0
)

---------------------------------
--- Get the file size         ---
---------------------------------
fn kx_filesize fh =
(
    local fsize = 0
    if fh == undefined then return false
    try (
        inipos = filepos fh
        while not eof(fh) do
        (
            fsize += 1
            seek fh fsize
        )
        seek fh inipos
    ) catch ()
    return fsize
)

-------------------------------------
--- Rotating only the pivot point ---
-------------------------------------
fn kx_RotatePivotOnly obj rotation =
(
    local rotValInv=inverse (rotation as quat)
    animate off in coordsys local obj.rotation*=RotValInv
    obj.objectoffsetrot*=RotValInv
    obj.objectoffsetpos*=RotValInv
)

------------------------------------------------------
--- Copy the modifiers from src to dst.            ---
--- Code by roboius (http://rockbottom.vtex.net/)  ---
------------------------------------------------------
fn kx_copy_modifiers dst src ignore:#() only:#() =
(
    local ii

    for ii in src.modifiers.count to 1 by -1 do
    (
        if (only.count == 0 or finditem only src.modifiers[ii].name) then
        (
            if (finditem ignore src.modifiers[ii].name) == 0 then
                addmodifier dst (copy src.modifiers[ii])
        )
    )
)

-------------------------------------------------
--- Similar to the "Reset Transform" utility. ---
--- Thanks to Roboius                         ---
-------------------------------------------------
fn kx_reset_transforms obj =
(
    local ntm, ptm, rtm = matrix3 1, tm, piv

    if (not iskindof obj Editable_Mesh) then ( return 0 )

    -- Get the parent and node TMs.
    ntm = obj.transform
    if (obj.parent != undefined) then (
        ptm = obj.parent.transform
    ) else (
        ptm = matrix3 1
    )

    -- Compute the relative TM.
    ntm = ntm * inverse ptm

    -- Need to flip the normals if the determinant is negative.
    if (ntm.determinantsign < 0) then
    (
        meshop.flipnormals obj obj.faces
    )

    -- The reset TM only inherits position.
    rtm.translation = ntm.translation

    -- Set the node TM to the reset TM.
    tm = rtm * ptm
    obj.transform = tm

    -- Compute the pivot TM.
    local piv = obj.objecttransform * inverse obj.transform

    -- Reset the offset to 0.
    obj.objectoffsetpos = [0,0,0]
    obj.objectoffsetrot = (quat 0 0 0 1)
    obj.objectoffsetscale = [1,1,1]

    -- Take the position out of the original node transform matrix since
    -- we don't reset position.
    ntm.translation = [0,0,0]

    -- Apply the offset to the TM.
    ntm = piv * ntm

    -- Apply an XForm modifier to the node.
    local xform_mod = xform()
    addmodifier obj xform_mod

    xform_mod.gizmo.transform = ntm
)



-------------------------------------
--- UpdateMaterialEditor function ---
-------------------------------------
fn kx_UpdateMaterialEditor MatLib=
(
    -- from Waylands original script
    if MatLib.count > 0 then
    (
      local curLibIndex = 1
      for matIndex in 1 to 24 do
      (
        curMat = meditMaterials[matIndex]
        if findString curMat.name " - Default" != 0 then
        (
          -- Put the model material into the Material Editor
          setMeditMaterial matIndex MatLib[curLibIndex]
          -- Set the sample object type for this material to a square
          setMTLMeditObjType MatLib[curLibIndex] 3
          curLibIndex = curLibIndex + 1
          if curLibIndex > MatLib.count then
          (
            exit
          ) -- end curLibIndex > MatLib.count check
        ) -- end current material slot in editor is a standard material check
      ) -- end materials in editor loop
    ) -- end MatLib.count > 0 check
) -- end UpdateMaterialEditor function




-----------------------------------
--- Create Surface Material ---
-----------------------------------
global kx_SurfaceMat = #()
fn kx_createWalkMaterial =
(
    if DEBUG then format "%kx_createWalkMaterial: start\r\n" strIndent1
    -- from Wayland's script
    local SurfaceMatName = #()
    kx_SurfaceMat = #()
    local matName = "OdysseyWalkMaterial";

    local newMaterial = sceneMaterials[matName]
    if (newMaterial == undefined) then
    (
        -- only create the material of it's not already done
        --

        -- get the material count
        local num_mats = (getINISetting (g_kotormaxPath + "wokmat.ini") "init" "count") as integer
        local mat_name

        for m = 1 to num_mats do
        (
            mat_name = getINISetting (g_kotormaxPath + "wokmat.ini") ("m"+(m as string)) "name"
            append SurfaceMatName mat_name
            kx_exec_str = "append kx_SurfaceMat "+(getINISetting (g_kotormaxPath + "wokmat.ini") ("m"+(m as string)) mat_name)
            execute kx_exec_str
        )

       newMaterial = multimaterial name:matName numsubs:SurfaceMatName.count

       for i in 1 to SurfaceMatName.count do
       (
         newMaterial.names[i] = SurfaceMatName[i]
         newMaterial.materialList[i].name = SurfaceMatName[i]
         newMaterial.materialList[i].diffuse = kx_SurfaceMat[i]
       )
    )
    kx_SurfaceMat = undefined
    if DEBUG then format "%kx_createWalkMaterial: end\r\n" strIndent1
    return newMaterial
)


-----------------------------------
---                             ---
-----------------------------------
fn kx_setinivalue datalist noClear:false noEnd:false =
(
    /*
    Data list is an array of arrays.
    Each sub array contains the section, atrrib, value, file
    */
    local sublist

    if g_ismax then
    (
        for sublist in datalist do
        (
            setINISetting sublist[4] sublist[1] sublist[2] sublist[3]
        )
    ) else
    (
        -- gmax processing. Verbose data dumping
        if not noClear then clearListener()
        format "<snoopstart file=%>~\n\n" (g_kotormaxPath + "scratch/setini.txt")
        for sublist in datalist do
        (
            format "<snoopset %,%,%,%,\>~\n" sublist[1] sublist[2] sublist[3] sublist[4]
        )
        format "</snoopstart>~\n"
        if not noEnd then format "</snoopend>~\n"
    )
)


-----------------------------------
--- Anim Key reduction          ---
-----------------------------------
fn kx_reduct obj start end pos:false rot:false sca:false \
            thresholdRot:0 thresholdPos:0 safeframes:#() =
(
   -- get all the controllers
    local ctrls = #()
    if pos then append ctrls obj.position.controller
    if rot then append ctrls obj.rotation.controller
    if sca then append ctrls obj.scale.controller

    -- start reduction on each controller type
    local basekey, comparekey1, comparekey2, delKey
    local delkeys
    for ctrl in ctrls do
    (
        if (ctrl != undefined) AND (ctrl.keys != undefined) then
        (
            basekey = 1
            comparekey1 = basekey + 1
            comparekey2 = basekey + 2
            while (comparekey2 <= ctrl.keys.count) and (ctrl.keys[comparekey2].time <= end) do
            (
                delKey = false
                -- get starting values
                baseval = ctrl.keys[basekey].value
                compval1 = ctrl.keys[comparekey1].value
                compval2 = ctrl.keys[comparekey2].value

                -- compare type depends on the control type
                if (iskindof baseval Point3) then
                (
                    -- this is a position control type
                    diff1 = abs(distance baseval compval1)
                    diff2 = abs(distance baseval compval2)

                    /*
                    Convert to using a gradient based test.
                    Do this test on x,y,z and only delete key if all three are within
                    the specified threshold.
                    arctan(opp/adj) == angle
                    adj == time
                    opp == position axis difference between keys

                    Appraoch is for each axis calc the angles of the vectors from
                    that axis and compare them to the threshold. If all the vector
                    angles for the key are within tolerances then delete
                    that position key
                    */

                    delKeyX = false
                    delKeyY = false
                    delKeyZ = false

                    -- x axis
                    if DEBUG then format "X axis\r\n"
                    opposite = compval1.x - baseval.x
                    adjacent = ctrl.keys[comparekey1].time - ctrl.keys[basekey].time
                    angle1 = atan(opposite/adjacent)
                    opposite = compval2.x - compval1.x
                    adjacent = ctrl.keys[comparekey2].time - ctrl.keys[comparekey1].time
                    angle2 = atan(opposite/adjacent)
                    if abs(angle2 - angle1) <= thresholdPos then delKeyX = true
                    if DEBUG then format "Key: %  Type: %\r\n" comparekey1 (classof baseval)
                    if DEBUG then format "Angle1: %   Angle2: %   Abs Diff: %\r\n" angle1 angle2 (abs(angle2-angle1))
                    -- y axis
                    if DEBUG then format "X axis\r\n"
                    opposite = compval1.y - baseval.y
                    adjacent = ctrl.keys[comparekey1].time - ctrl.keys[basekey].time
                    angle1 = atan(opposite/adjacent)
                    opposite = compval2.y - compval1.y
                    adjacent = ctrl.keys[comparekey2].time - ctrl.keys[comparekey1].time
                    angle2 = atan(opposite/adjacent)
                    if abs(angle2 - angle1) <= thresholdPos then delKeyY = true
                    if DEBUG then format "Key: %  Type: %\r\n" comparekey1 (classof baseval)
                    if DEBUG then format "Angle1: %   Angle2: %   Abs Diff: %\r\n" angle1 angle2 (abs(angle2-angle1))
                    -- z axis
                    if DEBUG then format "X axis\r\n"
                    opposite = compval1.z - baseval.z
                    adjacent = ctrl.keys[comparekey1].time - ctrl.keys[basekey].time
                    angle1 = atan(opposite/adjacent)
                    opposite = compval2.z - compval1.z
                    adjacent = ctrl.keys[comparekey2].time - ctrl.keys[comparekey1].time
                    angle2 = atan(opposite/adjacent)
                    if abs(angle2 - angle1) <= thresholdPos then delKeyZ = true
                    if DEBUG then format "Key: %  Type: %\r\n" comparekey1 (classof baseval)
                    if DEBUG then format "Angle1: %   Angle2: %   Abs Diff: %\r\n" angle1 angle2 (abs(angle2-angle1))

                    if (delKeyX and delKeyY and delKeyZ) then delKey = true
                )

                if (iskindof baseval Quat) then
                (
                    diff1 = abs(baseval.angle - compval1.angle)
                    diff2 = abs(baseval.angle - compval2.angle)
                    tol = abs(baseval.angle * thresholdRot)
                    strAxis0 = baseval.axis as string
                    strAxis1 = compval1.axis as string
                    strAxis2 = compval2.axis as string
                    if DEBUG then format "KeyTime: %  Type: %\r\n" (ctrl.keys[comparekey1].time) (classof baseval)
                    if DEBUG then format "Diff1:%  Diff2:%  Tol:%  Axis:% % %\r\n" diff1 diff2 tol baseval.axis compval1.axis compval2.axis
                    if DEBUG then format "Axis test: % %\r\n" (strAxis0 == strAxis1) (strAxis0 == strAxis2)
                    if (strAxis0 == strAxis1) and (strAxis0 == strAxis2) then
                    (
                    if DEBUG then format "Tol test: % %\r\n" (diff1 <= tol) (diff2 <= tol)
                    if (diff1 <= tol) and (diff2 <= tol) then
                    (
                        delKey = true
                    )
                    )
                )

                -- now check that the current key is in the target frame range
                if (ctrl.keys[comparekey1].time < start) and (ctrl.keys[comparekey1].time > end) then
                (
                    delKey = false
                )
                -- check that the key frame is not a protected frame.
                -- Frames are deemed protected if they are the start and end of a
                -- named animation range
                if (findItem safeframes (ctrl.keys[comparekey1].time)) != 0 then
                (
                    delKey = false
                )

                if delKey then
                (
                    -- delete comparekey1
                    if DEBUG then format "Del key: %\r\n" comparekey1
                    deleteKey ctrl comparekey1
                ) else
                (
                    -- move the baseline forward
                    basekey = comparekey1
                    comparekey1 += 1
                    comparekey2 += 1
                )
            )
        )
    )
)

---------------------------------------
---  Create alpha map from tga file ---
---------------------------------------
fn kx_tiff_from_tex bmp =
(
    local h = bmp.height
    local w = bmp.width
    local r     -- row counter
    local c     -- column counter
    local rowdata
    local new_bmp
    local gs_value
    local newbmp_filename

    -- create new grayscale image
    newbmp_filename = (getFilenamePath bmp.filename) + (getFilenameFile bmp.filename) + ".tif"
    new_bmp = bitmap w h color:black filename:newbmp_filename
    -- copy the bmp
    copy bmp new_bmp
	save new_bmp
	new_bmp
)


--------------------------------------
---  Create material from tga file ---
--------------------------------------
fn kx_mat_from_tga tgaFile matName index:0 =
(
    local objMaterial, tga, texMap, tgaDepth

    objMaterial = standardmaterial name:matName
    objMaterial.shaderType = 1 -- Blinn
    objMaterial.ambient = color 220 220 220
    objMaterial.diffuse = color 220 220 220
    objMaterial.specular = color 220 220 220
    objMaterial.glossiness = 10
    objMaterial.selfillumcolor = color 0 0 0
    objMaterial.opacity = 100
    objMaterial.soften = 0.1

    if (kx_existFile tgaFile) then
    (
        tga = openBitMap(tgaFile)
        tgaDepth = kx_read_tga_bit tgaFile
    )
    else
    (
        -- fundamental error, texture no longer there
        messagebox "Error in finding a tga file,\ncheck all tgas are still in place"
        return false
    )

    texMap = BitmapTexture bitmap:tga name:matName
    objMaterial.mapAmounts[2] = 100
    objMaterial.maps[2] = texMap
    objMaterial.mapEnables[2] = true;
    showTextureMap objMaterial texMap true
    if index > 0 then
    (
        setMeditMaterial index objMaterial
    )
    objMaterial
)

fn kx_iskindof obj string =
(
	return ((kx_lowercase (classof obj as string)) == (kx_lowercase string))
)

fn kx_hideSubtree TreeRoot =
(
	if TreeRoot != undefined then
	(
		for TreeChild in TreeRoot.children do (kx_hideSubtree TreeChild)
		hide TreeRoot
	)
)

fn kx_unhideSubtree TreeRoot =
(
	if TreeRoot != undefined then
	(
		for TreeChild in TreeRoot.children do (kx_unhideSubtree TreeChild)
		unhide TreeRoot
	)
)

fn kx_CreateExportName sModel =
(
	local sName = sModel
	if kx_itob g_expLower then sName = kx_lowercase sName
	return (sName + "-kotormax")
)

fn sortBones p1 p2 = 
(
	if p1[2] == p2[2] then return 0
	else if p1[2] > p2[2] then return -1
	else return 1
)

function kx_LimitAllWeights =
(
    local save_selection = selection
	
	local ourSkins = for obj in $objects where obj.modifiers[#skin] != undefined collect obj
	for node in ourSkins do
	(
		max modify mode
		select node
		local skinMod = node.modifiers[#skin]
		local n = skinops.getnumbervertices skinMod
		local i
		for i = 1 to n do
		(
			local w_num = skinops.getvertexweightcount skinMod i
			if w_num <= 4 then continue
			
			local vertWeights = #()
			local j
			for j = 1 to w_num do -- for each of the weights on the current vertex...
			(
				local bone_id = skinops.getvertexweightboneid skinMod i j
				local weight_i_j = skinops.getvertexweight skinMod i j
				
				if weight_i_j > 0.0 then append vertWeights #(bone_id, weight_i_j)
			)
			
			qsort vertWeights sortBones
			local nTotal = 0
			for m = 1 to 4 where m <= vertWeights.count do nTotal += vertWeights[m][2]
			for m = 1 to 4 where m <= vertWeights.count do vertWeights[m][2] += (1 - nTotal) * (vertWeights[m][2] / nTotal)
			skinOps.ReplaceVertexWeights skinMod i \
				(for m = 1 to 4 where m <= vertWeights.count collect vertWeights[m][1]) \
				(for m = 1 to 4 where m <= vertWeights.count collect vertWeights[m][2]) 
		)
	)
	
    if save_selection != undefined do select save_selection
	
	return 0
)

-- Enables channel support if necessary
fn kx_enableMapChannel mesh nChannel =
(
	local nMaps = meshop.getNumMaps mesh
	-- If the map is already "active", only check for support and enable it if disabled
	if nChannel < nMaps then
	(
		if not meshop.getMapSupport mesh nChannel then 
		(
			meshop.setMapSupport mesh nChannel true
			
			-- Enabling support for the mesh will put garbage into it, so we need to delete that
			meshop.setNumMapVerts mesh nChannel 0
		)
		return 1
	)
	
	-- If we get here then we need to increase the number of maps. This automatically enables support for the newly activated maps.
	meshop.setNumMaps mesh (nChannel + 1)
	
	-- For every new map in between that we don't need, we need to disable support for it
	for n = nMaps to (nChannel - 1) do
	(
		-- This is an unnecessary map channel, disable support
		meshop.setMapSupport mesh n false
	)
	
	-- Enabling support for the mesh will put garbage into it, so we need to delete that
	meshop.setNumMapVerts mesh nChannel 0
	
	return 2
)

-- This is provided as a pair to the above function. Simply disables support, but won't change the number of "active" map channels
fn kx_disableMapChannel mesh nChannel =
(
	if meshop.getNumMaps mesh > nChannel then meshop.setMapSupport mesh nChannel false
)

-- Checking function
fn kx_isMapChannelEnabled mesh nChannel =
(
	if meshop.getMapSupport mesh nChannel then return true
	else return false
)

/*
attributes "Bump Material"
(
)
*/