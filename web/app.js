import {initializeExperience,offerWalkthrough,preferences,connectionState} from './experience.js';
const $ = (id) => document.getElementById(id);
const clone = (x) => structuredClone(x);
const clamp = (x, lo, hi) => Math.max(lo, Math.min(hi, x));
const esc = (s) => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const isPlayer = location.pathname === '/play';
const nativeBridge=window.PocketPadNative;
const demoMode=Boolean(nativeBridge)&&new URLSearchParams(location.search).get('demo')==='1';
const buttonNames = ['A','B','X','Y','LB','RB','BACK','START','L3','R3','GUIDE','UP','DOWN','LEFT','RIGHT','TOUCH_CLICK'];
const axisNames = ['lx','ly','rx','ry','lt','rt'];
const neutral = () => ({buttons:[],lx:0,ly:0,rx:0,ry:0,lt:0,rt:0,touches:[],touchClick:false,mouse:{dx:0,dy:0}});
const library = [
  ...[['A','A / Cross','A'],['B','B / Circle','B'],['X','X / Square','X'],['Y','Y / Triangle','Y'],['LB','Left bumper','LB'],['RB','Right bumper','RB'],['BACK','View / Create','▣'],['START','Menu / Options','☰'],['L3','Left stick click','L3'],['R3','Right stick click','R3'],['GUIDE','Guide / Home','⌂'],['TOUCH_CLICK','Touchpad click','▭'],['UP','D-pad up','↑'],['DOWN','D-pad down','↓'],['LEFT','D-pad left','←'],['RIGHT','D-pad right','→']].map(([mapping,name,glyph])=>({type:'button',mapping,name,glyph,label:mapping,w:['LB','RB'].includes(mapping)?178:84,h:['LB','RB'].includes(mapping)?48:84,shape:['LB','RB'].includes(mapping)?'rect':'round'})),
  {type:'stick',mapping:'left',name:'Left joystick',glyph:'◎',label:'L',w:192,h:192},
  {type:'stick',mapping:'right',name:'Right joystick',glyph:'◎',label:'R',w:192,h:192},
  {type:'dpad',mapping:'dpad',name:'Directional pad',glyph:'✚',label:'D-pad',w:190,h:190},
  {type:'trigger',mapping:'lt',name:'Left trigger',glyph:'LT',label:'LT',w:178,h:48},
  {type:'trigger',mapping:'rt',name:'Right trigger',glyph:'RT',label:'RT',w:178,h:48},
  {type:'touchpad',mapping:'touchpad',name:'PS touchpad',glyph:'▭',label:'Touchpad',w:274,h:144},
  {type:'slider',mapping:'lx',name:'Steering slider',glyph:'↔',label:'STEER',w:220,h:50},
  {type:'slider',mapping:'rt',name:'Throttle slider',glyph:'▰',label:'GAS',w:200,h:50},
  {type:'slider',mapping:'lt',name:'Brake slider',glyph:'▰',label:'BRAKE',w:200,h:50},
  {type:'label',mapping:'',name:'HUD text',glyph:'T',label:'PLAYER ONE',w:190,h:38},
];
let info, socket, reconnectTimer, presets=[], active=0, draft, dirty=false, selected=null, filter='all';
let undo=[], redo=[], serverOnline=false, claimed=false, playing=false, latency=null, wakeLock;
let sources=new Map(), lastState=neutral(), pingTime=0, toastTimer, ignoreLibraryClick=false;
let pendingSave=false;
let hasConnected=false,resumeWanted=false;
let motionMode='off', motionSensitivity=1, motionInput={lx:0,ly:0,rx:0,ry:0};
let motionSettings={profile:'aim',sensitivity:100,aimOnly:true,invertX:false,invertY:false},lastMotionEvent=0;
function resetMotion(){motionInput={lx:0,ly:0,rx:0,ry:0};lastMotionEvent=0;}
let phoneScale=1, touchSerial=0, pointerDelta={dx:0,dy:0}, lastTouch=new Map(), touchPulse=false;
try{phoneScale=clamp(Number(localStorage.getItem('pocketpad-phone-scale'))||1,.65,1.5);}catch{}

function icon(mapping,theme='xbox'){
  const svg=body=>`<svg viewBox="0 0 40 40" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">${body}</svg>`;
  if(theme==='ps5'&&['A','B','X','Y'].includes(mapping))return svg({A:'<path d="m9 9 22 22M31 9 9 31"/>',B:'<circle cx="20" cy="20" r="14"/>',X:'<rect x="7" y="7" width="26" height="26"/>',Y:'<path d="M20 5 36 33H4Z"/>'}[mapping]);
  if(['A','B','X','Y'].includes(mapping))return `<svg viewBox="0 0 40 40" aria-hidden="true"><text x="20" y="30" fill="currentColor" text-anchor="middle" font-family="Arial,sans-serif" font-weight="500" font-size="30">${mapping}</text></svg>`;
  if(mapping==='BACK')return theme==='ps5'?svg('<path d="M20 9v21M12 13v13M28 13v13M17 4h6"/>'):svg('<rect x="6" y="7" width="20" height="20" rx="1"/><path d="M14 27v6h20V13h-8"/>');
  if(mapping==='START')return svg('<path d="M8 11h24M8 20h24M8 29h24"/>');
  if(mapping==='GUIDE')return theme==='ps5'?'<svg viewBox="0 0 40 40" aria-hidden="true"><text x="20" y="28" text-anchor="middle" font-size="24" font-family="Arial" font-weight="600" fill="currentColor" letter-spacing="-3">PS</text></svg>':svg('<circle cx="20" cy="20" r="17" fill="currentColor" stroke="none"/><path d="M7 7q11-1 26 26M33 7Q22 6 7 33" stroke="var(--symbol-cutout,#dedee3)" stroke-width="4.5"/>');
  if(mapping==='TOUCH_CLICK')return svg('<rect x="5" y="7" width="30" height="22" rx="4"/><path d="M20 11v12m-5-5 5 5 5-5M14 34h12"/>');
  if(mapping==='SHARE')return svg('<path d="M11 20v12h22V20M22 24V5m-6 6 6-6 6 6"/>');
  return '';
}
function dpadArt(theme){
  if(theme==='ps5')return '<svg class="dpad-art" viewBox="0 0 100 100" aria-hidden="true"><g fill="#25252a" stroke="#4c4c56" stroke-width="1.2"><path d="M36 8Q50 2 64 8L60 32 50 43 40 32Z"/><path d="M92 36Q98 50 92 64L68 60 57 50 68 40Z"/><path d="M64 92Q50 98 36 92L40 68 50 57 60 68Z"/><path d="M8 64Q2 50 8 36L32 40 43 50 32 60Z"/></g><g fill="none" stroke="#8b8b97" stroke-width="1.2"><path d="m45 23 5-5 5 5m22 22 5 5-5 5M55 77l-5 5-5-5M23 55l-5-5 5-5"/></g></svg>';
  return '<svg class="dpad-art" viewBox="0 0 100 100" aria-hidden="true"><circle cx="50" cy="50" r="48" fill="#121215" stroke="#3b3b42" stroke-width="2"/><path d="M18 18 37 7H63L82 18 93 37V63L82 82 63 93H37L18 82 7 63V37Z" fill="#29292e" stroke="#41414a"/><g stroke="#0e0e11" stroke-width="1.8" fill="none"><path d="M37 7v30H7m56-30v30h30M7 63h30v30m56-30H63v30M37 37h26v26H37ZM18 18l19 19m45-19L63 37M18 82l19-19m45 19L63 63"/></g></svg>';
}

