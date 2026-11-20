"""Windows mouse/keyboard output for the paired phone's desktop mode."""
import ctypes
import math


class MouseInput(ctypes.Structure):
    _fields_ = [('dx', ctypes.c_int32), ('dy', ctypes.c_int32),
                ('data', ctypes.c_uint32), ('flags', ctypes.c_uint32),
                ('time', ctypes.c_uint32), ('extra', ctypes.c_size_t)]


class KeyboardInput(ctypes.Structure):
    _fields_ = [('vk', ctypes.c_uint16), ('scan', ctypes.c_uint16),
                ('flags', ctypes.c_uint32), ('time', ctypes.c_uint32), ('extra', ctypes.c_size_t)]


class InputData(ctypes.Union):
    _fields_ = [('mouse', MouseInput), ('keyboard', KeyboardInput)]


class Input(ctypes.Structure):
    _anonymous_ = ('value',)
    _fields_ = [('type', ctypes.c_uint32), ('value', InputData)]


KEYS = dict(Enter=13, Backspace=8, Tab=9, Escape=27, Space=32, Delete=46,
            ArrowLeft=37, ArrowUp=38, ArrowRight=39, ArrowDown=40,
            Home=36, End=35, PageUp=33, PageDown=34, Insert=45,
            **{f'F{i}': 111+i for i in range(1, 13)})
MODIFIERS = dict(Ctrl=17, Alt=18, Shift=16, Win=91)
GESTURES = {
    'app_next': (['Alt'], 'Tab'), 'app_previous': (['Alt', 'Shift'], 'Tab'),
    'task_view': (['Win'], 'Tab'), 'show_desktop': (['Win'], 'D'),
    'desktop_next': (['Ctrl', 'Win'], 'ArrowRight'),
    'desktop_previous': (['Ctrl', 'Win'], 'ArrowLeft'),
    'search': (['Win'], 'S'), 'quick_settings': (['Win'], 'A'),
}
BUTTONS = {'left': (2, 4), 'right': (8, 16), 'middle': (32, 64)}


def number(value, limit):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError('Invalid desktop input number')
    return round(max(-limit, min(limit, value)))


