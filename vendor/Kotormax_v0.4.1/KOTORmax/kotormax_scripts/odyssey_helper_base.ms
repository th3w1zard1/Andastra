/*-----------------------------------------------------------------------------\

    OdysseyBase Helper Object
	
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
format "load: odyssey_helper_base.ms\r\n"
kx_progressPlus()

-- make the list all lower case to cope with differences in export scripts
global odysseyClassTypes = #("other","effect","tile","character","door","lightsaber","placeable","flyer") --,"walkmesh","bodypart","item","gui")

plugin helper odysseybase
name:"OdysseyBase"
classID:#(0xc52f72fa, 0xc48a3a16)
version:2
extends:Dummy
replaceUI:true
invisible:true
(
	-- parameters
	parameters main rollout:params
	(
		-- data
    	-- Animation data is stored in the 'animations' array as a
    	-- space delimited string of the form:
    	-- 'name' 'firstFrame' 'lastFrame' 'transition', 'active', 'animroot'
		animations      type:#stringtab tabsizevariable:true
		events          type:#stringtab  tabsizevariable:true
		visrooms        type:#stringtab tabsizevariable:true
		setsupermodel   type:#string  default:"NULL" ui:edit_supermodel
		classification  type:#string default:"other"
		classification_sel  type:#integer default:1 ui:droplist_classui
		classification_unk1 type:#integer default:0
		setanimationscale   type:#float default:1.0 ui:spn_animscale
		radius			type:#float default:7.0
		bmin    		type:#point3 default:[-5,-5,-1]
		bmax    		type:#point3 default:[5,5,10]
		ignorefog		type:#integer default:0 ui:chk_ignorefog
		headlink 		type:#integer default:0 ui:chk_headlink
		compressquats	type:#integer default:0 ui:chk_compressquats
		layouttype      type:#integer default:1 ui:droplist_areatype
		layoutnum       type:#integer default:-1
		extranames      type:#stringtab tabsizevariable:true
		
		export_path     type:#string default:g_exportPath ui:edit_exportpath
		sanity_check	type:#integer default:1 ui:chk_check
		uselyt      	type:#integer default:1 ui:chk_uselyt
		copy_tga		type:#integer default:0 ui:chk_copytga
		tga2dds			type:#integer default:0 ui:chk_convdds
		sanityok        type:#integer default:1
		pointer         type:#node

		on classification_sel set val do
		(
			-- do a quick sanity check on val
			if (val < 1) or (val > odysseyClassTypes.count) then
			(
				val = 1
				format "Error: classification_sel set call not in bounds\r\n"
			)

    		classification = odysseyClassTypes[val]
		)
	)


	rollout params "Odyssey Base Params"
	(
		label lbl_lyttype "Layout Type:" across:2 offset:[-2,3] align:#left
		dropdownlist droplist_areatype items:#("None", "Room", "Track", "Obstacle") width:75 offset:[-4,0]
		label lbl_lytnum "Layout Number:" across:2 align:#left offset:[-2,-3]
		label lytnum "" offset:[-4,-3]
		
		group "Supermodel"
		(
			edittext edit_supermodel "" height:20
		)
		
		group "Model Parameters"
		(
			label classtype "Classification:" across:2 offset:[-4,3]
			dropdownlist droplist_classui items:odysseyClassTypes width:75 offset:[-4,0]
			checkbox chk_ignorefog "Ignore Fog" 
			checkbox chk_headlink "Head Link"
		)

        group "Animation Parameters" (
            spinner spn_animscale "Animation scale:" type:#float scale:0.01 range:[0,10,1] fieldwidth:45 align:#left
			checkbox chk_compressquats "Compress Quaternions" 
            button btn_animeditor "Open Animation Editor"
        )

		-- export data button widgets
		group "Export"
		(
            editText edit_exportpath "Dir:"
            button btn_exportbrowse "Browse" align:#left across:2
            button btn_exportreset "Reset" align:#right
            button  btn_export_mdl	"Export Model+Anim " align:#center width:140
            button  btn_export_noanims "Export Geom only" align:#center width:140 toolTip:"Export ONLY the geometry, no animations"
            checkbox chk_uselyt "Export Layout Position" align:#left
            checkbox chk_check "Do Sanity Checks" align:#left
            checkbox chk_copytga "Copy TGAs" align:#left checked:false
            checkbox chk_convdds "Convert to DDS" align:#left checked:false enabled:false
            button btn_viewsanity "View Sanity Check Results" align:#center width:140
        )
		
		on chk_check changed state do
		(
			if state == false then messagebox ("KOTORmax has its own version of the sanity check. If it fails, then something is seriously wrong with the setup, which may result in deformed meshes or your game crashing. If you're turning this off you better know what you're doing!") title:"Warning!" beep:true
		)

		on droplist_areatype selected val do
		(
			if val == 2 then lytnum.text = this.layoutnum as string
			else lytnum.text = ""
		)
		
        on edit_exportpath entered val do
        (
            if not (kx_existDir val) then
                messagebox "Directory does not exist!"
			else
			(
				g_exportPath = val
				kx_setinivalue #(#("init", "lastexport", g_exportPath, (g_kotormaxPath + "kotormax.ini")))
			)
        )

		fn export_start doanim:true =
		(
    		local node = selection[1]
    		local bSanityCheck
    		local mdlextn
    		local model_name
    		local strCmd
	        -- Make sure listener is clear
	        clearListener()
    		if (kx_existDir export_path) then
    		(
        		if (export_path[export_path.count] != "\\") then export_path = export_path + "\\"

        		-- Tell GMAX Snoop what fully qualified file name to use and keep a global copy
        		g_exportPath = export_path

                mdlextn = "mdl"
				if kx_itob g_dotAscii then mdlextn += ".ascii"
				model_name = kx_CreateExportName node.name

				-- we need to make sure that the model_name has no tabs in it
				-- as it will break things. The rule will be tabs will be changed to '_'
				if (matchPattern node.name pattern:"*	") then
				(
	   				messageBox "Model name contains a tab. ERROR!! Processing Halted."
	   				return -1
				)

        		-- set the external control snoop bracket.
        		if not g_ismax then format "<snoopstart file=%%>%" g_kotormaxPath "scratch\\exportcontrol.txt" g_delim to:g_strBuffer
	    		if not g_ismax then format "<snoopstart file=%%.%>%" export_path model_name mdlextn g_delim to:g_strBuffer

				-- we can now do some sanity checks and record the results for review
	    		if not g_ismax then
	    		    format "<snoopstart file=%%_sanitycheck.txt>%" (g_kotormaxPath+"sanity/") model_name g_delim to:g_strBuffer
                else
					g_strBuffer = createFile (g_kotormaxPath + "sanity/" + model_name + "_sanitycheck.txt")

                -- DO SANITY CHECK IF APPLICABLE
				if chk_check.checked then
		    		bSanityCheck = SanityCheck node
	    		else
	    			bSanityCheck = 1

	    		if not g_ismax then
                (
		       		format "</snoopstart>%" g_delim to:g_strBuffer
	    		)
                else
                (
		       		flush g_strBuffer
		       		close g_strBuffer
	    		)

				if bSanityCheck == 0 then
				(
					-- Ask user if want to continue export anyway
	  				str = "Please fix the issues listed in the sanity check report. " + \
						"If you do not, your model may crash the game or its meshes may be deformed. " + \ 
						"If you want to have a look at the ascii anyway, push YES to continue export. Just don't compile it, unless your goal is to crash your game!"
	  				if (queryBox str title:("SanityCheck failed for " + model_name) beep:true) then
	  				    bSanityCheck = 1    -- over-ride and export
	      		)

	      		if bSanityCheck == 1 then
	      		(
	         		if g_ismax then
					(
	            		--g_strBuffer = createFile (export_path + model_name + "." + mdlextn)

						if not doesFileExist export_path or not getFileAttribute export_path #directory do makeDir export_path
						g_strBuffer = createFile (export_path + model_name + "." + mdlextn)
					)

	         		ExportOdysseyModel node anim:doanim

	         		if g_ismax then
                    (
	            		flush g_strBuffer
	            		close g_strBuffer
	         		)
	      		)

	      		if not g_ismax then format "</snoopstart>%" g_delim to:g_strBuffer  -- closes model export

	      		-- copy the tga files to export dir. If converting to dds then put in a tga dir inside export dir
	      		if node.copy_tga == 1 then
	      		(
		      			kx_getTextures node node
      			)

	      		-- perform the tga to dds conversion
	      		if node.tga2dds == 1 then
	      		(
		      		-- process dds
		      		local cmd = g_kotormaxPath + "tga2dds\\processtextures.exe "
		      		cmd += export_path + "\\tga "
		      		cmd += export_path
		      		DOSCommand cmd
	      		)

	      		if not g_ismax then
	      		(
	        		format "\r\n%" g_delim to:g_strBuffer
	      			format "</snoopstart>%" g_delim to:g_strBuffer  -- closes exportcontrol.txt
	      			format "</snoopend>%" g_delim to:g_strBuffer
					--format "Done exporting." to:g_strBuffer
				)
    		)
            else messagebox "Directory does not exist."

    	    clearSelection()
    	    select node
    	    kx_FlushBuffer force:true
		)

		-- support browse for export dir path
		on btn_exportbrowse pressed do
		(
			local p = getSavePath caption:"Model save path"
			if p != undefined then
			(
				edit_exportpath.text = p
			)
		)

		-- reset the export path
    	on btn_exportreset pressed do
		(
			edit_exportpath.text = (getDir #export)
		)

		-- Export model and animations
		on btn_export_mdl pressed do
		(
    		export_start()
		)

		-- Export model geomtrey only
		on btn_export_noanims pressed do
		(
    		export_start doanim:false
		)

		-- Copy TGA switch
		on chk_copytga changed checked do
		(
			if checked then
			(
				chk_convdds.enabled = true
			) else
			(
				chk_convdds.checked = false
				chk_convdds.enabled = false
			)
		)

		-- View the sanity file results
		on btn_viewsanity pressed do
		(
			local model_name = selection[1].name
			local target_file = g_kotormaxPath + "sanity\\" + (kx_CreateExportName model_name) + "_sanitycheck.txt"
			-- test to make sure sanity file exists
			if not kx_existFile(target_file) then
			(
				messageBox "No sanity check file found.\nHave you exported the model?"
				return false
			)
      		if (not g_ismax) then
      		(
	      		clearListener()
				format "<snoopstart file=%>\r\n%" (g_kotormaxPath + "scratch\\sanityviewcontrol.txt") g_delim to:g_strBuffer
				format "<commandstart>%" g_delim to:g_strBuffer
				format "\"notepad %\"%" target_file g_delim to:g_strBuffer
				format "</commandstart>%" g_delim to:g_strBuffer
        		format "\r\n%" g_delim to:g_strBuffer
        		format "\r\n%" g_delim to:g_strBuffer
      			format "</snoopstart>\r\n%" g_delim to:g_strBuffer  -- closes exportcontrol.txt
      			format "</snoopend>%" g_delim to:g_strBuffer
      			kx_FlushBuffer force:true
      		) else
      		(
				format "View sanity file.\r\n"
				shellLaunch "notepad.exe" target_file
      		)

		)

        on btn_animeditor pressed do
        (
            try (filein (g_kotormaxPath + "kotormax_scripts/kx_animeditor.ms")) catch(format "Opening kx_animeditor.ms failed!\n")
        )

		--
        -- Odyssey base rollout has been opened
        --
		on params open do
		(
			current = 0;
			edit_supermodel.text = setsupermodel
			
			if this.layouttype == 2 then lytnum.text = this.layoutnum as string
			else lytnum.text = ""
			
			/*
			if ($ != undefined) then
				modelname.text = "Model:  " + $.name
			else
				modelname.text = "Model: "
			*/
		)
	) ---- rollout params "MDL Base Parameters"
)

