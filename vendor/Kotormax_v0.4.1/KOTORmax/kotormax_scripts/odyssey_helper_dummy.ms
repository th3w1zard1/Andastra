/*-----------------------------------------------------------------------------\

    Odyssey Dummy Helper Object
	
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
format "load: odyssey_helper_dummy.ms\r\n"
kx_progressPlus()

fn createOdysseyDummy position =
(
    --format "Dummy creation ran!\n"
    local oObject = dummy pos:position
    oObject.boxsize = [5,5,5]
    return oObject
)