function toast(message) {
  $('toast').textContent=message; $('toast').classList.add('show');
  clearTimeout(toastTimer); toastTimer=setTimeout(()=>$('toast').classList.remove('show'),3200);
}
function send(data) {
  if(demoMode){
    if(data.type==='claim'){claimed=playing=true;$('playerOverlay').hidden=true;clearInputs();updatePlayerStatus();}
    if(data.type==='release'){claimed=playing=false;clearInputs();showPlayerOverlay('Controller preview paused.');}
    if(data.type==='activate'){active=data.slot;draft=clone(presets[active]);clearInputs();renderPlayer();renderPlayerPresets();}
    return true;
  }
  if (socket?.readyState===WebSocket.OPEN) {socket.send(JSON.stringify(data)); return true;}
  toast('The PC host is disconnected. Keep its window open.'); return false;
}
function connect() {
  clearTimeout(reconnectTimer);
  if(!hasConnected)connectionState('connecting');
  const token = new URLSearchParams(location.search).get('token')||'';
  const name = /iPhone|iPad/.test(navigator.userAgent)?'iPhone / iPad':/Android/.test(navigator.userAgent)?'Android phone':'Browser controller';
  const query = isPlayer?`role=controller&token=${encodeURIComponent(token)}&name=${encodeURIComponent(name)}`:'role=monitor';
  socket = new WebSocket(`${location.protocol==='https:'?'wss':'ws'}://${location.host}/ws?${query}`);
  socket.onopen=()=>{serverOnline=true;hasConnected=true;connectionState('ready');updateHostStatus();if(resumeWanted&&!document.hidden){resumeWanted=false;send({type:'claim'});}else if(isPlayer&&!playing)showPlayerOverlay('Connected to your PC. Tap Start controller when you’re ready.');};
  socket.onmessage=({data})=>{
    let msg; try {msg=JSON.parse(data);} catch {return;}
    if (msg.type==='config') onConfig(msg);
    if (msg.type==='status') updateStatus(msg);
    if (msg.type==='pong') {latency=Math.max(0,Math.round(performance.now()-msg.time));updatePlayerStatus();}
    if (msg.type==='claimed') {
      claimed=playing=true; $('playerOverlay').hidden=true; $('exitPlayer').textContent='Pause';
      clearInputs();connectionState('ready');acquireWakeLock();updatePlayerStatus();
    }
    if (msg.type==='released') {claimed=playing=false;resumeWanted=false;connectionState('paused');showPlayerOverlay('Paused. Tap below when you’re ready.');}
    if (msg.type==='busy') {claimed=playing=false;resumeWanted=false;connectionState('busy');showPlayerOverlay(msg.message);}
    if (msg.type==='error') {pendingSave=false;toast(msg.message);}
    if (msg.type==='saved') {if(pendingSave){pendingSave=false;toast('Layout saved. Your phone is up to date.');}else if(msg.action==='delete')toast('Custom slot cleared.');}
  };
  socket.onclose=event=>{
    resumeWanted=playing&&!/Disconnected from PC/i.test(event.reason||'')&&!document.hidden;
    serverOnline=claimed=playing=false;clearInputs(false);updateHostStatus();
    connectionState(hasConnected?'reconnecting':'connecting');
    if(isPlayer)showPlayerOverlay('Connection lost. Reconnecting… If the host restarted, scan its new QR code.');
    reconnectTimer=setTimeout(connect,2000);
  };
  socket.onerror=()=>socket.close();
}
function updateHostStatus(){
  $('hostDot').className=`dot ${serverOnline?'live':'bad'}`;
  $('footerStatus').textContent=serverOnline?`Local host online · port ${info?.port||8765}`:'Host disconnected · reconnecting…';
  $('hostState').textContent=serverOnline?'Online':'Disconnected';
  updatePlayerStatus();
}
function updatePlayerStatus(){
  $('playerStatus').textContent=demoMode?'Preview · no PC input':!serverOnline?'Reconnecting…':!claimed?'Connected to PC':`${info?.driver?(info.output==='ds4'?'PlayStation':'Xbox'):info?.mock?'Test mode':'Driver needed'}${latency===null?'':` · ${latency} ms`}`;
}
function updateStatus(status){
  if(info)Object.assign(info,status);
  $('connectionDot').className=`dot ${status.connected?'live':''}`;
  $('driverState').textContent=status.driver?`Ready · ${status.output==='ds4'?'PlayStation / DS4':'Xbox / XInput'}`:status.mock?'Test mode · no gamepad':'Driver setup needed';
  $('outputState').textContent=status.output==='ds4'?'PlayStation / DS4 · touch enabled':'Xbox 360 / XInput · mouse touchpad';
  $('driverState').style.color=status.driver?'var(--accent)':'var(--danger)';
  $('driverError').hidden=!status.driverError;
  $('driverError').textContent=status.driverError;
  $('phoneName').textContent=status.connected?(status.controllerName||'Phone connected'):'Not connected';
  $('phoneBadge').textContent=status.connected?'Connected':'Waiting for phone';
  $('monitorBadge').textContent=status.connected?'Receiving live input':'Waiting for controller';
  $('disconnectButton').disabled=!status.connected;
  if(!isPlayer)renderMonitor(status.input||neutral());
  updatePlayerStatus();
}
function onConfig(config){
  const first=!draft;
  presets=config.presets; active=config.active;
  if(isPlayer){
    draft=clone(presets[active]);clearInputs();renderPlayer(true);renderPlayerPresets();if(first)setTimeout(offerWalkthrough,160);return;
  }
  // A monitor receiving another client's update keeps unsaved work unless the active layout changes.
  const keepDraft=dirty&&!pendingSave&&draft?._slot===active;
  if(!keepDraft){draft=clone(presets[active]);draft._slot=active;dirty=false;selected=null;undo=[];redo=[];}
  if(first){
    try{const saved=JSON.parse(localStorage.getItem('pocketpad-draft'));if(saved&&saved._slot===active&&validLayout(saved)){draft=saved;dirty=true;toast('Your unsaved layout has been restored.');}}catch{}
  }
  renderAll();renderLibrary();
}
function checkpoint(){if(!draft)return;undo.push(clone(draft));if(undo.length>50)undo.shift();redo=[];}
function markDirty(){dirty=true;draft._slot=active;try{localStorage.setItem('pocketpad-draft',JSON.stringify(draft));}catch{}updateDraftUI();}
function updateDraftUI(){
  $('draftBadge').hidden=!dirty;$('layoutTitle').textContent=draft?.name||'Layout';
  $('undoButton').disabled=!undo.length;$('redoButton').disabled=!redo.length;
  $('allControlSize').value=Math.round((draft?.scale||1)*100);$('allControlSizeValue').textContent=`${Math.round((draft?.scale||1)*100)}%`;
}
function renderAll(){renderPresets();renderEditor();renderInspector();updateDraftUI();}
function renderPresets(){
  $('presets').replaceChildren();
  presets.forEach((p,i)=>{
    const b=document.createElement('button');b.className=`preset-item ${i===active?'active':''} ${!p?'empty':''}`;
    b.innerHTML=`<span class="preset-icon">${i<2?icon('GUIDE',i===1?'ps5':'xbox'):'＋'}</span><span><strong>${esc(p?.name||`Custom ${i-1}`)}</strong><small>${i<2?'Default':p?'Custom':'Empty slot'}</small></span>`;
    b.onclick=()=>{
      if(i===active&&p)return;
      if(dirty&&!confirm('Discard unsaved layout edits?'))return;
      if(!p){$('saveSlot').value=String(i);openSave(i);return;}
      localStorage.removeItem('pocketpad-draft');dirty=false;send({type:'activate',slot:i});
    };
    $('presets').append(b);
  });
}
function fitSurface(viewport,surface,player=false){
  const fit=()=>{
    if(player){
      surface.style.transform='none';
      if(draft)surface.querySelectorAll('.control').forEach(el=>{const c=draft.controls.find(c=>c.id===el.dataset.id);if(c)positionControl(el,c);});
      return;
    }
    surface.style.transform=`scale(${viewport.clientWidth/1000})`;
  };
  new ResizeObserver(fit).observe(viewport);if(player)new ResizeObserver(fit).observe($('player'));fit();
}
function applySurfaceStyle(surface){
  surface.classList.toggle('ps5',draft.theme==='ps5');surface.classList.toggle('no-grid',!draft.grid);
  surface.style.setProperty('--hud-accent',draft.accent||'#c5f36b');
}
function positionControl(el,c){
  const surface=isPlayer?$('playerSurface'):$('editorSurface');
  const w=isPlayer?surface.clientWidth:1000,h=isPlayer?surface.clientHeight:480;
  const base=isPlayer?Math.min(w/1000,h/480):1;
  const size=(draft.scale||1)*(isPlayer?phoneScale:1)*base;
  const cw=Math.min(w-8,c.w*size),ch=Math.min(h-8,c.h*size);
  el.style.left=`${clamp(c.x/1000*w,cw/2+3,w-cw/2-3)}px`;el.style.top=`${clamp(c.y/480*h,ch/2+3,h-ch/2-3)}px`;
  el.style.width=`${cw}px`;el.style.height=`${ch}px`;el.style.setProperty('--symbol-cutout',draft.theme==='ps5'?'#151519':'#dedee3');
  if(c.type==='button'||c.type==='trigger'||c.type==='label')el.style.fontSize=`${Math.min(c.type==='label'?16:24,(c.w-12)/Math.max(1,String(c.label).length)*1.6)*size}px`;
}
function makeControl(c){
  const el=document.createElement('div');
  el.className=`control ${c.type} ${c.shape==='rect'?'rect':''} ${draft.theme==='ps5'?'ps-control':'xbox-control'}`;
  el.dataset.id=c.id;el.setAttribute('aria-label',c.label||c.mapping||c.type);
  el.dataset.mapping=c.mapping;positionControl(el,c);el.style.opacity=c.opacity??1;el.style.setProperty('--control-opacity',c.opacity??1);
  if(c.color){el.style.setProperty('--ctrl-color',c.color);el.style.color=c.color;}
  if(c.type==='stick'){
    const knob=document.createElement('div');knob.className='stick-knob';const label=document.createElement('span');label.textContent=c.label;knob.append(label);el.append(knob);
  }else if(c.type==='dpad'){
    el.innerHTML=dpadArt(draft.theme);
  }else if(c.type==='touchpad'){
    el.innerHTML='<span class="touchpad-caption">TOUCHPAD</span>';
  }else{
    if(c.type==='trigger'||c.type==='slider'){const fill=document.createElement('div');fill.className='control-fill';el.append(fill);}
    const defaults=['A','B','X','Y','×','○','□','△','BACK','START','GUIDE','View','Create','Share','Menu','Options','PS','Xbox','TOUCH_CLICK','Touch click'];
    const glyph=c.type==='button'&&defaults.includes(c.label)?icon(c.id==='share'?'SHARE':c.mapping,draft.theme):'';
    if(glyph){const label=document.createElement('span');label.className='control-icon';label.innerHTML=glyph;el.append(label);}else{const label=document.createElement('span');label.className='control-text';label.textContent=c.label;el.append(label);}
    if(['A','B','X','Y'].includes(c.mapping))el.classList.add('face');
    if(['BACK','START'].includes(c.mapping))el.classList.add('utility');
    if(c.mapping==='GUIDE')el.classList.add('home');
    if(c.mapping==='TOUCH_CLICK')el.classList.add('touch-click');
    if(['LB','RB'].includes(c.mapping))el.classList.add('shoulder');
  }
  return el;
}
function attachResize(el,c){
  if(el.querySelector('.resize-handle'))return;
  const handle=document.createElement('span');handle.className='resize-handle';handle.title='Drag to resize';el.append(handle);
  handle.onpointerdown=e=>{
    e.preventDefault();e.stopPropagation();checkpoint();
    const start={x:e.clientX,y:e.clientY,w:c.w,h:c.h};
    const scale=$('editorSurface').getBoundingClientRect().width/1000*(draft.scale||1);
    handle.setPointerCapture(e.pointerId);
    handle.onpointermove=ev=>{if(!handle.hasPointerCapture(ev.pointerId))return;c.w=clamp(Math.round(start.w+(ev.clientX-start.x)/scale),24,600);c.h=clamp(Math.round(start.h+(ev.clientY-start.y)/scale),24,460);c.x=clamp(c.x,c.w/2,1000-c.w/2);c.y=clamp(c.y,c.h/2,480-c.h/2);positionControl(el,c);};
    const end=()=>{handle.onpointermove=null;markDirty();renderInspector();};handle.onpointerup=end;handle.onpointercancel=end;
  };
}
function renderEditor(){
  const surface=$('editorSurface');surface.replaceChildren();applySurfaceStyle(surface);
  for(const c of draft.controls){
    const el=makeControl(c);if(c.id===selected){el.classList.add('selected');attachResize(el,c);}
    el.onpointerdown=e=>{
      if(e.button!==0)return;e.preventDefault();e.stopPropagation();
      selected=c.id;surface.querySelectorAll('.selected').forEach(n=>n.classList.remove('selected'));el.classList.add('selected');renderInspector();
      surface.querySelectorAll('.resize-handle').forEach(n=>n.remove());attachResize(el,c);
      const rect=surface.getBoundingClientRect(),scale=rect.width/1000;
      const origin={px:e.clientX,py:e.clientY,x:c.x,y:c.y};let moved=false;
      checkpoint();el.setPointerCapture(e.pointerId);
      el.onpointermove=ev=>{
        if(!el.hasPointerCapture(ev.pointerId))return;
        let x=origin.x+(ev.clientX-origin.px)/scale,y=origin.y+(ev.clientY-origin.py)/scale;
        if($('snapGrid').checked){x=Math.round(x/10)*10;y=Math.round(y/10)*10;}
        c.x=clamp(x,c.w/2,1000-c.w/2);c.y=clamp(y,c.h/2,480-c.h/2);
        positionControl(el,c);moved=true;
      };
      const end=()=>{el.onpointermove=null;el.onpointerup=null;el.onpointercancel=null;if(moved){markDirty();renderInspector();}else{undo.pop();updateDraftUI();}};
      el.onpointerup=end;el.onpointercancel=end;
    };
    surface.append(el);
  }
}
function renderLibrary(){
  const search=$('librarySearch').value.toLowerCase();$('library').replaceChildren();
  const shown=library.filter(c=>(filter==='all'||(filter==='axis'?['stick','trigger','slider','dpad','touchpad'].includes(c.type):c.type===filter))&&`${c.name} ${c.mapping}`.toLowerCase().includes(search));
  $('libraryCount').textContent=`${shown.length} controls`;
  for(const c of shown){
    const b=document.createElement('button');b.className='library-item';b.draggable=true;b.title=`Add ${c.name}`;
    b.innerHTML=`<span class="lib-glyph ${['trigger','slider','label','touchpad'].includes(c.type)?'square':''}">${icon(c.mapping,draft?.theme)||esc(c.glyph)}</span><small>${esc(c.name)}</small>`;
    b.ondragstart=e=>{e.dataTransfer.setData('text/plain',String(library.indexOf(c)));e.dataTransfer.effectAllowed='copy';};
    b.onclick=()=>{if(ignoreLibraryClick){ignoreLibraryClick=false;return;}addControl(c);};
    b.onpointerdown=e=>{
      if(e.pointerType==='mouse')return;
      const start={x:e.clientX,y:e.clientY};let moved=false;
      const move=ev=>{if(Math.hypot(ev.clientX-start.x,ev.clientY-start.y)>12)moved=true;};
      const up=ev=>{
        window.removeEventListener('pointermove',move);window.removeEventListener('pointerup',up);
        if(moved){ignoreLibraryClick=true;const r=$('editorSurface').getBoundingClientRect();if(ev.clientX>=r.left&&ev.clientX<=r.right&&ev.clientY>=r.top&&ev.clientY<=r.bottom)addControl(c,(ev.clientX-r.left)*1000/r.width,(ev.clientY-r.top)*480/r.height);}
      };
      window.addEventListener('pointermove',move);window.addEventListener('pointerup',up,{once:true});
    };
    $('library').append(b);
  }
}
function addControl(template,x=500,y=240){
  if(!draft||draft.controls.length>=100){toast('Use up to 100 controls per layout.');return;}
  checkpoint();
  const {name,glyph,...data}=template;
  const c={...data,id:`c-${Date.now()}-${Math.random().toString(36).slice(2,7)}`,x:clamp(x,data.w/2,1000-data.w/2),y:clamp(y,data.h/2,480-data.h/2),opacity:1,deadzone:.1};
  draft.controls.push(c);selected=c.id;markDirty();renderEditor();renderInspector();
}
function field(label,id,type,value,attrs=''){
  return `<label class="field">${label}<input id="${id}" type="${type}" value="${esc(value)}" ${attrs}></label>`;
}
function selectField(label,id,options,value){
  return `<label class="field">${label}<select id="${id}">${options.map(([v,n])=>`<option value="${esc(v)}" ${v===value?'selected':''}>${esc(n)}</option>`).join('')}</select></label>`;
}
function renderInspector(){
  if(!draft)return;
  const c=draft.controls.find(c=>c.id===selected),panel=$('inspector');
  $('inspectorTitle').textContent=c?'Control settings':'Layout settings';$('selectedType').textContent=c?c.type.toUpperCase():'HUD';
  if(!c){
    panel.innerHTML=`<p class="inspector-description">Select a control to change its size, position or mapping.</p>${field('Layout name','layoutName','text',draft.name,'maxlength="40"')}${selectField('Button style','layoutTheme',[['xbox','Xbox Series X · Black'],['ps5','PS5 · Black'],['neon','Custom · Dark']],draft.theme)}${selectField('PC controller output','layoutOutput',[['xbox','Xbox / XInput'],['ds4','PlayStation / DS4']],draft.output||'xbox')}<p class="hint">PlayStation output sends native two-finger touch and click. Xbox output uses the touchpad as a mouse. Choose output before launching your game.</p>${field('Accent color','layoutAccent','color',draft.accent||'#ededf0')}<label class="toggle-label"><input id="hudGrid" type="checkbox" ${draft.grid?'checked':''}> Background grid</label><hr class="inspector-divider"><div class="inspector-actions"><button id="clearLayout" class="quiet">Clear canvas</button>${active>1?'<button id="deleteSlot" class="quiet danger">Delete slot</button>':''}</div><div class="empty-selection"><p>Use the slider below the canvas to scale all controls. Resize individual controls from their corner handle.</p></div>`;
    const change=(id,key,read=el=>el.value)=>$(id).onchange=()=>{checkpoint();draft[key]=read($(id));markDirty();renderEditor();};
    change('layoutName','name');change('layoutTheme','theme');change('layoutOutput','output');change('layoutAccent','accent');change('hudGrid','grid',el=>el.checked);
    $('clearLayout').onclick=()=>{if(confirm('Remove every control from this draft?')){checkpoint();draft.controls=[];markDirty();renderEditor();}};
    if($('deleteSlot'))$('deleteSlot').onclick=()=>{if(confirm('Delete this saved custom layout?')){dirty=false;localStorage.removeItem('pocketpad-draft');send({type:'delete',slot:active});}};
    return;
  }
  const mappings=c.type==='button'?buttonNames.map(v=>[v,v]):c.type==='stick'?[['left','Left stick'],['right','Right stick']]:c.type==='dpad'?[['dpad','Directional pad']]:c.type==='trigger'?[['lt','Left trigger'],['rt','Right trigger']]:c.type==='slider'?axisNames.map(v=>[v,v.toUpperCase()]):c.type==='touchpad'?[['touchpad','Two-finger touch surface']]:[['','Display only']];
  panel.innerHTML=`${field('Label','controlLabel','text',c.label,'maxlength="30"')}${selectField('Gamepad mapping','controlMapping',mappings,c.mapping)}<div class="field-row">${field('Position X','controlX','number',Math.round(c.x),'min="0" max="1000"')}${field('Position Y','controlY','number',Math.round(c.y),'min="0" max="480"')}</div><div class="field-row">${field('Width','controlW','number',c.w,'min="24" max="400"')}${field('Height','controlH','number',c.h,'min="24" max="400"')}</div>${c.type==='button'?selectField('Button shape','controlShape',[['round','Round'],['rect','Rounded rectangle']],c.shape||'round'):''}${field('Control color','controlColor','color',c.color||draft.accent||'#c5f36b')}${field(`Opacity · ${Math.round((c.opacity??1)*100)}%`,'controlOpacity','range',c.opacity??1,'min="0.15" max="1" step="0.05"')}${c.type==='stick'?field(`Deadzone · ${Math.round((c.deadzone??.1)*100)}%`,'controlDeadzone','range',c.deadzone??.1,'min="0" max="0.5" step="0.01"'):''}<hr class="inspector-divider"><div class="inspector-actions"><button class="quiet" id="duplicateControl">Duplicate</button><button class="quiet danger" id="deleteControl">Delete</button><button class="quiet" id="deselectControl">Done</button></div>`;
  const bind=(id,key,numeric=false)=>{if(!$(id))return;$(id).onchange=()=>{
    let value=numeric?Number($(id).value):$(id).value;if(numeric&&!Number.isFinite(value))return;
    checkpoint();c[key]=value;
    c.w=clamp(c.w,24,600);c.h=clamp(c.h,24,460);c.x=clamp(c.x,c.w/2,1000-c.w/2);c.y=clamp(c.y,c.h/2,480-c.h/2);
    markDirty();renderEditor();renderInspector();
  };};
  for(const [id,key,num] of [['controlLabel','label'],['controlMapping','mapping'],['controlX','x',true],['controlY','y',true],['controlW','w',true],['controlH','h',true],['controlShape','shape'],['controlColor','color'],['controlOpacity','opacity',true],['controlDeadzone','deadzone',true]])bind(id,key,num);
  $('controlW').max='600';$('controlH').max='460';
  const sizing=document.createElement('label');sizing.className='field';sizing.innerHTML=`Size <output id="controlSizeValue">${c.w} × ${c.h}</output><input id="controlSize" type="range" min="50" max="175" value="100" aria-label="Control size">`;
  panel.insertBefore(sizing,panel.querySelector('.field-row'));
  const base={w:c.w,h:c.h};let changing=false;
  $('controlSize').oninput=()=>{if(!changing){checkpoint();changing=true;}const scale=Number($('controlSize').value)/100;c.w=clamp(Math.round(base.w*scale),24,600);c.h=clamp(Math.round(base.h*scale),24,460);$('controlSizeValue').textContent=`${c.w} × ${c.h}`;$('controlW').value=c.w;$('controlH').value=c.h;markDirty();renderEditor();};
  $('controlSize').onchange=()=>{changing=false;renderInspector();};
  if(c.type==='touchpad'){const note=document.createElement('p');note.className='hint';note.textContent='One or two fingers to track. Two-finger tap to click, or add the separate Touchpad click button.';panel.prepend(note);}
  $('deleteControl').onclick=deleteSelected;
  $('duplicateControl').onclick=()=>addControl(c,c.x+30,c.y+30);
  $('deselectControl').onclick=()=>{selected=null;renderEditor();renderInspector();};
}
function deleteSelected(){if(!selected)return;checkpoint();draft.controls=draft.controls.filter(c=>c.id!==selected);selected=null;markDirty();renderEditor();renderInspector();}
function travelHistory(back){
  const from=back?undo:redo,to=back?redo:undo;if(!from.length)return;to.push(clone(draft));draft=from.pop();selected=null;markDirty();renderEditor();renderInspector();
}
function openSave(slot=active>1?active:presets[2]?3:2){
  if(!draft)return;$('presetName').value=active<2?`${draft.name} custom`:draft.name;$('saveSlot').value=String(slot);
  updateSaveWarning();$('saveDialog').showModal();
}
function updateSaveWarning(){const p=presets[Number($('saveSlot').value)];$('slotWarning').textContent=p?`This will replace “${p.name}”.`:'This slot is empty and ready for your layout.';}
function validLayout(layout){
  return layout&&Array.isArray(layout.controls)&&layout.controls.length<=100&&layout.controls.every(c=>['button','stick','dpad','trigger','slider','label','touchpad'].includes(c.type)&&['x','y','w','h'].every(k=>typeof c[k]==='number'&&Number.isFinite(c[k]))&&c.w>=24&&c.w<=600&&c.h>=24&&c.h<=460&&c.x>=0&&c.x<=1000&&c.y>=0&&c.y<=480&&typeof c.id==='string');
}
function setupEditor(){
  document.querySelectorAll('[data-tab]').forEach(b=>b.onclick=()=>switchTab(b.dataset.tab));
  $('pairButton').onclick=()=>switchTab('connect');
  $('layoutSettingsButton').onclick=()=>{selected=null;renderEditor();renderInspector();};
  let scaling=false;
  $('allControlSize').oninput=()=>{if(!draft)return;if(!scaling){checkpoint();scaling=true;}draft.scale=Number($('allControlSize').value)/100;markDirty();renderEditor();};
  $('allControlSize').onchange=()=>scaling=false;
  document.querySelectorAll('[data-filter]').forEach(b=>b.onclick=()=>{filter=b.dataset.filter;document.querySelectorAll('[data-filter]').forEach(x=>x.classList.toggle('active',x===b));renderLibrary();});
  $('librarySearch').oninput=renderLibrary;renderLibrary();
  $('editorSurface').onpointerdown=e=>{if(e.target===$('editorSurface')){selected=null;renderEditor();renderInspector();}};
  $('editorViewport').ondragover=e=>{e.preventDefault();e.dataTransfer.dropEffect='copy';};
  $('editorViewport').ondrop=e=>{e.preventDefault();const i=Number(e.dataTransfer.getData('text/plain'));const r=$('editorSurface').getBoundingClientRect();if(library[i])addControl(library[i],(e.clientX-r.left)*1000/r.width,(e.clientY-r.top)*480/r.height);};
  $('undoButton').onclick=()=>travelHistory(true);$('redoButton').onclick=()=>travelHistory(false);
  $('resetButton').onclick=()=>{if(!draft)return;if(dirty&&!confirm('Reset this layout to its saved version?'))return;checkpoint();draft=clone(presets[active]);selected=null;markDirty();renderAll();};
  $('saveButton').onclick=()=>openSave();$('cancelSave').onclick=()=>$('saveDialog').close();$('saveSlot').onchange=updateSaveWarning;
  $('saveForm').onsubmit=e=>{
    e.preventDefault();const layout=clone(draft);layout.name=$('presetName').value.trim()||'Custom layout';delete layout._slot;
    pendingSave=true;
    if(send({type:'save',slot:Number($('saveSlot').value),layout})){localStorage.removeItem('pocketpad-draft');$('saveDialog').close();}else pendingSave=false;
  };
  $('exportButton').onclick=()=>{if(!draft)return;const data=clone(draft);delete data._slot;const url=URL.createObjectURL(new Blob([JSON.stringify(data,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download=`pocketpad-${draft.name.replace(/[^a-z0-9]/gi,'-')}.json`;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);};
  $('importButton').onclick=()=>$('importFile').click();
  $('importFile').onchange=async()=>{
    const f=$('importFile').files[0];if(!f)return;
    try{if(f.size>128*1024)throw Error();const layout=JSON.parse(await f.text());if(!validLayout(layout))throw Error();checkpoint();draft=layout;selected=null;markDirty();renderAll();toast('Layout imported. Save it to a custom slot.');}catch{toast('That file is not a valid PocketPad layout.');}
    $('importFile').value='';
  };
  $('phonePreview').onclick=()=>{if(dirty)toast('Preview uses the saved layout. Save your edits to test them.');window.open(`http://${location.host}/play?token=${info.token}`,'_blank');};
  $('disconnectButton').onclick=()=>send({type:'disconnect'});
  $('copyUrl').onclick=async()=>{try{await navigator.clipboard.writeText($('pairUrl').value);toast('Pairing link copied.');}catch{$('pairUrl').select();document.execCommand('copy');toast('Pairing link selected.');}};
  window.addEventListener('keydown',e=>{
    if(['INPUT','SELECT','TEXTAREA'].includes(document.activeElement.tagName)||$('saveDialog').open)return;
    if(e.key==='Delete'||e.key==='Backspace'){e.preventDefault();deleteSelected();}
    if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='z'){e.preventDefault();travelHistory(!e.shiftKey);}
    if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='y'){e.preventDefault();travelHistory(false);}
    if(e.key==='Escape'){selected=null;renderEditor();renderInspector();}
  });
  fitSurface($('editorViewport'),$('editorSurface'));
  $('monitorButtons').innerHTML=buttonNames.map(name=>`<span id="mon-${name}" class="monitor-key">${name}</span>`).join('');
  $('monitorAxes').innerHTML=axisNames.map(axis=>`<div class="axis-row"><label>${axis.toUpperCase()}</label><div class="axis-track"><span id="axis-${axis}"></span></div><output id="value-${axis}">0.00</output></div>`).join('');
  const touch=document.createElement('div');touch.id='touchMonitor';touch.className='touch-monitor';$('monitorAxes').after(touch);
}
function switchTab(tab){
  document.querySelectorAll('[data-tab]').forEach(b=>b.classList.toggle('active',b.dataset.tab===tab));
  for(const t of ['editor','connect','monitor'])$(t+'Tab').hidden=t!==tab;
}
function setupPairing(){
  const select=$('networkSelect');select.replaceChildren();
  info.urls.forEach(url=>{const option=document.createElement('option');option.value=url;option.textContent=new URL(url).hostname;select.append(option);});
  select.onchange=()=>{let url=select.value;const ip=new URL(url).hostname;const app=$('qrPurpose').value==='android';if(app)url=new URL('/android',url).href;$('qrImage').src=`/api/qr.svg?host=${encodeURIComponent(ip)}${app?'&target=android':''}`;$('pairUrl').value=url;$('localPlayLink').href=app?'/android':`/play?token=${info.token}`;$('localPlayLink').textContent=app?'Open Android download page ↗':'Open controller on this PC ↗';};
  $('qrPurpose').onchange=select.onchange;
  select.onchange();
}
function renderMonitor(state){
  for(const name of buttonNames)$('mon-'+name)?.classList.toggle('on',name==='TOUCH_CLICK'?state.touchClick:state.buttons.includes(name));
  if($('touchMonitor'))$('touchMonitor').textContent=`Touchpad: ${(state.touches||[]).length} / 2 fingers · Click: ${state.touchClick?'pressed':'released'}${state.touches?.length?' · '+state.touches.map(t=>`#${t.id} (${t.x.toFixed(2)}, ${t.y.toFixed(2)})`).join(' / '):''}`;
  for(const name of axisNames){const v=state[name]||0,trigger=name==='lt'||name==='rt';const el=$('axis-'+name);if(!el)continue;el.style.left=trigger?'0%':v<0?`${50+v*50}%`:'50%';el.style.width=`${Math.abs(v)*(trigger?100:50)}%`;$('value-'+name).textContent=v.toFixed(2);}
}

// Phone controls send complete snapshots, so dropping a packet cannot leave a button held.
function mergedInput(){
  const out=neutral(),buttons=new Set();
  for(const s of sources.values()){
    for(const b of s.buttons||[])buttons.add(b);
    for(const key of axisNames){const value=s[key]||0;if(Math.abs(value)>Math.abs(out[key]))out[key]=value;}
    if(s.touch&&out.touches.length<2)out.touches.push(s.touch);
  }
  out.touchClick=touchPulse||buttons.has('TOUCH_CLICK');buttons.delete('TOUCH_CLICK');
  if(motionMode!=='off'&&playing&&performance.now()-lastMotionEvent<160){
    const aiming=motionMode==='aim';
    const allowed=!aiming||(!motionSettings.aimOnly||out.lt>.2);
    // A thumb on the camera stick takes priority over gyro, so the two never fight.
    const manual=aiming&&(Math.abs(out.rx)>.08||Math.abs(out.ry)>.08);
    if(allowed&&!manual)for(const key of ['lx','ly','rx','ry'])out[key]=clamp(out[key]+motionInput[key],-1,1);
  }
  out.mouse={...pointerDelta};out.buttons=[...buttons].sort();return out;
}
function transmit(){
  lastState=mergedInput();
  if(claimed&&socket?.readyState===WebSocket.OPEN){socket.send(JSON.stringify({type:'input',state:lastState}));pointerDelta={dx:0,dy:0};}
  $('playerSurface').querySelectorAll('.touchpad').forEach(el=>el.classList.toggle('clicked',lastState.touchClick));
}
function clearInputs(transmitNow=true){
  sources.clear();lastTouch.clear();pointerDelta={dx:0,dy:0};touchPulse=false;resetMotion();lastState=neutral();
  $('playerSurface').querySelectorAll('.touch-marker').forEach(el=>el.remove());
  $('playerSurface').querySelectorAll('.control').forEach(el=>{el.classList.remove('pressed');const knob=el.querySelector('.stick-knob');if(knob)knob.style.transform='';const fill=el.querySelector('.control-fill');if(fill){if(el.classList.contains('slider'))fill.style.width='0%';else fill.style.height='0%';}});
  if(transmitNow)transmit();
}
function bindTouchpad(el){
  const fingers=new Map();let gesture=null;
  const draw=()=>{
    el.querySelectorAll('.touch-marker').forEach(n=>n.remove());
    for(const pointer of fingers.keys()){
      const touch=sources.get(pointer)?.touch;if(!touch)continue;
      const marker=document.createElement('span');marker.className='touch-marker';marker.style.left=`${touch.x*100}%`;marker.style.top=`${touch.y*100}%`;el.append(marker);
    }
    el.classList.toggle('pressed',fingers.size>0);
  };
  const update=e=>{
    const finger=fingers.get(e.pointerId);if(!finger)return;
    const rect=el.getBoundingClientRect();
    const x=clamp((e.clientX-rect.left)/rect.width,0,1),y=clamp((e.clientY-rect.top)/rect.height,0,1);
    if(finger.last&&[...fingers.keys()][0]===e.pointerId){pointerDelta.dx+=(e.clientX-finger.last.x)*1.6;pointerDelta.dy+=(e.clientY-finger.last.y)*1.6;}
    if(Math.hypot(e.clientX-finger.start.x,e.clientY-finger.start.y)>12&&gesture)gesture.moved=true;
    finger.last={x:e.clientX,y:e.clientY};sources.set(e.pointerId,{buttons:[],touch:{id:finger.id,x,y}});draw();transmit();
  };
  el.onpointerdown=e=>{
    for(const id of fingers.keys())if(!sources.has(id))fingers.delete(id);
    if(!playing||!claimed||fingers.size>=2)return;e.preventDefault();
    if(!fingers.size)gesture={time:performance.now(),max:0,moved:false};
    fingers.set(e.pointerId,{id:(touchSerial++)%128,start:{x:e.clientX,y:e.clientY},last:null});
    gesture.max=Math.max(gesture.max,fingers.size);el.setPointerCapture(e.pointerId);update(e);
  };
  el.onpointermove=e=>{if(fingers.has(e.pointerId)&&playing)update(e);};
  const release=e=>{
    if(!fingers.has(e.pointerId))return;
    fingers.delete(e.pointerId);sources.delete(e.pointerId);
    if(!fingers.size&&gesture){
      if(e.type!=='pointercancel'&&gesture.max===2&&!gesture.moved&&performance.now()-gesture.time<320){touchPulse=true;setTimeout(()=>{touchPulse=false;transmit();},110);}
      gesture=null;
    }
    draw();transmit();
  };
  el.onpointerup=release;el.onpointercancel=release;el.onlostpointercapture=release;
  el.oncontextmenu=e=>e.preventDefault();
}
function renderPlayer(animate=false){
  if(!draft)return;const surface=$('playerSurface');surface.replaceChildren();applySurfaceStyle(surface);
  let entryIndex=0;
  for(const c of draft.controls){
    const el=makeControl(c);surface.append(el);if(c.type==='label')continue;
    if(animate&&preferences.animations){el.classList.add('control-arrive');el.style.setProperty('--entry-delay',Math.min(entryIndex++*24,240)+'ms');}
    if(c.type==='touchpad'){bindTouchpad(el);continue;}
    const update=e=>{
      const rect=el.getBoundingClientRect();const x=clamp((e.clientX-rect.left)/rect.width,0,1),y=clamp((e.clientY-rect.top)/rect.height,0,1);
      let s={buttons:[]};
      if(c.type==='button')s.buttons=[c.mapping];
      if(c.type==='trigger'){const value=clamp(1-y,0,1);s[c.mapping]=value;el.querySelector('.control-fill').style.height=`${value*100}%`;}
      if(c.type==='slider'){const value=['lt','rt'].includes(c.mapping)?x:x*2-1;s[c.mapping]=value;el.querySelector('.control-fill').style.width=`${x*100}%`;}
      if(c.type==='stick'){
        let dx=(x-.5)*2,dy=(y-.5)*2;const length=Math.hypot(dx,dy);if(length>1){dx/=length;dy/=length;}
        const deadzone=c.deadzone??.1,mag=Math.hypot(dx,dy);
        if(mag<=deadzone){dx=dy=0;}else{const scaled=(mag-deadzone)/(1-deadzone);dx=dx/mag*scaled;dy=dy/mag*scaled;}
        s[c.mapping==='left'?'lx':'rx']=dx;s[c.mapping==='left'?'ly':'ry']=dy;
        el.querySelector('.stick-knob').style.transform=`translate(${dx*rect.width*.15}px,${dy*rect.height*.15}px)`;
      }
      if(c.type==='dpad'){
        const dx=x-.5,dy=y-.5;
        if(Math.abs(dx)>.17)s.buttons.push(dx<0?'LEFT':'RIGHT');
        if(Math.abs(dy)>.17)s.buttons.push(dy<0?'UP':'DOWN');
      }
      sources.set(e.pointerId,s);el.classList.add('pressed');transmit();
    };
    el.onpointerdown=e=>{if(!playing||!claimed)return;e.preventDefault();el.setPointerCapture(e.pointerId);update(e);};
    el.onpointermove=e=>{if(!playing||!el.hasPointerCapture(e.pointerId)||!sources.has(e.pointerId))return;update(e);};
    const release=e=>{
      sources.delete(e.pointerId);el.classList.remove('pressed');const knob=el.querySelector('.stick-knob');if(knob)knob.style.transform='';
      const fill=el.querySelector('.control-fill');if(fill){if(c.type==='slider')fill.style.width=['lt','rt'].includes(c.mapping)?'0%':'50%';else fill.style.height='0%';}
      transmit();
    };
    el.onpointerup=release;el.onpointercancel=release;el.onlostpointercapture=release;
    el.oncontextmenu=e=>e.preventDefault();
  }
}
function showPlayerOverlay(message){
  $('playerMessage').textContent=message;$('playerOverlay').hidden=false;$('exitPlayer').textContent='Resume';$('startPlaying').textContent='Start controller';updatePlayerStatus();
}
async function acquireWakeLock(){try{wakeLock=await navigator.wakeLock?.request('screen');}catch{}}
async function enterFullscreen(){
  if(nativeBridge){nativeBridge.fullscreen();return;}
  try{if(!document.fullscreenElement)await document.documentElement.requestFullscreen();await screen.orientation?.lock?.('landscape');}catch{}
}
function renderPlayerPresets(){
  $('playerPresetMenu').replaceChildren();presets.forEach((p,i)=>{if(!p)return;const b=document.createElement('button');b.textContent=p.name;b.className=i===active?'active':'';b.onclick=()=>{clearInputs();send({type:'activate',slot:i});$('playerPresetMenu').hidden=true;};$('playerPresetMenu').append(b);});
}
function setupPlayer(){
  $('studio').hidden=true;$('player').hidden=false;document.body.style.overflow='hidden';
  initializeExperience();
  $('startPlaying').onclick=()=>{
    if(!serverOnline){toast('Waiting for the PC host. If it restarted, scan the new QR code.');return;}
    resumeWanted=false;clearInputs();send({type:'claim'});enterFullscreen();
  };
  $('exitPlayer').onclick=()=>{
    if(playing){resumeWanted=false;clearInputs();send({type:'release'});wakeLock?.release();}else $('startPlaying').click();
  };
  $('fullscreenButton').onclick=()=>{if(document.fullscreenElement)document.exitFullscreen();else enterFullscreen();};
  $('playerPresetButton').onclick=()=>$('playerPresetMenu').hidden=!$('playerPresetMenu').hidden;
  $('playerSizeButton').onclick=()=>{$('playerSizeMenu').hidden=!$('playerSizeMenu').hidden;$('playerPresetMenu').hidden=true;};
  $('phoneControlSize').value=Math.round(phoneScale*100);$('phoneControlSizeValue').textContent=`${Math.round(phoneScale*100)}%`;
  $('phoneControlSize').oninput=()=>{clearInputs();phoneScale=Number($('phoneControlSize').value)/100;$('phoneControlSizeValue').textContent=`${Math.round(phoneScale*100)}%`;try{localStorage.setItem('pocketpad-phone-scale',phoneScale);}catch{}renderPlayer();};
  $('phoneSizeReset').onclick=()=>{$('phoneControlSize').value=100;$('phoneControlSize').oninput();};
  if(nativeBridge){
    document.querySelector('.player-brand').onclick=e=>{e.preventDefault();clearInputs();if(claimed)send({type:'release'});nativeBridge.home();};
    $('playerMotionButton').hidden=false;
    $('playerMotionButton').onclick=()=>{$('playerMotionMenu').hidden=!$('playerMotionMenu').hidden;$('playerPresetMenu').hidden=true;$('playerSizeMenu').hidden=true;};
    const available=nativeBridge.hasMotion();
    try{const saved=nativeBridge.getMotionPreferences?nativeBridge.getMotionPreferences():localStorage.getItem('pocketpad-gyro');if(saved)Object.assign(motionSettings,JSON.parse(saved));}catch{}
    motionSettings.profile=motionSettings.profile==='steer'?'steer':'aim';
    motionSettings.sensitivity=clamp(Number(motionSettings.sensitivity)||100,25,200);
    motionSensitivity=motionSettings.sensitivity/100;
    motionMode=available?nativeBridge.getMotionMode():'off';
    if(!['aim','steer'].includes(motionMode))motionMode='off';else motionSettings.profile=motionMode;
    const saveMotion=()=>{const json=JSON.stringify(motionSettings);try{localStorage.setItem('pocketpad-gyro',json);}catch{}if(nativeBridge.saveMotionPreferences)nativeBridge.saveMotionPreferences(json);};
    const refreshMotion=()=>{
      const enabled=motionMode!=='off';
      $('motionEnabled').checked=enabled;$('motionEnabled').disabled=!available;
      $('playerGyroToggle').disabled=!available;$('playerGyroToggle').textContent=enabled?'Gyro on':'Gyro off';
      $('playerGyroToggle').setAttribute('aria-pressed',String(enabled));
      $('motionMode').value=motionMode;$('motionMode').disabled=!available;
      $('motionSensitivity').value=motionSettings.sensitivity;$('motionSensitivityValue').textContent=motionSettings.sensitivity+'%';
      $('motionAimOnly').checked=!!motionSettings.aimOnly;$('motionAimOnly').disabled=motionSettings.profile==='steer';
      $('motionInvertX').checked=!!motionSettings.invertX;$('motionInvertY').checked=!!motionSettings.invertY;
      $('motionHint').textContent=!available?'A rotation sensor is not available on this device.':motionSettings.profile==='steer'?'Tilt left or right to steer. Hold the phone comfortably, then recenter.':'GTA V: use the right stick for large turns, move the phone for fine aim. '+(motionSettings.aimOnly?'Hold LT / L2 to activate gyro.':'Gyro controls the camera whenever enabled.');
    };
    const setMotion=mode=>{motionMode=available?mode:'off';if(motionMode!=='off')motionSettings.profile=motionMode;resetMotion();nativeBridge.setMotion(motionMode);nativeBridge.recenter();saveMotion();refreshMotion();transmit();if(preferences.haptics)nativeBridge.clickHaptic();};
    $('playerGyroToggle').hidden=false;
    $('playerGyroToggle').onclick=()=>{setMotion(motionMode==='off'?motionSettings.profile:'off');toast(motionMode==='off'?'Gyro off.':motionSettings.profile==='aim'?'Gyro aiming on.':'Tilt steering on.');};
    $('motionEnabled').onchange=()=>setMotion($('motionEnabled').checked?motionSettings.profile:'off');
    $('motionMode').onchange=()=>setMotion($('motionMode').value);
    $('motionSensitivity').oninput=()=>{motionSettings.sensitivity=Number($('motionSensitivity').value);motionSensitivity=motionSettings.sensitivity/100;resetMotion();saveMotion();refreshMotion();};
    for(const [id,key] of [['motionAimOnly','aimOnly'],['motionInvertX','invertX'],['motionInvertY','invertY']])$(id).onchange=()=>{motionSettings[key]=$(id).checked;resetMotion();saveMotion();refreshMotion();transmit();};
    $('motionRecenter').onclick=()=>{resetMotion();nativeBridge.recenter();transmit();toast('Gyro centered.');};
    refreshMotion();
    window.addEventListener('pocketpad-motion',e=>{
      if(motionMode==='off'||!playing)return;
      lastMotionEvent=performance.now();
      const roll=clamp(Number(e.detail?.roll)||0,-180,180),pitch=clamp(Number(e.detail?.pitch)||0,-180,180);
      const deadzone=v=>Math.abs(v)<.035?0:clamp(v,-1,1);
      const x=deadzone(roll/30*motionSensitivity),y=deadzone(pitch/30*motionSensitivity);
      const target={lx:0,ly:0,rx:0,ry:0};
      if(motionMode==='steer')target.lx=x*(motionSettings.invertX?-1:1);
      else{
        // Native 1.2 supplies angular velocity: stationary phone means neutral camera.
        // Older companion versions retain tilt aiming until their APK is updated.
        const rate=Number.isFinite(e.detail?.yawRate)&&Number.isFinite(e.detail?.pitchRate);
        const response=v=>Math.abs(v)<.012?0:clamp(v/1.8*motionSensitivity,-1,1);
        target.rx=(rate?response(-e.detail.yawRate):x)*(motionSettings.invertX?-1:1);
        target.ry=(rate?response(-e.detail.pitchRate):y)*(motionSettings.invertY?-1:1);
      }
      for(const key of Object.keys(target))motionInput[key]=target[key]===0?0:motionInput[key]+(target[key]-motionInput[key])*.45;
    });
    window.addEventListener('pointerdown',e=>{if(playing&&preferences.haptics&&e.target.closest('.control'))nativeBridge.clickHaptic();},{passive:true});
  }
  window.addEventListener('pocketpad-pause',()=>{resumeWanted=false;clearInputs();if(claimed)send({type:'release'});playing=false;motionInput={lx:0,ly:0,rx:0,ry:0};connectionState(demoMode?'preview':'paused');showPlayerOverlay(demoMode?'Controller preview paused.':'Tap Start controller to resume.');});
  document.addEventListener('visibilitychange',()=>{
    if(document.hidden){resumeWanted=false;clearInputs();if(claimed)send({type:'release'});playing=false;wakeLock?.release();}
    else if(!playing)showPlayerOverlay('Welcome back. Tap below to resume your controller.');
  });
  window.addEventListener('blur',()=>clearInputs());
  window.addEventListener('pagehide',()=>{clearInputs();if(claimed)send({type:'release'});});
  window.addEventListener('contextmenu',e=>e.preventDefault());
  fitSurface($('playerViewport'),$('playerSurface'),true);
  setInterval(()=>{if(playing)transmit();},33);
}
async function boot(){
  if(isPlayer)setupPlayer();else setupEditor();
  if(demoMode){
    try{presets=await(await fetch('/assets/presets.json')).json();presets=[...presets,null,null];active=1;info={version:'2.1',mock:true,driver:false,connected:false,output:'ds4',input:neutral()};serverOnline=true;onConfig({presets,active});updateStatus(info);connectionState('preview');showPlayerOverlay('Offline preview. Explore both controller layouts without connecting to a PC.');}
    catch{showPlayerOverlay('Preview assets could not load. Return to the app home screen.');}
    return;
  }
  try{const response=await fetch('/api/info');if(!response.ok)throw Error();info=await response.json();if(!['1.1','2.0','2.1'].includes(info.version))toast('Restart PocketPad to load the new presets and touchpad support.');if(!isPlayer)setupPairing();updateStatus(info);connect();}
  catch{if(isPlayer)showPlayerOverlay('Could not reach the PC host. Open the current QR code again.');else toast('Could not reach the local host. Refresh after starting PocketPad.');}
  setInterval(()=>{if(socket?.readyState===WebSocket.OPEN){pingTime=performance.now();socket.send(JSON.stringify({type:'ping',time:pingTime}));}},1500);
}
boot();