-- ModelBase Wizard - beta, by Joco
rollout basewiz "Model Base Wizard"
(
	local modelbase

	edittext ed_name "Name:" offset:[0, 10]
	dropDownList ddl_type "Model Type" width:133 height:40 align:#center items:odysseyClassTypes selection:1 offset:[0, 10]
	checkbox chk_animkeys "Add standard anim keys" width:138 align:#left enabled:false
	checkbox chk_helpers "Add standard helpers" width:138 align:#left enabled:false
	checkbox chk_center "Center to [0,0,0]" width:138 align:#left
	button btn_ok "OK" width:91 height:23 align:#center

	fn addAnims animarray node = (
    	local maxkey = 0
    	local parsedkey
    	-- set base time 0 key
    	with animate on
    	(
           	at time 0 node.scale = [1,1,1] -- 1:1
            at time 0 node.rotation = (quat 0 [0,0,0]) -- zero rot
            at time 0 node.position = [0,0,0]
        )
        for i = 1 to animarray.count do
        (
            parsedkey = filterString animarray[i] " "
        	with animate on
        	(
            	-- start
               	at time (parsedkey[2] as integer - 1) node.scale = [1,1,1] -- 1:1
                at time (parsedkey[2] as integer - 1) node.rotation = (quat 0 [0,0,0]) -- zero rot
                at time (parsedkey[2] as integer - 1) node.position = [0,0,0]
                -- end
               	at time (parsedkey[3] as integer + 1) node.scale = [1,1,1] -- 1:1
                at time (parsedkey[3] as integer + 1) node.rotation = (quat 0 [0,0,0]) -- zero rot
                at time (parsedkey[3] as integer + 1) node.position = [0,0,0]
            )
            if (parsedkey[3] as integer) > maxkey then maxkey = (parsedkey[3] as integer)
        )
        return maxkey
	)

	on basewiz open do
	(
    	modelbase = selection[1]
    	setFocus ed_name
	)

	on btn_ok pressed do
	(
		-- Set the models name
		if ed_name.text != "" then modelbase.name = ed_name.text
    	modelbase.classification_sel = ddl_type.selection
    	local key_list = #()
    	local maxframe = 0
    	case modelbase.classification_sel of
    	(
        	1:( -- Character
            	if chk_animkeys.checked then
            	(
                	/*
                	(creature animations ## = beetle frames per segment)
                    60 -creadyr (default ready to attack)
                    60 -creadyl (alternate ready to attack)
                    10 -cdamagel
                    10 -cdamager
                    10 -cdamages
                    90 -cpause1
                    45 -ca1slashl (defualt claw attack, slash left)
                    40 -cwalk
                    30 -cclosel (the big slam with the leg)
                    45 -ca1slashr (defualt claw attack, slash right)
                    45 -ca1stab (defualt bite attack)
                    30 -ccloseh (the head butt)
                    30 -creach (attack when target moves away)
                    30 -cparryr (parry right)
                    30 -cparryl (parry left)
                    30 -cdodgelr (big dodge)
                    30 -cdodges (small dodge)
                    30 -ckdbck
                    10 -ckdbckps
                    20 -ckdbckdmg
                    30 -ckdbckdie (dying)
                    1 -cdead (static death)
                    30 -cguptokdb (preparing to get up off back)
                    30 -cgustandb (Getting up off back)
                    15 -ccwalkf (combat motion)
                    15 -ccwalkb (backwards)
                    15 -ccwalkl (left)
                    15 -ccwalkr (right)
                    15 -cgetmid
                    30 -chturnl (head turn left)
                    30 -chturnr (head turn right)
                    30 -ctaunt (taunt)
                    43 -cconjure1 (casting motion)
                    30 -ccastoutlp (longer casting outwards motion)
                    60 -cappear Seem to be the same as pause
                    60 -cdisappear Seem to be the same as pause
                    15 -ccastout (casting outwards motion)
                    1 -cgetmidlp
                    20 -cspasm
                    30 -crun
                    15 -ccturnr
                    60 -cdisappearlp same as pause
                    */
                	key_list = #("creadyr 50 110 0.25",
                	            "creadyl 120 180 0.25",
                	            "cdamagel 190 200 0.25",
                	            "cdamager 210 220 0.25",
                	            "cdamages 230 240 0.25",
                	            "cpause1 250 340 0.25",
                	            "ca1slashl 350 395 0.25",
                	            "cwalk 410 450 0.25",
                	            "cclose1 460 490 0.25",
                	            "ca1slashr 500 545 0.25",
                	            "ca1stab 555 600 0.25",
                	            "ccloseh 610 640 0.25",
                	            "creach 650 680 0.25",
                	            "cparryr 690 720 0.25",
                	            "cparryl 730 760 0.25",
                	            "cdodgelr 770 800 0.25",
                	            "cdodges 810 840 0.25",
                	            "ckdbck 850 880 0.25",
                	            "ckdbckps 890 900 0.25",
                	            "ckdbckdmg 910 930 0.25",
                	            "ckdbckdie 940 970 0.25",
                	            "cdead 980 980 0.25",
                	            "cguptokdb 990 1020 0.25",
                	            "cgustandb 1030 1060 0.25",
                	            "ccwalkf 1070 1085 0.25",
                	            "ccwalkb 1095 1110 0.25",
                	            "ccwalkl 1120 1135 0.25",
                	            "ccwalkr 1145 1160 0.25",
                	            "cgetmid 1170 1175 0.25",
                	            "chturnl 1185 1215 0.25",
                	            "chturnr 1225 1255 0.25",
                	            "ctaunt 1265 1295 0.25",
                	            "cconjure1 1305 1348 0.25",
                	            "ccastoutlp 1358 1388 0.25",
                	            "cappear 1400 1460 0.25",
                	            "cdisappear 1470 1530 0.25",
                	            "ccastout 1540 1565 0.25",
                	            "cgetmidlp 1575 1575 0.25",
                	            "cspasm 1585 1605 0.25",
                	            "crun 1615 1645 0.25",
                	            "ccturnr 1655 1670 0.25",
                	            "cdisappearlp 1680 1740 0.25")

    	            maxframe = (addAnims key_list modelbase)
                    -- set animation data to model base gui
                    modelbase.animations = key_list
                    animationRange = (interval 0 (maxframe + 50))
                )

                if chk_helpers.checked then
                (
                    -- place stock helper objects for effect
                    max modify mode
					try (
						local d
						-- rootdummy
						d = dummy()
						d.boxsize = [10,10,10]
						d.name = "rootdummy"
						d.pos = [0,0,120]
						-- Impact
						d = dummy()
						d.boxsize = [10,10,10]
						d.name = "Impact"
						d.pos = [0,0,145]
						-- handconjure
						d = dummy()
						d.boxsize = [10,10,10]
						d.name = "handconjure"
						d.pos = [0,40,150]
						-- headconjure
						d = dummy()
						d.boxsize = [10,10,10]
						d.name = "headconjure"
						d.pos = [0,-14,240]
						-- lforearm
						d = dummy()
						d.boxsize = [10,10,10]
						d.name = "lforearm"
						d.pos = [-27,-3,106]
						-- lhand
						d = dummy()
						d.boxsize = [10,10,10]
						d.name = "lhand"
						d.pos = [-28,0,87]
						-- head
						d = dummy()
						d.boxsize = [10,10,10]
						d.name = "head"
						d.pos = [0,-3,200]
						-- rhand
						d = dummy()
						d.boxsize = [10,10,10]
						d.name = "rhand"
						d.pos = [28,0,87]
					) catch (format "Error creating helpers\r\n")
                )
         	)
        	2:( -- Tile
            	if chk_animkeys.checked then
            	(
                	/* add Tile anim key lists as needed
                	-animloop01  	(moving parts and emitters, like smoke)
                    -Day 	        (The rest of the animations mostly deal
                    -Night 	         with changing the colors of the light
                    -Night2Day 	     sources)
                    -Day2Night
                    -tiledefault
                	*/
                	key_list = #("animloop01 50 100 0.25",
                	            "Day 110 160 0.25",
                	            "Night 170 220 0.25",
                	            "Night2Day 230 280 0.25",
                	            "Day2Night 290 340 0.25",
                	            "tiledefault 350 400 0.25")

    	            maxframe = (addAnims key_list modelbase)
						-- set animation data to model base gui
						modelbase.animations = key_list
						animationRange = (interval 0 (maxframe + 50))
					)

					if chk_helpers.checked then
					(
                        --nothing
					)
        	)
        	3:()
        	4:(
            	if chk_animkeys.checked then
            	(
                	/* add Visual Effect animations
                	-impact
                    -duration
                    -cessation
                	*/
                	key_list = #("impact 50 100 0.25",
                	            "duration 110 160 0.25",
                	            "cessation 170 220 0.25")

    	            maxframe = (addAnims key_list modelbase)
                    -- set animation data to model base gui
                    modelbase.animations = key_list
                    animationRange = (interval 0 (maxframe + 50))
                )

                if chk_helpers.checked then
                (
                    -- place stock helper objects for effect
                )
        	)
        	5:()
        	6:(
            	if chk_animkeys.checked then
            	(
                  	/* Placeable animations
                	-close  	ANIMATION_PLACEABLE_CLOSE (1 frame closed)
                    -close2open 	(from closed to open)
                    -open 	ANIMATION_PLACEABLE_OPEN (1 frame open)
                    -open2close 	(from open to closed)
                    -damage 	(3 frame position change)
                    -die 	(move model below ground plane)
                    -dead 	(1 frame below ground plane)
                    -default 	(normal position)

                    Probably can't have both open/close and on/off animations
                    in the same model. I haven't tried it though.

                    -on 	ANIMATION_PLACEABLE_ACTIVATE (looping animation)
                    -off 	ANIMATION_PLACEABLE_DEACTIVATE (1 frame in off position)
                    -off2on 	(from off to on position)
                    -on2off 	(from on to off position)

                	*/
                 	key_list = #("close 50 51 0.25",
                	            "close2open 60 70 0.25",
                	            "open 80 81 0.25",
                	            "open2close 90 140 0.25",
                	            "damage 150 152 0.25",
                	            "die 160 210 0.25",
                	            "dead 220 221 0.25",
                	            "default 230 280 0.25")

    	            maxframe = (addAnims key_list modelbase)
                    -- set animation data to model base gui
                    modelbase.animations = key_list
                    animationRange = (interval 0 (maxframe + 50))
                )

                if chk_helpers.checked then
                (
	                max modify mode
                    -- place stock helper objects for placeable
						try (
							local d
							-- xxx_ground
							d = dummy()
							d.boxsize = [10,10,10]
							d.name = "xxx_ground"
							d.pos = [0,0,0]
							-- xxx_hand
							d = dummy()
							d.boxsize = [10,10,10]
							d.name = "xxx_hand"
							d.pos = [0,-70,90]
							-- xxx_head
							d = dummy()
							d.boxsize = [10,10,10]
							d.name = "xxx_head"
							d.pos = [0,0,219]
							-- xxx_head_hit
							d = dummy()
							d.boxsize = [10,10,10]
							d.name = "xxx_head_hit"
							d.pos = [0,0,170]
							-- xxx_impact
							d = dummy()
							d.boxsize = [10,10,10]
							d.name = "xxx_impact"
							d.pos = [0,0,86]
							-- xxx_pwk_use01
							d = dummy()
							d.boxsize = [10,10,10]
							d.name = "xxx_pwk_use01"
							d.pos = [0,-92,0]
						) catch (format "Error creating helpers\r\n")
                )
        	)
    	)

    	if chk_center.checked then
    	(
	    	max modify mode
	    	try (
		    	modelbase.pos = [0,0,0]
	    	) catch (format "Error creating helpers\r\n")
    	)

    	destroyDialog basewiz
	)
)

