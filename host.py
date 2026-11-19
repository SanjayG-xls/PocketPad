"""PocketPad: local phone-to-XInput bridge. Start with Start PocketPad.cmd."""
import argparse
import asyncio
import contextlib
import ctypes
import io
import ipaddress
import json
import math
import os
from pathlib import Path
import secrets
import struct
import socket
import sys
import time
import webbrowser
import zlib

from aiohttp import web, WSMsgType
import qrcode
import qrcode.image.svg

ROOT = Path(__file__).resolve().parent
BUTTONS = {
    'A': 0x1000, 'B': 0x2000, 'X': 0x4000, 'Y': 0x8000,
    'LB': 0x0100, 'RB': 0x0200, 'BACK': 0x0020, 'START': 0x0010,
    'L3': 0x0040, 'R3': 0x0080, 'GUIDE': 0x0400,
    'UP': 0x0001, 'DOWN': 0x0002, 'LEFT': 0x0004, 'RIGHT': 0x0008,
}
AXES = ('lx', 'ly', 'rx', 'ry', 'lt', 'rt')
DS_BUTTONS = {'A': 0x20, 'B': 0x40, 'X': 0x10, 'Y': 0x80, 'LB': 0x100,
              'RB': 0x200, 'BACK': 0x1000, 'START': 0x2000, 'L3': 0x4000, 'R3': 0x8000}


def neutral():
    return dict(buttons=[], touches=[], touchClick=False, mouse={'dx': 0, 'dy': 0}, **{key: 0.0 for key in AXES})


def clean_input(raw):
    if not isinstance(raw, dict):
        raise ValueError('Input must be an object')
    out = neutral()
    buttons = raw.get('buttons', [])
    if not isinstance(buttons, list) or len(buttons) > 32:
        raise ValueError('Invalid buttons')
    out['buttons'] = sorted({b for b in buttons if isinstance(b, str) and b in BUTTONS})
    for key in AXES:
        value = float(raw.get(key, 0))
        if not math.isfinite(value):
            raise ValueError('Invalid axis')
        out[key] = max(0 if key in ('lt', 'rt') else -1, min(1, value))
    touches = raw.get('touches', [])
    if not isinstance(touches, list) or len(touches) > 2:
        raise ValueError('Touchpad supports two contacts')
    ids = set()
    for touch in touches:
        if not isinstance(touch, dict):
            raise ValueError('Invalid contact')
        contact = {'id': int(touch.get('id', 0)) % 128}
        if contact['id'] in ids:
            raise ValueError('Duplicate contact')
        ids.add(contact['id'])
        for key in ('x', 'y'):
            value = float(touch.get(key, 0))
            if not math.isfinite(value):
                raise ValueError('Invalid touch coordinate')
            contact[key] = max(0, min(1, value))
        out['touches'].append(contact)
    out['touchClick'] = bool(raw.get('touchClick', False))
    mouse = raw.get('mouse', {})
    if isinstance(mouse, dict):
        for key in ('dx', 'dy'):
            value = float(mouse.get(key, 0))
            if not math.isfinite(value):
                raise ValueError('Invalid pointer delta')
            out['mouse'][key] = max(-200, min(200, round(value)))
    return out


