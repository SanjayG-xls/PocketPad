"""Factory layouts based on the black DualSense and Xbox Series X references."""
import json
from pathlib import Path


def layout(ps=False):
    controls = []
    def add(id, type, mapping, label, x, y, w, h, **extra):
        controls.append(dict(id=id, type=type, mapping=mapping, label=label, x=x, y=y,
                             w=w, h=h, opacity=1, deadzone=.1, **extra))
    for side,x in [('l',120),('r',880)]:
        add(side+'t','trigger',side+'t',('L2' if side=='l' else 'R2') if ps else side.upper()+'T',x,36,178,48)
        add(side+'b','button',side.upper()+'B',('L1' if side=='l' else 'R1') if ps else side.upper()+'B',x,94,178,48,shape='rect')
    if ps:
        add('touchpad','touchpad','touchpad','Touchpad',500,166,274,144)
        add('back','button','BACK','Create',317,160,42,62,shape='rect')
        add('start','button','START','Options',683,160,42,62,shape='rect')
        add('dpad','dpad','dpad','D-pad',145,245,190,190)
        add('left-stick','stick','left','L',350,353,192,192)
        add('right-stick','stick','right','R',650,353,192,192)
        add('guide','button','GUIDE','PS',500,326,58,50)
        add('touch-click','button','TOUCH_CLICK','Touch click',500,259,92,30,shape='rect')
        add('l3','button','L3','L3',195,418,54,34,shape='rect')
        add('r3','button','R3','R3',805,418,54,34,shape='rect')
        add('mic-mark','label','','—',500,401,54,25)
    else:
        add('guide','button','GUIDE','Xbox',500,88,66,66)
        add('back','button','BACK','View',418,188,48,48)
        add('start','button','START','Menu',582,188,48,48)
        add('share','button','BACK','Share',500,248,46,46)
        add('left-stick','stick','left','L',170,215,196,196)
        add('dpad','dpad','dpad','D-pad',350,355,180,180)
        add('right-stick','stick','right','R',650,355,196,196)
        add('l3','button','L3','L3',165,412,54,34,shape='rect')
        add('r3','button','R3','R3',805,412,54,34,shape='rect')
    for key,x,y in [('Y',855,160),('X',773,242),('B',937,242),('A',855,324)]:
        add('face-'+key,'button',key,{'A':'×','B':'○','X':'□','Y':'△'}[key] if ps else key,x,y,84,84)
    return dict(name='PS5' if ps else 'Xbox', theme='ps5' if ps else 'xbox', accent='#687cff' if ps else '#f1f1f3',
                grid=False, scale=1, output='ds4' if ps else 'xbox', controls=controls)


if __name__ == '__main__':
    target = Path(__file__).resolve().parents[1] / 'web' / 'presets.json'
    target.write_text(json.dumps([layout(),layout(True)],ensure_ascii=False,indent=2),encoding='utf-8')