fn createOdysseyBase position =
(
    local oObject = odysseybase pos:position
    oObject.delegate.boxSize = [100,100,5]
    select oObject
	oObject.export_path = g_exportPath
    -- make the pointer to the front
    if g_basepointer == 1 then
    (
        local tri = ngon()
        tri.radius = 25
        tri.nsides = 3
        tri.rotation *= (quat 90 [0,0,1])
        tri.pos =   position + [0,50,0]
        tri.wireColor = (color 92 179 240)
        tri.name = "ignore_basepointer"
        tri.parent = oObject
        oObject.pointer = tri
    )
    return oObject
)

-- #nodePreDelete
fn baseOnDelete =
(
    --format "Welcome to node on delete, here we will gladly rape your soul\n"
    local node = callbacks.notificationParam()
    --print node
    if iskindof node odysseybase then
    (
        --print node.pointer
        if isvalidnode node.pointer then delete node.pointer
    )
)
/*
-- #filePreSave
fn baseOnSave =
(
	for base in $helpers where iskindof base odysseybase do
	(
		base.str_visiblenodes = #()
		for room in base.visiblenodes do
		(
			local SameNamedNodes = for node in $helpers where iskindof node odysseybase and node.name == room.name collect node
			append base.str_visiblenodes (room.name + " " + (SameNamedNodes.count as string))
		)
		base.visiblenodes = #()
	)
)

-- #filePostOpen, #filePostSave
fn baseOnOpen =
(
	for base in $helpers where iskindof base odysseybase do
	(
		base.visiblenodes = #()
		for sRoom in base.str_visiblenodes do
		(
			local tokens = filterString sRoom " "
			local SameNamedNodes = for node in $helpers where iskindof node odysseybase and node.name == tokens[1] collect node
			append base.visiblenodes SameNamedNodes[(tokens[2] as integer)]
		)
		base.str_visiblenodes = #()
	)
)
*/