def validate_layout(raw):
    if not isinstance(raw, dict):
        raise ValueError('Invalid layout')
    controls = raw.get('controls')
    if not isinstance(controls, list) or len(controls) > 100:
        raise ValueError('Use up to 100 controls')
    result = {
        'name': str(raw.get('name', 'Custom layout'))[:40],
        'theme': raw.get('theme') if raw.get('theme') in ('xbox', 'ps5', 'neon') else 'neon',
        'accent': str(raw.get('accent', '#c5f36b'))[:20],
        'grid': bool(raw.get('grid', True)), 'controls': [],
        'output': raw.get('output', 'xbox') if raw.get('output', 'xbox') in ('xbox', 'ds4') else 'xbox',
        'scale': max(.65, min(1.6, float(raw.get('scale', 1)))),
    }
    if not math.isfinite(result['scale']):
        raise ValueError('Invalid layout scale')
    seen = set()
    for i, c in enumerate(controls):
        if not isinstance(c, dict) or c.get('type') not in ('button', 'stick', 'dpad', 'trigger', 'slider', 'label', 'touchpad'):
            raise ValueError('Unknown control')
        c = {k: c[k] for k in ('id', 'type', 'label', 'mapping', 'x', 'y', 'w', 'h', 'color', 'opacity', 'shape', 'deadzone') if k in c}
        c['id'] = str(c.get('id', f'control-{i}'))[:80]
        if c['id'] in seen:
            raise ValueError('Duplicate control ID')
        seen.add(c['id'])
        c['label'] = str(c.get('label', ''))[:30]
        for key, default, lo, hi in [('w', 70, 24, 600), ('h', 70, 24, 460), ('x', 500, 0, 1000), ('y', 240, 0, 480), ('opacity', 1, .15, 1), ('deadzone', .1, 0, .5)]:
            value = float(c.get(key, default))
            if not math.isfinite(value):
                raise ValueError('Invalid control geometry')
            c[key] = max(lo, min(hi, value))
        c['x'] = max(c['w']/2, min(1000-c['w']/2, c['x']))
        c['y'] = max(c['h']/2, min(480-c['h']/2, c['y']))
        mapping = c.get('mapping', '')
        allowed = {'button': set(BUTTONS) | {'TOUCH_CLICK'}, 'stick': {'left', 'right'}, 'dpad': {'dpad'}, 'trigger': {'lt', 'rt'}, 'slider': set(AXES), 'label': {''}, 'touchpad': {'touchpad'}}
        if mapping not in allowed[c['type']]:
            raise ValueError('Invalid control mapping')
        result['controls'].append(c)
    return result


def pack_ds4_report(state, sequence=0):
    """63-byte extended report, using ViGEmClient's packed wire layout.

    Coordinates occupy 12 bits each; tracking IDs have an active-low contact bit.
    Write bytes explicitly to avoid the old vgamepad DS4_TOUCH ctypes alignment bug.
    """
    data = bytearray(63)
    for offset, axis in enumerate(('lx', 'ly', 'rx', 'ry')):
        data[offset] = max(0, min(255, 128 + round(state[axis] * 127)))
    buttons = set(state['buttons'])
    bits = sum(DS_BUTTONS[b] for b in buttons if b in DS_BUTTONS)
    x = int('RIGHT' in buttons) - int('LEFT' in buttons)
    y = int('DOWN' in buttons) - int('UP' in buttons)
    hat = {(0,0):8, (0,-1):0, (1,-1):1, (1,0):2, (1,1):3,
           (0,1):4, (-1,1):5, (-1,0):6, (-1,-1):7}[(x,y)]
    if state['lt'] > 0: bits |= 0x400
    if state['rt'] > 0: bits |= 0x800
    data[4:6] = (bits | hat).to_bytes(2, 'little')
    data[6] = (sequence & 63) << 2 | int('GUIDE' in buttons) | (2 if state['touchClick'] else 0)
    data[7], data[8] = round(state['lt']*255), round(state['rt']*255)
    data[9:11] = ((sequence*188) & 65535).to_bytes(2, 'little')
    data[11], data[29], data[32], data[33] = 255, 0x1b, 1, sequence & 255
    data[34] = data[38] = 0x80  # Lift both contacts, including in neutral reports.
    for i, touch in enumerate(state['touches'][:2]):
        offset = 34+i*4
        x, y = round(touch['x']*1919), round(touch['y']*942)
        data[offset:offset+4] = bytes((touch['id'] & 127, x & 255, (x >> 8) | ((y & 15) << 4), y >> 4))
    return bytes(data)


