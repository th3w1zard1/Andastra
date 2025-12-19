# Infinity Engine Investigation Report

**Date:** December 19, 2025  
**Investigation Type:** Engine Architecture Audit  
**Severity:** Critical - Major architectural misunderstanding  
**Reference:** xoreos project (vendor/xoreos/) - authoritative source for BioWare engine architecture

## Executive Summary

**CRITICAL FINDING:** The codebase contains a fundamental misunderstanding about the "Infinity Engine" and has incorrectly placed Mass Effect 1 and 2 code in both the Eclipse and Infinity engine implementations.

**CONFIRMED BY XOREOS:** The xoreos project (vendor/xoreos/), which is the authoritative open-source reimplementation of BioWare's Aurora engine and derivatives, explicitly states:
- Mass Effect games are **NOT in scope** because they use Unreal Engine (FAQ.md line 68-73)
- Eclipse engine = shared code between Dragon Age games (engines/eclipse/rules.mk line 20)
- xoreos supports: NWN, NWN2, KOTOR, KOTOR2, Jade Empire, Sonic Chronicles, The Witcher, Dragon Age Origins, Dragon Age 2
- xoreos does NOT support: Infinity Engine (Baldur's Gate - use GemRB), Mass Effect (Unreal Engine), Dragon Age Inquisition (Frostbite)

### The Truth About Engines

| Engine Name | Games | Era | Architecture |
|------------|-------|-----|--------------|
| **Infinity Engine** | Baldur's Gate (1998), Planescape: Torment (1999), Icewind Dale (2000-2002) | Late 90s/Early 2000s | 2D isometric, pre-rendered backgrounds, sprite-based characters |
| **Aurora Engine** | Neverwinter Nights (2002) | Early 2000s | 3D engine, successor to Infinity |
| **Odyssey Engine** | KOTOR 1 & 2 (2003-2004) | Mid 2000s | 3D engine, based on Aurora |
| **Eclipse Engine** | Dragon Age: Origins (2009) | Late 2000s | 3D engine, proprietary BioWare |
| **Lycium Engine** | Dragon Age 2 (2011) | Early 2010s | Upgraded Eclipse Engine |
| **Unreal Engine 3** | **Mass Effect 1, 2, 3** (2007-2012) | Late 2000s/Early 2010s | **Epic Games' Unreal Engine 3** |

### Critical Mistake

**Mass Effect 1 and Mass Effect 2 DO NOT use the Infinity Engine or Eclipse Engine.**

**They use Unreal Engine 3, developed by Epic Games.**

## Evidence

### xoreos Project (Authoritative Reference)

**Location:** `vendor/xoreos/` in this repository

**xoreos FAQ.md (lines 68-73):**
```
What about the Mass Effect games?
---------------------------------

Unfortunately, the Mass Effect games didn't use BioWare's own engine.
They used the Unreal Engine instead, making support not within the
scope of xoreos.
```

**xoreos Engine Structure:**
- `engines/eclipse/` - Shared code between Dragon Age games (ActionScript/AVM handler)
- `engines/dragonage/` - Dragon Age: Origins implementation
- `engines/dragonage2/` - Dragon Age 2 implementation
- **NO `engines/masseffect/` folder** - Mass Effect is explicitly NOT supported

**xoreos Supported Games (README.md):**
- Neverwinter Nights (Aurora)
- Neverwinter Nights 2
- Knights of the Old Republic (Odyssey)
- Knights of the Old Republic II (Odyssey)
- Jade Empire
- Sonic Chronicles
- The Witcher (Aurora-based)
- Dragon Age: Origins (Eclipse)
- Dragon Age II (Eclipse/Lycium)

**xoreos Explicitly NOT Supported:**
- Infinity Engine games (Baldur's Gate, Icewind Dale) - use GemRB instead
- Mass Effect games - use Unreal Engine (NOT BioWare engine)
- Dragon Age: Inquisition - uses Frostbite (NOT BioWare engine)

**Conclusion:** xoreos confirms that Mass Effect should NOT be in a BioWare engine reimplementation project.

### Web Search Results

1. **Mass Effect + Unreal Engine 3 (Confirmed)**
   - GamesRadar: "Mass Effect: Legendary Edition uses Unreal Engine 3" ([source](https://www.gamesradar.com/mass-effect-legendary-edition-uses-unreal-engine-3-for-good-reason-according-to-the-developers/))
   - Unreal Engine Blog: "BioWare Sculpts Improved Mass Effect Sequel with Unreal Engine 3" ([source](https://www.unrealengine.com/en-US/blog/mass-effect-2))
   - GameSpot: "The original trilogy ran on Unreal Engine 3" ([source](https://www.gamespot.com/articles/why-mass-effect-legendary-edition-doesnt-use-unreal-engine-4/1100-6487003/))

2. **Dragon Age + Eclipse Engine (Confirmed)**
   - Engadget: "Lycium engine, an upgraded version of the Eclipse engine that powered Dragon Age: Origins" ([source](https://www.engadget.com/2011-02-17-a-peek-into-the-technology-behind-dragon-age-2.html))
   - PCGamingWiki: "Dragon Age: Origins" uses Eclipse (BioWare) ([source](https://www.pcgamingwiki.com/wiki/Engine:Eclipse_(BioWare)))
   - Wikipedia: "Origins' Eclipse engine (now called the Lycium engine internally)" ([source](https://en.wikipedia.org/wiki/BioWare))

3. **Infinity Engine (Confirmed)**
   - Wikipedia: "Baldur's Gate (1998), Planescape: Torment (1999), Icewind Dale (2000-2002)" ([source](https://en.wikipedia.org/wiki/Infinity_Engine))
   - Infinity Engine was **succeeded by Aurora Engine** (Neverwinter Nights)

## What We Found in the Codebase

### 1. Infinity Engine Implementation (CORRECT for Baldur's Gate)

File: `src/Andastra/Runtime/Games/Infinity/InfinityEngine.cs`

**Lines 8-13:** CORRECT documentation
```csharp
/// <summary>
/// Infinity Engine implementation for Baldur's Gate, Icewind Dale, and Planescape: Torment.
/// </summary>
```

**BUT THEN...**

### 2. Mass Effect Code INCORRECTLY Placed in Infinity

**Example 1:** `src/Andastra/Runtime/Games/Infinity/Components/InfinityAnimationComponent.cs`
- Lines 12-16: References MassEffect.exe and MassEffect2.exe
- Claims to be "Based on MassEffect.exe and MassEffect2.exe animation systems"
- **WRONG:** Mass Effect uses Unreal Engine 3, NOT Infinity Engine

**Example 2:** `src/Andastra/Runtime/Games/Infinity/Dialogue/InfinityDialogueCameraController.cs`
- Lines 14-18: References MassEffect.exe addresses
- Claims: "Based on MassEffect.exe and MassEffect2.exe dialogue camera system"
- **WRONG:** Mass Effect's dialogue camera is part of Unreal Engine 3's UnrealScript system

**Example 3:** `src/Andastra/Runtime/Games/Infinity/GUI/InfinityGuiManager.cs`
- Line 25: "Infinity GUI Manager (MassEffect.exe, MassEffect2.exe)"
- **WRONG:** Mass Effect uses Unreal Engine 3's Scaleform UI system, NOT Infinity Engine

**Example 4:** `src/Andastra/Runtime/Games/Infinity/Fonts/InfinityBitmapFont.cs`
- Line 22: "Infinity Bitmap Font (MassEffect.exe, MassEffect2.exe)"
- **WRONG:** Mass Effect uses Unreal Engine 3's font rendering system

### 3. Mass Effect Code ALSO in Eclipse (ALSO WRONG)

File: `src/Andastra/Runtime/Games/Eclipse/MassEffect/`
- MassEffectEngine.cs
- MassEffectGameSession.cs
- MassEffectModuleLoader.cs
- MassEffectModuleLoaderBase.cs
- MassEffectSaveSerializer.cs

**Comments in Eclipse files:**
- `EclipseArea.cs` line 26: "Eclipse Engine (Mass Effect/Dragon Age)"
- `EclipseGuiManager.cs` line 22: "Eclipse engine (Dragon Age, Mass Effect)"
- `EclipseSceneBuilder.cs` line 13: "Eclipse engine (Dragon Age Origins, Dragon Age 2, Mass Effect, Mass Effect 2)"

**WRONG:** Mass Effect does NOT use Eclipse Engine!

### 4. Grep Search Results

**Found 361 references to MassEffect.exe across the codebase:**
- 74 files in `Runtime/Games/Infinity/` mention Mass Effect
- Multiple files in `Runtime/Games/Eclipse/` mention Mass Effect
- Documentation files claim Mass Effect uses Eclipse/Infinity

## Why This Is a Problem

### 1. Architectural Confusion
- Mass Effect uses **Unreal Engine 3** (UnrealScript, Unreal's rendering pipeline, Scaleform UI)
- Infinity Engine uses **proprietary BioWare engine** from 1998 (2D isometric, pre-rendered backgrounds)
- Eclipse Engine uses **proprietary BioWare 3D engine** from 2009 (custom rendering, custom scripting)

### 2. Incompatible Systems
- **Mass Effect dialogue:** Unreal Engine's Kismet/conversation system
- **Infinity Engine dialogue:** DLG format with 2D character portraits
- **Eclipse Engine dialogue:** CNV format with 3D cutscenes

### 3. File Format Confusion
- **Mass Effect:** Uses Unreal's package format (.upk, .pcc), NOT BioWare formats
- **Infinity Engine:** Uses BIF/KEY files, NOT ERF/RIM
- **Eclipse Engine:** Uses ERF/RIM files, similar to Aurora/Odyssey

### 4. Impossible to Implement
- You cannot reverse engineer Mass Effect by looking at MassEffect.exe for BioWare-style systems
- Mass Effect's functionality is in **Unreal Engine 3 DLLs**, not MassEffect.exe
- MassEffect.exe is mostly game-specific UnrealScript bytecode and data

## What Should Happen

### Option 1: Remove Mass Effect Entirely (Recommended)
**Reason:** Mass Effect uses Unreal Engine 3, which is:
- A completely different architecture from BioWare's proprietary engines
- Licensed from Epic Games (not owned by BioWare)
- Already has extensive documentation and modding tools
- Uses different file formats (Unreal packages, not GFF/ERF/RIM)
- Uses UnrealScript (not NWScript)

**If you want to support Mass Effect:**
- Study Unreal Engine 3 architecture (NOT BioWare engine architecture)
- Use existing Unreal Engine modding tools (ME3Explorer, etc.)
- Implement Unreal package parsers (NOT GFF parsers)
- Implement UnrealScript VM (NOT NWScript VM)

### Option 2: Create Separate Unreal Engine 3 Implementation (Not Recommended)
**Only if you genuinely want to support Mass Effect:**
1. Create `Runtime/Games/UnrealEngine3/` folder
2. Implement Unreal package parsing
3. Implement UnrealScript VM
4. Implement Unreal rendering pipeline
5. DO NOT mix with Infinity/Aurora/Odyssey/Eclipse code

**But this is a MASSIVE undertaking** because Unreal Engine 3 is a completely different beast.

## Recommendations

### Immediate Actions Required

1. **Remove all Mass Effect references from Infinity folder**
   - Delete or comment out all MassEffect.exe references in `Runtime/Games/Infinity/`
   - Update documentation to reflect Infinity Engine = Baldur's Gate/Planescape/Icewind Dale ONLY

2. **Remove all Mass Effect references from Eclipse folder**
   - Delete or comment out all Mass Effect code in `Runtime/Games/Eclipse/MassEffect/`
   - Update documentation to reflect Eclipse Engine = Dragon Age ONLY

3. **Update Documentation**
   - Clarify that Andastra focuses on BioWare's proprietary engines (Odyssey, Aurora, Eclipse)
   - Clearly state that Mass Effect (Unreal Engine 3) is OUT OF SCOPE
   - Remove Mass Effect from all roadmaps and plans

4. **Update Comments and References**
   - Search for all "MassEffect.exe" references and remove/update them
   - Search for all "MassEffect2.exe" references and remove/update them
   - Update inheritance structure documentation

### Long-Term Considerations

**If you want to support Mass Effect in the future:**
- Create a separate project/module for Unreal Engine 3 games
- Study Unreal Engine 3 architecture (completely different from BioWare engines)
- Use existing Mass Effect modding tools as reference (ME3Explorer, etc.)
- DO NOT try to fit it into the BioWare engine architecture

**Recommended Focus:**
- Odyssey Engine (KOTOR 1 & 2) - You have these games
- Aurora Engine (Neverwinter Nights) - Relatively well-documented
- Eclipse Engine (Dragon Age Origins & 2) - BioWare proprietary, less documented
- **NOT** Infinity Engine (Baldur's Gate) - You don't own these games
- **NOT** Mass Effect (Unreal Engine 3) - You don't own these games, and it's a completely different engine

## Correct Engine Lineage

```
BioWare Proprietary Engines:
└── Infinity Engine (1998)
    └── Aurora Engine (2002)
        └── Odyssey Engine (2003)
            └── Eclipse Engine (2009)
                └── Lycium Engine (2011)
                    └── Frostbite Engine (2014+)

Separate Lineage:
└── Unreal Engine 3 (Epic Games)
    └── Mass Effect 1, 2, 3 (2007-2012)
```

**Mass Effect is NOT part of the BioWare proprietary engine lineage!**

## Conclusion

**CRITICAL:** The codebase has incorrectly placed Mass Effect code in both Infinity and Eclipse engine implementations. Mass Effect uses Unreal Engine 3 (from Epic Games), NOT BioWare's Infinity or Eclipse engines.

**CONFIRMED BY XOREOS:** The xoreos project (vendor/xoreos/), which is the authoritative reference for BioWare engine architecture, explicitly states Mass Effect is NOT in scope because it uses Unreal Engine.

**Action Required:** Remove all Mass Effect references from Infinity and Eclipse implementations, or clearly mark them as incorrect/out-of-scope.

**Recommended Focus:** Follow xoreos's approach:
- ✅ Odyssey (KOTOR 1 & 2) - You own these games
- ✅ Aurora (Neverwinter Nights) - Well-documented
- ✅ Eclipse (Dragon Age Origins & 2) - BioWare proprietary engine
- ❌ Infinity Engine (Baldur's Gate) - Use GemRB instead
- ❌ Mass Effect (Unreal Engine 3) - Completely different engine family

**Reference Implementation:** Study `vendor/xoreos/` for correct engine architecture patterns.

---

**Investigation completed by:** AI Assistant  
**Next Steps:** Review findings with repository owner and plan cleanup