fn baseOnRename =
(
    local names = callbacks.notificationParam()
	for base in $helpers where iskindof base odysseybase do
	(
		local stData = base.visrooms
		local nIndex = findItem stData names[1]
		if nIndex > 0 then stData[nIndex] = names[2]
		base.visrooms = stData
	)
)

/*
callbacks.addScript #filePreSave "baseOnSave()" id:#baseSave
callbacks.addScript #filePostSave "baseOnOpen()" id:#baseSaveAfter
callbacks.addScript #filePostOpen "baseOnOpen()" id:#baseOpen
*/
callbacks.addScript #nodeRenamed "baseOnRename()" id:#baseRename
callbacks.addScript #nodePreDelete "baseOnDelete()" id:#baseDeath

plugin helper aurorabasebox
name:"AuroraBase"
(
    tool create
    (
        on mousePoint clickNo do
        (
            if modPanel.getCurrentObject() == undefined and clickNo > 1 then
            (
                local oObject = createOdysseyBase gridPoint
                select oObject
                -- enter modify mode to exit mouse tool
                -- and get ready for name updates
                max modify mode
                -- model template wizard to start
                createDialog basewiz modal:true
                -- flick back to max create mode as that is where we started
                --max create mode
                #stop -- stop odysseybase creation in all cases, because we have a dialog
            )
        )
    )
)

fn kx_baseHasContent base =
(
    if base == undefined or not iskindof base odysseybase then return false
    for child in base.children where (not iskindof child odysseybase and not matchpattern child.name pattern:"ignore_*" and not (iskindof child dummy and matchpattern child.name pattern:"door_*")) do return true
    return false
)

fn kx_isTopmostBase base =
(
	if base == undefined or not iskindof base odysseybase then return -1
	local parentnode = base.parent
	while parentnode != undefined do
	(
		if iskindof parentnode odysseybase then return 0
		parentnode = parentnode.parent
	)
	return 1
)
