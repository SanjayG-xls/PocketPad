# Third-party notices

PocketPad is made by **Sanjay**. Its application code and interface assets are original; the phone-controller workflow was inspired by [RojX](https://rojx.io).

Ready-to-run release packages include third-party components. Their existing license and notice files are retained in the runtime, Android assets or upstream driver installer. This document is a component index, not a replacement for those licenses.

| Component | Purpose | License / upstream |
| --- | --- | --- |
| Python 3.12 | Bundled local runtime | [Python license](https://docs.python.org/3/license.html); included in `runtime/LICENSE.txt`. |
| aiohttp | HTTP and WebSocket engine | [Apache-2.0](https://github.com/aio-libs/aiohttp/blob/master/LICENSE.txt). |
| python-qrcode | Local pairing QR generation | [BSD](https://github.com/lincolnloop/python-qrcode/blob/main/LICENSE). |
| vgamepad | Xbox 360 and DS4 virtual-device interface | [MIT](https://github.com/yannbouteiller/vgamepad/blob/main/LICENSE). |
| ViGEmBus 1.22.0 | Windows virtual controller driver | [Upstream release](https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0); upstream project is archived. Its signed installer is included in release downloads. |
| ZXing Core 3.5.3 | Android QR decoding | [Apache-2.0](android/app/src/main/assets/licenses/ZXing-LICENSE.txt) and [NOTICE](android/app/src/main/assets/licenses/ZXing-NOTICE.txt). |

The exact Python dependencies are listed in [requirements-lock.txt](requirements-lock.txt). Their license metadata is kept under the bundled runtime's `Lib/site-packages` directories.

Native DS4 touch reports use the layout defined by [ViGEmClient's DS4 report structure](https://github.com/nefarius/ViGEmClient/blob/master/include/ViGEm/Common.h).

Android SDK packages and ZXing are downloaded as build prerequisites; SDK tool archives, local signing keys and build caches are excluded from this repository.

Xbox, PlayStation, DualSense, DualShock and Windows names and marks belong to their respective owners. PocketPad is an independent application and is not affiliated with Microsoft, Sony or Rockstar Games.
