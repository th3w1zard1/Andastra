# HolocronToolset / Module Walkability Issues

## Problem 1 — v4 Beta: Complete Immobility
- Modules generated with the **v4 beta Indoor Map Builder** (K1 or K2)
- Warping into the module results in:
  - Player character is **completely immobile**
  - Frozen in place
- **Not** caused by surface materials or walkmesh flags (already ruled out)

---

## Problem 2 — KAurora Workaround: Room Transition Failure
- Processing a v4‑generated module through **KAurora**:
  - Restores **walkability within a single room**
  - **However:** Player cannot move through **doorways** or **room transitions**
- Occurs in **both K1 and K2**
- Indicates transition hooks or adjacency data are not being preserved or interpreted correctly

---

## Problem 3 — v2.0.4 Conversion Issue: K2 Transition Failure
- HolocronToolset **v2.0.4**:
  - Works correctly for **K1** (walkable, transitions functional)
- After converting that K1 module to **K2** using **MDLEdit**:
  - Room‑to‑room walkability breaks
  - Door hooks / transition data appear corrupted or lost
- Likely the same underlying issue as Problem 2, but triggered during the conversion process