class DesktopInput:
    def __init__(self, mock=False):
        self.mock = mock
        self.held = set()
        self.send_input = None
        if not mock:
            user32 = ctypes.WinDLL('user32', use_last_error=True)
            self.send_input = user32.SendInput
            self.send_input.argtypes = [ctypes.c_uint, ctypes.POINTER(Input), ctypes.c_int]
            self.send_input.restype = ctypes.c_uint

    @staticmethod
    def mouse(flags, dx=0, dy=0, data=0):
        return Input(type=0, mouse=MouseInput(dx, dy, data & 0xffffffff, flags, 0, 0))

    @staticmethod
    def key(vk=0, scan=0, up=False):
        extended = vk in (33, 34, 35, 36, 37, 38, 39, 40, 45, 46, 91)
        return Input(type=1, keyboard=KeyboardInput(vk, scan,
            (4 if scan else 1 if extended else 0) | (2 if up else 0), 0, 0))

    def emit(self, events):
        if not events or self.mock:
            return
        batch = (Input * len(events))(*events)
        sent = self.send_input(len(events), batch, ctypes.sizeof(Input))
        if sent != len(events):
            # A partial batch must never leave one of our modifiers pressed.
            cleanup = [self.key(vk, up=True) for vk in MODIFIERS.values()]
            cleanup += [self.mouse(flags[1]) for flags in BUTTONS.values()]
            self.send_input(len(cleanup), (Input * len(cleanup))(*cleanup), ctypes.sizeof(Input))
            self.held.clear()
            raise ValueError('Windows could not send input to the focused app. Check whether that app is running as administrator.')

    def chord(self, modifiers, events):
        self.emit([self.key(MODIFIERS[name]) for name in modifiers] + events +
                  [self.key(MODIFIERS[name], up=True) for name in reversed(modifiers)])

    def reset(self):
        if self.held:
            events = [self.mouse(BUTTONS[name][1]) for name in sorted(self.held)]
            self.held.clear()
            try:
                self.emit(events)
            except ValueError:
                pass  # emit already attempted its emergency key/button releases.

    def apply(self, data):
        if not isinstance(data, dict):
            raise ValueError('Invalid desktop input')
        action = data.get('action')
        if action == 'move':
            dx, dy = number(data.get('dx', 0), 2000), number(data.get('dy', 0), 2000)
            if dx or dy:
                self.emit([self.mouse(1, dx, dy)])
        elif action == 'scroll':
            dx, dy = number(data.get('dx', 0), 1200), number(data.get('dy', 0), 1200)
            self.emit(([self.mouse(0x1000, data=dx)] if dx else []) +
                      ([self.mouse(0x800, data=dy)] if dy else []))
        elif action in ('button', 'click'):
            name = data.get('button', 'left')
            if name not in BUTTONS:
                raise ValueError('Unknown mouse button')
            down, up = BUTTONS[name]
            if action == 'click':
                count = data.get('count', 1)
                if type(count) is not int or count not in (1, 2):
                    raise ValueError('Invalid click count')
                self.emit([self.mouse(flag) for _ in range(count) for flag in (down, up)])
            else:
                pressed = data.get('down')
                if type(pressed) is not bool:
                    raise ValueError('Invalid mouse button state')
                if pressed != (name in self.held):
                    self.emit([self.mouse(down if pressed else up)])
                    self.held.add(name) if pressed else self.held.discard(name)
        elif action in ('key', 'text', 'zoom', 'gesture'):
            modifiers = data.get('modifiers', [])
            if not isinstance(modifiers, list) or len(modifiers) > 4 or any(not isinstance(m, str) or m not in MODIFIERS for m in modifiers):
                raise ValueError('Invalid keyboard modifiers')
            modifiers = list(dict.fromkeys(modifiers))
            if action == 'gesture':
                name = data.get('name')
                if name not in GESTURES:
                    raise ValueError('Unknown touchpad gesture')
                modifiers, key = GESTURES[name]
                vk = KEYS.get(key, ord(key) if len(key) == 1 else 0)
                events = [self.key(vk), self.key(vk, up=True)]
            elif action == 'zoom':
                steps = number(data.get('steps', 0), 5)
                modifiers, events = ['Ctrl'], [self.mouse(0x800, data=steps*120)] if steps else []
            elif action == 'key':
                key = data.get('key')
                if not isinstance(key, str) or (key not in KEYS and not (len(key) == 1 and key.isascii() and key.isalnum())):
                    raise ValueError('Unknown keyboard key')
                vk = KEYS.get(key, ord(key.upper()) if len(key) == 1 else 0)
                events = [self.key(vk), self.key(vk, up=True)]
            else:
                value = data.get('text')
                if not isinstance(value, str) or len(value) > 2048 or '\0' in value:
                    raise ValueError('Text must contain at most 2048 characters')
                if modifiers:
                    if len(value) != 1 or not value.isascii() or not value.isalnum():
                        raise ValueError('Use a single letter or digit with shortcut modifiers')
                    vk = ord(value.upper())
                    events = [self.key(vk), self.key(vk, up=True)]
                else:
                    encoded = value.replace('\r\n', '\n').encode('utf-16-le')
                    events = []
                    for offset in range(0, len(encoded), 2):
                        unit = int.from_bytes(encoded[offset:offset+2], 'little')
                        if unit in (10, 13, 9):
                            vk = 9 if unit == 9 else 13
                            events.extend([self.key(vk), self.key(vk, up=True)])
                        else:
                            events.extend([self.key(scan=unit), self.key(scan=unit, up=True)])
            if events:
                self.chord(modifiers, events)
        elif action == 'release':
            self.reset()
        else:
            raise ValueError('Unknown desktop input action')
