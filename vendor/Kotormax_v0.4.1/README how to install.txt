KOTORmax v0.4.1 by bead-v, 
extended from NWmax by Joco.

You don't need to uninstall NWmax for this to work, but you will have to disable it being loaded on startup, and you will not be able to use both NWmax and KOTORmax at the same time - they use the same globals and would therefore break each other. You may switch them around between sessions though.

Install instructions:
1. Put the 'KOTORmax' subfolder into your scripts folder under the root 3dsmax/gmax directory. 

2. If it doesn't exist yet, create a folder named 'startup' in the scripts folder (where you just copied the KOTORmax folder to). Copy the file 'autokotormax.ms' into the 'startup' folder. Now, if you have NWmax installed, you will need to remove 'autonwmax.ms' from the 'startup' folder. I suggest you move it into the 'NWmax' folder, since you may need it again later.

3. Only if you use a version of 3dsmax as old or older than gmax (3ds Max 8 or older):
Go into your KOTORmax subfolder and open kotormax.ini. Find the line 'usemax=0' and change the 0 to 1, then save. If you don't do this, you will not be able to export anything.

4. Only if you use gmax:
You need to copy 'kotormax.exe' to your gmax root folder. From now on, you always run that instead of gmax.exe, and it will open gmax for you. If you don't do this, you will not be able to export anything.



