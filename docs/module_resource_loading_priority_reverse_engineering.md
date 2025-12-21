# Module Resource Loading Priority - Reverse Engineering

## Resource Search Order (PROVEN by Code)

**Function**: `FUN_00407230` (swkotor.exe: 0x00407230)

**Search Order** (lines 8-16):

```c
iVar1 = FUN_004071a0((undefined4 *)((int)this + 0x14),param_1,param_2,param_3,param_4,0);  // 1st: this+0x14
if (iVar1 == 0) {
  iVar1 = FUN_004071a0((undefined4 *)((int)this + 0x18),param_1,param_2,param_3,param_4,1);  // 2nd: this+0x18 (param_6=1)
  if (iVar1 == 0) {
    iVar1 = FUN_004071a0((undefined4 *)((int)this + 0x1c),param_1,param_2,param_3,param_4,0);  // 3rd: this+0x1c
    if (iVar1 == 0) {
      iVar1 = FUN_004071a0((undefined4 *)((int)this + 0x18),param_1,param_2,param_3,param_4,2);  // 4th: this+0x18 (param_6=2)
      if (iVar1 == 0) {
        iVar1 = FUN_004071a0((undefined4 *)((int)this + 0x10),param_1,param_2,param_3,param_4,0);  // 5th: this+0x10
```

**Source Type Identification** (`FUN_004074d0` line 44-60):

```c
iVar2 = *(int *)(local_2c + 0x1c);  // Get source type
if (iVar2 == 1) {
  FUN_005e5140(local_24,"BIF");  // BIF = Chitin archives
}
else if (iVar2 == 2) {
  // DIR = Override directory
  iVar2 = FUN_004071a0((undefined4 *)((int)this + 0x1c),this_00,param_2,&param_1,&param_2,0);
  if (iVar2 == 0) {
    pcVar4 = "DIR";
  }
}
else {
  if (iVar2 == 3) {
    pcVar4 = "ERF";  // ERF = Module containers (MOD/ERF)
  }
  // ...
}
```

**Source Type Values**:

- `1` = BIF (Chitin archives)
- `2` = DIR (Override directory)
- `3` = ERF (Module containers: MOD/ERF)
- `4` = RIM (Module RIM files)

**Resource Location Mapping** (`FUN_004076e0` lines 9-21):

```c
switch((uint)param_1[2] >> 0x1e) {  // Check high 2 bits of param_1[2]
case 0:
  param_1 = (undefined4 *)((int)this + 0x10);  // Location 0
  break;
case 1:
  param_1 = (undefined4 *)((int)this + 0x1c);  // Location 1
  break;
case 2:
  param_1 = (undefined4 *)((int)this + 0x18);  // Location 2
  break;
case 3:
  param_1 = (undefined4 *)((int)this + 0x14);  // Location 3
  break;
}
```

**Priority Order** (from `FUN_00407230`):

1. **this+0x14** (Location 3) - Highest priority
2. **this+0x18** with param_6=1 (Location 2, variant 1)
3. **this+0x1c** (Location 1)
4. **this+0x18** with param_6=2 (Location 2, variant 2)
5. **this+0x10** (Location 0) - Lowest priority

**Module Loading Evidence** (`FUN_004094a0`):

- Line 91: `FUN_00406e20(param_1,aiStack_48,2,0);` - Loads MODULES: with type 2 (DIR/Override)
- Line 136: `FUN_00406e20(param_1,aiStack_38,3,2);` - Loads MODULES: with type 3 (ERF/MOD)
- Line 42/85/118/159: `FUN_00406e20(param_1,aiStack_38,4,0);` - Loads RIMS: with type 4 (RIM)

**Conclusion**: Modules are loaded into the resource table and searched in priority order. The search function `FUN_00407230` will find resources from modules if they're registered in the resource table.
