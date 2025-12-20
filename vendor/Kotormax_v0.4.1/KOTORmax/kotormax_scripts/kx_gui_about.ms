/*-----------------------------------------------------------------------------\

    KOTORmax About

	KOTORmax
	by bead-v

	Original script for:
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

rollout CreditDialogNWmax "NWmax Credits" width:326 height:291
(
	label lbl5 "
NWmax is based on, adapted from and an extension of code originally developed by:
- Bioware
- Joco
- Wayland

Special thanks and acknowledgements to
* DLA (ideas, mentoring and lots of bug reports)
* roboius (assistance, code and \"The TileSlicer\")
* Velmar (animation lists, code and \"Velmar Tools\")
* Torlack (\"nwnmdlcomp\" mdl compiler/decompiler)
* simple tim (fixes for the original Wayland script)
* scooterpb (MDL Suite UI)
* Zaddix (original WOK file importer)
* Revinor (decoding the MDL format)
" width:284 height:235 pos:[21,10]
	button btn_close "Close" width:104 height:32 --pos:[111,250]
	on btn_close pressed do destroyDialog CreditDialogNWmax
)

rollout CreditDialogKOTORmax "KOTORmax Credits" width:376 height:421
(
	label lbl5 "
KOTORmax is directly extended from Joco's NWmax.

\"It's great to know that people are still finding value from the tool and the time that myself and those who were members of the DLA mod group invested. 
 
I'm comfortable with any of the proposed release models. At the end of the day do what will give the best user experience to the users in the community you operate within.
 
All the best,
Joco\"
 
New code for KOTORmax written by bead-v.

Very special thanks to ndix UR, who was a source of knowledge, 
ideas and inspiration. KOTORmax wouldn't exist without him.

Also thanks to:
* Magnusll (originally edited NWmax scripts for lightmap export)
* People who helped with testing and/or sharing experience/opinions:
   - JCarter426
   - Quanon
   - DarthParametric
   - FairStrides
   - VarsityPuppet
" width:344 height:355 pos:[21,0]
	label lbl_space ""
	button btn_nwmax "NWmax" width:104 height:32 across:2 --pos:[111,250]
	button btn_close "Close" width:104 height:32 --pos:[111,250]
	on btn_nwmax pressed do(
		createDialog CreditDialogNWmax
		destroyDialog CreditDialogKOTORmax
	)
	on btn_close pressed do destroyDialog CreditDialogKOTORmax
)

rollout AboutRollout "KOTORmax" width:162 height:400
(
    local bmap_kx = g_kotormaxPath+"kotormax_scripts/kotormax_v1.bmp"
	button btn_kotormax "About and Credits" width:144 images:#(bmap_kx, undefined, 1, 1, 1, 1, 1) \
    	toolTip:"Click for About and Credits."
	label lbl_ver ""
	--label lbl_author "by bead-v"
	hyperlink hlink_deadlystream "www.deadlystream.com" address:"http://www.deadlystream.com" align:#center
	
	label lbl_space ""
	
	label lbl_derived "Extended from:"
    local bmap = g_kotormaxPath+"kotormax_scripts/nwmax_v1.bmp"
    local maskmap = g_kotormaxPath+"kotormax_scripts/nwmax_v1_mask.bmp"
	button btn_nwmax "" width:144 height:90 \
    	images:#(bmap, maskmap, 1, 1, 1, 1, 1) \
    	toolTip:"Click for About and Credits." --pos:[9,7] 
	--label lbl_hint "Click for About and Credits."
	
	--label lbl_ver ""

	--HyperLink hlink_nwmax "nwmax.dladventures.com" address:"http://nwmax.dladventures.com/" align:#center
	--HyperLink hlink_dla   "www.dladventures.com" address:"http://www.dladventures.com/" align:#center
	--HyperLink hlink_vote  "Vote NWmax at NWVault" address:"http://nwvault.ign.com/Files/other/data/1072761698656.shtml" align:#center
	--HyperLink hlink_docs  "NWmax Help" address:("file:///"+g_kotormaxPath + "docs/index.html") align:#center
	--HyperLink hlink_ccg   "CCG Wiki by DLA" address:"http://ccg.dladventures.com/" align:#center

	--button btn_code_reload "Reload Code"
	--on btn_code_reload pressed do kx_coderefresh()

	on btn_kotormax pressed do ( createDialog CreditDialogKOTORmax )
	on btn_nwmax pressed do ( createDialog CreditDialogNWmax )
    on AboutRollout open do ( lbl_ver.text = "v" + gblVersion )
)

