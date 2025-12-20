/*-----------------------------------------------------------------------------\

    Adjust normals functionality
	
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
struct kx_patch(
	obj = undefined,
	nVertex = -1,
	FaceInds = #(),
	nSmoothingGroups = 0
)

fn kx_anglePoint3 p3 q3 =
(
	return acos ((dot p3 q3) / (length p3 * length q3))
)

fn kx_findPoint3 aArray p3 =
(
	for i = 1 to aArray.count do
	(
		local pCompare = aArray[i]
		if not iskindof pCompare Point3 then continue
		if abs (pCompare.x - p3.x) < 0.01 and abs (pCompare.y - p3.y) < 0.01 and abs (pCompare.z - p3.z) < 0.01 then return i
	)
	return 0
)

fn CreatePatches patches =
(
	local doneVerts = #()
	for obj in $objects where iskindof obj editable_mesh do
	(
		for i = 1 to meshop.getNumVerts obj do --obj.baseobject.numverts do
		(
			local nIndex = kx_findPoint3 doneVerts (in coordsys world meshop.getVert obj i node:obj)
			local FaceIndices = #()
			local nSmoothing = 0
			
			for ind in (meshop.getFacesUsingVert obj i) do
			(
				append FaceIndices ind
				nSmoothing = bit.or nSmoothing (getFaceSmoothGroup obj ind)
			)
			
			if nIndex > 0 then append patches[nIndex] (kx_patch obj:obj nVertex:i FaceInds:FaceIndices nSmoothingGroups:nSmoothing)
			else
			(
				append patches #(kx_patch obj:obj nVertex:i FaceInds:FaceIndices nSmoothingGroups:nSmoothing)
				append doneVerts (in coordsys world meshop.getVert obj i node:obj)
			)
		)
	)
)

fn CalculateVertexNormals bArea:true bAngle:false bCrease:false nCrease:60 =
(
	local patches = #()
	CreatePatches patches
	
	for patch_group in patches do
	(
		for patch1 in patch_group do
		(
			local vNormal = [0,0,0]
			for patch2 in patch_group where patch1 == patch2 or (bit.and patch1.nSmoothingGroups patch2.nSmoothingGroups > 0) do
			(
				for f in patch2.FaceInds do
				(
					local vAdd = getFaceNormal patch2.obj f
					
					if bCrease and (kx_anglePoint3 vNormal vAdd) > nCrease and vNormal !=[0,0,0] then continue
					
					if bArea then vAdd *= meshop.getFaceArea patch2.obj f
					if bAngle then
					(
						local Edges = for ind in (meshop.getVertsUsingFace patch2.obj f) where ind != patch2.nVertex collect ((meshop.getVert patch2.obj ind) - (meshop.getVert patch2.obj patch2.nVertex))
						vAdd *= kx_anglePoint3 Edges[1] Edges[2]
					)
					
					vNormal += vAdd
				)
			)
			setNormal patch1.obj patch1.nVertex (normalize vNormal)
		)
	)
)