class MouseOutput:
    """Relative mouse fallback for the touchpad when output is Xbox/XInput."""
    def __init__(self):
        self.down = False

    def apply(self, state):
        dx, dy = state['mouse']['dx'], state['mouse']['dy']
        down = state['touchClick']
        flags = (1 if dx or dy else 0) | (2 if down and not self.down else 0) | (4 if self.down and not down else 0)
        if flags:
            ctypes.windll.user32.mouse_event(flags, dx, dy, 0, 0)
        self.down = down


class Gamepad:
    def __init__(self, mock=False, output='xbox'):
        self.device = None
        self.mock = mock
        self.output = output
        self.sequence = 0
        self.mouse = MouseOutput()
        self.vg = None
        self.error = ''
        self.connect()

    def connect(self):
        if self.mock:
            self.error = 'Test mode: inputs are monitored; no virtual controller is created.'
            return
        try:
            import vgamepad as vg
            self.vg = vg
            self.device = vg.VDS4Gamepad() if self.output == 'ds4' else vg.VX360Gamepad()
            time.sleep(.25)  # Let Windows finish enumerating the new XInput device.
            # Some bus versions retain their initial stick offsets when the first
            # report is all zero. A one-unit stick update followed by zero forces
            # a fresh centered report, without pressing any buttons.
            if self.output == 'xbox':
                self.device.left_joystick(x_value=1, y_value=0)
                self.device.update()
            self.apply(neutral())
            if self.device is None:
                raise RuntimeError(self.error)
            self.error = ''
        except Exception as exc:
            self.device = None
            self.error = f'Install the bundled ViGEmBus driver, then restart PocketPad. ({type(exc).__name__}: {exc})'

    def set_output(self, output):
        if output == self.output:
            return
        self.apply(neutral())
        self.device = None  # Detach the previous device before attaching the new kind.
        self.output = output
        self.connect()

    def apply(self, state):
        if self.output == 'xbox' and not self.mock:
            self.mouse.apply(state)
        if self.device is None:
            return
        try:
            if self.output == 'ds4':
                from vgamepad.win.vigem_commons import DS4_REPORT_EX
                report = DS4_REPORT_EX()
                raw = pack_ds4_report(state, self.sequence)
                for i, value in enumerate(raw):
                    report.ReportBuffer[i] = value
                self.sequence = (self.sequence+1) & 65535
                self.device.update_extended_report(report)
                return
            self.device.reset()
            for name in state['buttons']:
                self.device.press_button(button=BUTTONS[name])
            self.device.left_joystick_float(x_value_float=state['lx'], y_value_float=-state['ly'])
            self.device.right_joystick_float(x_value_float=state['rx'], y_value_float=-state['ry'])
            self.device.left_trigger_float(value_float=state['lt'])
            self.device.right_trigger_float(value_float=state['rt'])
            self.device.update()
        except Exception as exc:
            if self.output == 'xbox' and not self.mock:
                self.mouse.apply(neutral())
            self.error = f'Controller output failed: {exc}. Restart PocketPad.'
            self.device = None


def network_addresses():
    addresses = set()
    primary = None
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as probe:
            probe.connect(('8.8.8.8', 80))
            primary = probe.getsockname()[0]
            addresses.add(primary)
    except OSError:
        pass
    try:
        addresses.update(x[4][0] for x in socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET))
    except OSError:
        pass
    addresses = [x for x in addresses if not ipaddress.ip_address(x).is_loopback and not x.startswith('169.254.')]
    return sorted(addresses, key=lambda x: (x != primary, x)) or ['127.0.0.1']


