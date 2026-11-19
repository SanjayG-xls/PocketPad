r"""Focused host checks. Run with runtime\python.exe tools\check_host.py."""
import asyncio
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from aiohttp.test_utils import TestClient, TestServer
from host import Host, Gamepad, clean_input, neutral, validate_layout, pack_ds4_report


async def until(ws, kind):
    for _ in range(100):
        msg = await ws.receive_json(timeout=3)
        if msg.get('type') == kind:
            return msg
    raise AssertionError(f'Message {kind} not received')


class HostChecks(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.host = Host(8765, mock=True, data_dir=self.temp.name)
        self.client = TestClient(TestServer(self.host.app()))
        await self.client.start_server()

    async def asyncTearDown(self):
        await self.client.close()
        self.temp.cleanup()

    async def phone(self):
        ws = await self.client.ws_connect('/ws?role=controller&token='+self.host.token)
        await until(ws, 'status')
        return ws

    async def test_pairing_qr_and_token(self):
        info = await (await self.client.get('/api/info')).json()
        self.assertEqual(len(info['presets']), 4)
        self.assertFalse(info['driver'])
        self.assertTrue(info['mock'])
        qr = await self.client.get('/api/qr.svg')
        self.assertEqual(qr.status, 200)
        self.assertIn('<svg', await qr.text())
        bad = await self.client.get('/ws?role=controller&token=wrong')
        self.assertEqual(bad.status, 403)

    async def test_android_install_page_and_download(self):
        page=await self.client.get('/android')
        self.assertEqual(page.status,200)
        self.assertIn('/download/PocketPad.apk',await page.text())
        qr=await self.client.get('/api/qr.svg?target=android')
        self.assertEqual(qr.status,200)
        apk=await self.client.get('/download/PocketPad.apk')
        built=Path(__file__).resolve().parents[1]/'PocketPad-Android.apk'
        if built.exists():
            self.assertEqual(apk.status,200)
            self.assertEqual(apk.headers['Content-Type'],'application/vnd.android.package-archive')
            self.assertEqual(await apk.read(),built.read_bytes())
        else:
            self.assertEqual(apk.status,404)

    async def test_native_desktop_save_and_qr_png(self):
        layout=json.loads(json.dumps(self.host.builtins[1]))
        layout['name']='Native edited layout'
        response=await self.client.post('/api/desktop/action',json={'type':'save','slot':2,'layout':layout})
        self.assertEqual(response.status,200)
        self.assertEqual(self.host.custom[0]['name'],'Native edited layout')
        self.assertEqual(self.host.pad.output,'ds4')
        png=await self.client.get('/api/qr.png')
        self.assertEqual(png.headers['Content-Type'],'image/png')
        self.assertTrue((await png.read()).startswith(b'\x89PNG\r\n\x1a\n'))
        invalid=await self.client.post('/api/desktop/action',json={'type':'save','slot':0,'layout':layout})
        self.assertEqual(invalid.status,400)

    async def test_paused_phone_stays_paired(self):
        phone=await self.phone()
        self.assertTrue(self.host.status()['paired'])
        self.assertFalse(self.host.status()['playing'])
        await phone.send_json({'type':'claim'})
        await until(phone,'claimed')
        self.assertTrue(self.host.status()['playing'])
        await phone.send_json({'type':'release'})
        await until(phone,'released')
        self.assertTrue(self.host.status()['paired'])
        self.assertFalse(self.host.status()['playing'])
        self.assertEqual(self.host.state,neutral())
        await phone.close()
        await asyncio.sleep(.05)
        self.assertFalse(self.host.status()['paired'])

    async def test_multi_input_watchdog_exclusive_owner_and_disconnect(self):
        first, second = await self.phone(), await self.phone()
        await first.send_json({'type':'claim'})
        await until(first, 'claimed')
        await second.send_json({'type':'claim'})
        await until(second, 'busy')
        state = dict(buttons=['A','LB','UP'], lx=.72, ly=-.5, rx=-.8, ry=.4, lt=.25, rt=1)
        await first.send_json({'type':'input','state':state})
        await first.send_json({'type':'ping'})
        await until(first, 'pong')
        self.assertEqual(set(self.host.state['buttons']), {'A','LB','UP'})
        self.assertEqual(self.host.state['lx'], .72)
        await second.send_json({'type':'input','state':{'buttons':['B']}})
        await second.send_json({'type':'ping'})
        await until(second, 'pong')
        self.assertNotIn('B', self.host.state['buttons'])
        await asyncio.sleep(.8)
        self.assertEqual(self.host.state, neutral())
        await first.send_json({'type':'input','state':state})
        await first.send_json({'type':'ping'})
        await until(first, 'pong')
        await first.close()
        await asyncio.sleep(.1)
        self.assertEqual(self.host.state, neutral())
        self.assertIsNone(self.host.owner)
        await second.send_json({'type':'claim'})
        await until(second, 'claimed')

    async def test_custom_slots_persistence_and_factory_protection(self):
        ws = await self.client.ws_connect('/ws?role=monitor')
        await until(ws, 'status')
        for slot in (2,3):
            layout = json.loads(json.dumps(self.host.builtins[slot-2]))
            layout['name'] = f'Custom {slot-1}'
            await ws.send_json({'type':'save','slot':slot,'layout':layout})
            await until(ws, 'saved')
        self.assertEqual(self.host.active,3)
        stored = Host(8765, mock=True, data_dir=self.temp.name)
        self.assertEqual([p['name'] for p in stored.custom], ['Custom 1','Custom 2'])
        self.assertEqual(stored.active,3)
        await ws.send_json({'type':'save','slot':0,'layout':self.host.builtins[1]})
        error = await until(ws, 'error')
        self.assertIn('Custom',error['message'])
        self.assertEqual(self.host.builtins[0]['name'],'Xbox')
        await ws.send_json({'type':'delete','slot':3})
        await until(ws,'saved')
        self.assertIsNone(self.host.custom[1])
        self.assertEqual(self.host.active,0)


class MappingChecks(unittest.TestCase):
    def test_playstation_two_contacts_click_and_release_wire_format(self):
        state=clean_input(dict(buttons=['A','UP','RIGHT','GUIDE'],lx=.5,ly=-.5,rt=.75,
            touchClick=True,touches=[{'id':3,'x':0,'y':0},{'id':17,'x':1,'y':1}]))
        raw=pack_ds4_report(state,5)
        self.assertEqual(len(raw),63)
        self.assertEqual(raw[4]&15,1)  # north-east
        self.assertEqual(raw[4]&0x20,0x20)  # cross
        self.assertEqual(raw[6]&3,3)  # PS + touchpad click
        self.assertEqual(raw[34:38],bytes([3,0,0,0]))
        self.assertEqual(raw[38],17)
        self.assertEqual(raw[39]|((raw[40]&15)<<8),1919)
        self.assertEqual((raw[40]>>4)|(raw[41]<<4),942)
        released=pack_ds4_report(neutral(),6)
        self.assertEqual(released[34],128)
        self.assertEqual(released[38],128)
        self.assertEqual(released[6]&3,0)
        self.assertEqual(released[:4],bytes([128]*4))

    def test_touch_layout_and_size_settings_survive_validation(self):
        import json
        layout=json.loads((Path(__file__).resolve().parents[1]/'web/presets.json').read_text(encoding='utf-8'))[1]
        layout['scale']=1.3
        layout=validate_layout(layout)
        self.assertEqual(layout['scale'],1.3)
        self.assertEqual(layout['output'],'ds4')
        self.assertEqual(next(c for c in layout['controls'] if c['type']=='touchpad')['mapping'],'touchpad')
        with self.assertRaises(ValueError):
            clean_input({'touches':[{'id':1},{'id':1}]})

    def test_gamepad_mapping_and_y_direction(self):
        class Device:
            def __init__(self): self.calls=[]
            def __getattr__(self,name):
                return lambda **kw:self.calls.append((name,kw))
        pad=Gamepad(mock=True)
        pad.device=Device()
        pad.apply(clean_input(dict(buttons=['A','LB'],lx=.4,ly=-.7,rx=-.3,ry=.8,lt=.6,rt=.9)))
        self.assertIn(('left_joystick_float',{'x_value_float':.4,'y_value_float':.7}),pad.device.calls)
        self.assertIn(('right_joystick_float',{'x_value_float':-.3,'y_value_float':-.8}),pad.device.calls)
        self.assertIn(('press_button',{'button':0x1000}),pad.device.calls)
        self.assertEqual(pad.device.calls[-1][0],'update')

    def test_input_limits_and_invalid_axis(self):
        self.assertEqual(clean_input({'lx':4,'rt':-1,'buttons':['A','unknown','A']})['buttons'],['A'])
        self.assertEqual(clean_input({'lx':4})['lx'],1)
        self.assertEqual(clean_input({'rt':-1})['rt'],0)
        with self.assertRaises(ValueError):clean_input({'lx':float('nan')})


if __name__=='__main__':
    unittest.main(verbosity=2)
