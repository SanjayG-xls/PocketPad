"""Submit real two-contact DS4 reports, then release every contact and button."""
from pathlib import Path
import sys
import time

sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from host import Gamepad, clean_input, neutral

pad=Gamepad(output='ds4')
assert pad.device is not None,pad.error
try:
    pad.apply(clean_input({'touches':[{'id':1,'x':.25,'y':.3},{'id':2,'x':.75,'y':.8}]}))
    assert pad.device is not None,pad.error
    time.sleep(.05)
    pad.apply(neutral())
    assert pad.device is not None,pad.error
    print('PASS: ViGEmBus accepted native DS4 two-contact extended reports and contact release.')
finally:
    pad.apply(neutral())