class Host:
    def __init__(self, port, mock=False, data_dir=None):
        self.port = port
        self.token = secrets.token_urlsafe(18)
        self.pad = Gamepad(mock)
        self.state = neutral()
        self.last_input = 0
        self.owner = None
        self.peers = {}
        self.addresses = network_addresses()
        self.data_dir = Path(data_dir) if data_dir else ROOT / 'data'
        self.data_dir.mkdir(parents=True, exist_ok=True)
        self.config_file = self.data_dir / 'presets.json'
        self.builtins = json.loads((ROOT / 'web' / 'presets.json').read_text(encoding='utf-8'))
        self.custom = [None, None]
        self.active = 0
        try:
            stored = json.loads(self.config_file.read_text(encoding='utf-8'))
            self.custom = [validate_layout(x) if x else None for x in stored['custom'][:2]]
            if len(self.custom) != 2:
                raise ValueError('Invalid preset slots')
            self.active = int(stored.get('active', 0))
            if self.active not in range(4) or self.layout(self.active) is None:
                self.active = 0
        except (OSError, ValueError, TypeError, KeyError):
            self.custom, self.active = [None, None], 0
        self.task = None
        self.pad.set_output(self.layout(self.active).get('output', 'xbox'))

    def layout(self, slot):
        return (self.builtins + self.custom)[slot]

    def config(self):
        return {'type': 'config', 'presets': self.builtins + self.custom, 'active': self.active}

    def status(self):
        phones = [peer for peer in self.peers.values() if peer.get('role') == 'controller']
        return {'type': 'status', 'version': '2.1', 'desktopApi': 1, 'driver': self.pad.device is not None, 'mock': self.pad.mock,
                'output': self.pad.output,
                'driverError': self.pad.error, 'connected': self.owner is not None,
                'paired': bool(phones), 'playing': self.owner is not None,
                'controllerName': self.peers.get(self.owner, phones[0] if phones else {}).get('name', ''),
                'input': self.state, 'age': round(time.monotonic() - self.last_input, 2) if self.last_input else None}

    def persist(self):
        temp = self.config_file.with_suffix('.tmp')
        temp.write_text(json.dumps({'custom': self.custom, 'active': self.active}, indent=2), encoding='utf-8')
        os.replace(temp, self.config_file)

    def reset(self):
        self.state = neutral()
        self.pad.apply(self.state)

    async def broadcast(self, message, monitors_only=False):
        for ws, info in list(self.peers.items()):
            if monitors_only and info['role'] != 'monitor':
                continue
            if not ws.closed:
                with contextlib.suppress(ConnectionError, RuntimeError):
                    await ws.send_json(message)

    async def info(self, request):
        return web.json_response({**self.status(), **self.config(), 'token': self.token, 'port': self.port,
            'urls': [f'http://{ip}:{self.port}/play?token={self.token}' for ip in self.addresses]})

    async def qr(self, request):
        host = request.query.get('host', self.addresses[0])
        if host not in self.addresses:
            raise web.HTTPBadRequest(text='Unknown network interface')
        url = f'http://{host}:{self.port}/android' if request.query.get('target') == 'android' else f'http://{host}:{self.port}/play?token={self.token}'
        qr = qrcode.make(url, image_factory=qrcode.image.svg.SvgPathImage, border=3)
        buffer = io.BytesIO()
        qr.save(buffer)
        return web.Response(body=buffer.getvalue(), content_type='image/svg+xml')

    async def qr_png(self, request):
        host = request.query.get('host', self.addresses[0])
        if host not in self.addresses:
            raise web.HTTPBadRequest(text='Unknown network interface')
        url = f'http://{host}:{self.port}/android' if request.query.get('target') == 'android' else f'http://{host}:{self.port}/play?token={self.token}'
        code = qrcode.QRCode(border=3)
        code.add_data(url)
        code.make(fit=True)
        matrix = code.get_matrix()
        scale = 6
        width = len(matrix)*scale
        rows = b''.join((b'\0'+bytes(0 if value else 255 for value in row for _ in range(scale)))*scale for row in matrix)
        def chunk(kind, body):
            return struct.pack('!I', len(body))+kind+body+struct.pack('!I', zlib.crc32(kind+body) & 0xffffffff)
        image = b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR', struct.pack('!2I5B', width, width, 8, 0, 0, 0, 0))+chunk(b'IDAT', zlib.compress(rows))+chunk(b'IEND', b'')
        return web.Response(body=image, content_type='image/png')

    def require_desktop(self, request):
        if request.remote not in ('127.0.0.1', '::1'):
            raise web.HTTPForbidden(text='This operation is for the local desktop application.')

    async def desktop_action(self, request):
        self.require_desktop(request)
        try:
            data = await request.json()
            kind = data.get('type')
            if kind == 'disconnect':
                if self.owner:
                    await self.owner.close(code=1000, message=b'Disconnected from PC')
            elif kind in ('save', 'activate', 'delete'):
                slot = data.get('slot')
                if type(slot) is not int or slot not in range(4):
                    raise ValueError('Invalid preset slot')
                if kind == 'save':
                    if slot < 2:
                        raise ValueError('Save edits to one of the two custom slots')
                    self.custom[slot-2] = validate_layout(data.get('layout'))
                    self.active = slot
                elif kind == 'delete':
                    if slot < 2:
                        raise ValueError('Factory presets cannot be deleted')
                    self.custom[slot-2] = None
                    if self.active == slot:
                        self.active = 0
                else:
                    if self.layout(slot) is None:
                        raise ValueError('Save a layout to this slot first')
                    self.active = slot
                self.reset()
                self.pad.set_output(self.layout(self.active).get('output', 'xbox'))
                self.persist()
                await self.broadcast(self.config())
                await self.broadcast(self.status())
            else:
                raise ValueError('Unknown desktop action')
            return web.json_response({**self.config(), **self.status()})
        except (ValueError, TypeError, KeyError) as exc:
            return web.json_response({'error': str(exc)}, status=400)

    async def desktop_stop(self, request):
        self.require_desktop(request)
        def stop():
            raise web.GracefulExit()
        asyncio.get_running_loop().call_later(.15, stop)
        return web.json_response({'stopping': True})

    async def websocket(self, request):
        role = request.query.get('role', 'monitor')
        if role not in ('monitor', 'controller'):
            raise web.HTTPBadRequest()
        if role == 'controller' and not secrets.compare_digest(request.query.get('token', ''), self.token):
            raise web.HTTPForbidden(text='Scan the current QR code on the PC.')
        ws = web.WebSocketResponse(heartbeat=10, max_msg_size=128*1024)
        await ws.prepare(request)
        self.peers[ws] = {'role': role, 'name': request.query.get('name', 'Phone controller')[:60]}
        await ws.send_json(self.config())
        await ws.send_json(self.status())
        try:
            async for msg in ws:
                if msg.type != WSMsgType.TEXT:
                    continue
                try:
                    data = json.loads(msg.data)
                    kind = data.get('type')
                    if kind == 'ping':
                        await ws.send_json({'type': 'pong', 'time': data.get('time')})
                    elif kind == 'claim' and role == 'controller':
                        if self.owner is not None and self.owner is not ws:
                            await ws.send_json({'type': 'busy', 'message': 'Another phone is playing. Disconnect it first.'})
                        else:
                            self.reset()
                            self.owner = ws
                            self.last_input = time.monotonic()
                            await ws.send_json({'type': 'claimed'})
                            await self.broadcast(self.status())
                    elif kind == 'release' and self.owner is ws:
                        self.reset()
                        self.owner = None
                        await ws.send_json({'type': 'released'})
                        await self.broadcast(self.status())
                    elif kind == 'input' and self.owner is ws:
                        self.state = clean_input(data.get('state'))
                        self.last_input = time.monotonic()
                        self.pad.apply(self.state)
                    elif kind in ('save', 'activate', 'delete'):
                        slot = data.get('slot')
                        if type(slot) is not int or slot not in range(4):
                            raise ValueError('Invalid preset slot')
                        if kind == 'save':
                            if slot < 2:
                                raise ValueError('Save edits into Custom 1 or Custom 2')
                            self.custom[slot-2] = validate_layout(data.get('layout'))
                            self.active = slot
                        elif kind == 'delete':
                            if slot < 2:
                                raise ValueError('Built-in presets cannot be deleted')
                            self.custom[slot-2] = None
                            if self.active == slot:
                                self.active = 0
                        else:
                            if self.layout(slot) is None:
                                raise ValueError('Save a layout to this slot first')
                            self.active = slot
                        self.reset()
                        self.pad.set_output(self.layout(self.active).get('output', 'xbox'))
                        self.persist()
                        await self.broadcast(self.config())
                        await self.broadcast(self.status())
                        await ws.send_json({'type': 'saved', 'action': kind})
                    elif kind == 'disconnect' and role == 'monitor' and self.owner:
                        await self.owner.close(code=1000, message=b'Disconnected from PC')
                except (ValueError, TypeError, AttributeError, KeyError) as exc:
                    await ws.send_json({'type': 'error', 'message': str(exc)})
        finally:
            self.peers.pop(ws, None)
            if self.owner is ws:
                self.owner = None
                self.reset()
            await self.broadcast(self.status())
        return ws

    async def ticker(self):
        while True:
            if self.owner and time.monotonic() - self.last_input > .6:
                self.reset()
            await self.broadcast(self.status(), monitors_only=True)
            await asyncio.sleep(.1)

    async def startup(self, app):
        self.task = asyncio.create_task(self.ticker())

    async def shutdown(self, app):
        if self.task:
            self.task.cancel()
            with contextlib.suppress(asyncio.CancelledError):
                await self.task
        self.reset()
        for peer in list(self.peers):
            await peer.close(code=1001, message=b'Host stopped')

    def app(self):
        app = web.Application(client_max_size=128*1024)
        app.router.add_get('/api/info', self.info)
        app.router.add_get('/api/qr.svg', self.qr)
        app.router.add_get('/api/qr.png', self.qr_png)
        app.router.add_post('/api/desktop/action', self.desktop_action)
        app.router.add_post('/api/desktop/stop', self.desktop_stop)
        async def android_page(request):
            return web.FileResponse(ROOT / 'web/android.html')
        async def apk_download(request):
            apk = ROOT / 'PocketPad-Android.apk'
            if not apk.exists():
                raise web.HTTPNotFound(text='Build the Android APK first.')
            return web.FileResponse(apk, headers={'Content-Type': 'application/vnd.android.package-archive',
                'Content-Disposition': 'attachment; filename="PocketPad-Android.apk"'})
        app.router.add_get('/android', android_page)
        app.router.add_get('/download/PocketPad.apk', apk_download)
        app.router.add_get('/ws', self.websocket)
        async def index(request):
            return web.FileResponse(ROOT / 'web' / 'index.html', headers={'Cache-Control': 'no-store'})
        app.router.add_get('/', index)
        app.router.add_get('/play', index)
        app.router.add_static('/assets/', ROOT / 'web')
        app.on_startup.append(self.startup)
        app.on_shutdown.append(self.shutdown)
        return app


def main():
    parser = argparse.ArgumentParser(description='PocketPad local controller host')
    parser.add_argument('--port', type=int, default=8765)
    parser.add_argument('--no-browser', action='store_true')
    parser.add_argument('--mock', action='store_true', help='Monitor inputs without emulating a gamepad')
    parser.add_argument('--data-dir', help='Directory for personal layout data')
    args = parser.parse_args()
    host = Host(args.port, args.mock, data_dir=args.data_dir)
    print('\n  POCKETPAD  /  Your phone. Your controller.\n', flush=True)
    print(f'  PC studio: http://localhost:{args.port}', flush=True)
    for ip in host.addresses:
        print(f'  Phone:     http://{ip}:{args.port}/play?token={host.token}', flush=True)
    print('\n  Keep this window open while playing. Press Ctrl+C to stop.\n', flush=True)
    if host.pad.error:
        print(f'  {host.pad.error}\n', flush=True)
    if not args.no_browser:
        import threading
        threading.Timer(1.5, lambda: webbrowser.open(f'http://localhost:{args.port}')).start()
    web.run_app(host.app(), host='0.0.0.0', port=args.port, print=None)


if __name__ == '__main__':
    main()
