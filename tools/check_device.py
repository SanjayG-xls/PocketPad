"""Verify a real neutral virtual controller is visible through Windows XInput."""
import ctypes
from pathlib import Path
import sys
import time

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from host import Gamepad, neutral


class Pad(ctypes.Structure):
    _fields_=[('buttons',ctypes.c_ushort),('lt',ctypes.c_ubyte),('rt',ctypes.c_ubyte),
              ('lx',ctypes.c_short),('ly',ctypes.c_short),('rx',ctypes.c_short),('ry',ctypes.c_short)]


class State(ctypes.Structure):
    _fields_=[('packet',ctypes.c_uint32),('pad',Pad)]


api=ctypes.WinDLL('xinput1_4.dll')
api.XInputGetState.argtypes=[ctypes.c_uint32,ctypes.POINTER(State)]
api.XInputGetState.restype=ctypes.c_uint32


def states():
    found={}
    for index in range(4):
        state=State()
        if api.XInputGetState(index,ctypes.byref(state))==0:
            found[index]=state.pad
    return found


before=states()
pad=Gamepad()
assert pad.device is not None,pad.error
for _ in range(30):
    after=states()
    added=set(after)-set(before)
    if added:break
    time.sleep(.1)
assert added,'No new XInput slot found. Close other controllers if all four slots are occupied.'
index=next(iter(added))
for _ in range(30):
    pad.apply(neutral())
    time.sleep(.1)
    after=states()
    if index in after and all(getattr(after[index],name)==0 for name,_ in Pad._fields_):break
assert all(getattr(after[index],name)==0 for name,_ in Pad._fields_),str({name:getattr(after[index],name) for name,_ in Pad._fields_})
print(f'PASS: real virtual Xbox controller detected in Windows XInput slot {index}; neutral report verified.')
