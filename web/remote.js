// A relative touchpad: the phone never needs to know the PC's screen size.
const clamp=(v,lo,hi)=>Math.max(lo,Math.min(hi,v));
const center=points=>({x:points.reduce((n,p)=>n+p.x,0)/points.length,y:points.reduce((n,p)=>n+p.y,0)/points.length});
const distance=points=>points.length===2?Math.hypot(points[0].x-points[1].x,points[0].y-points[1].y):0;

export function initializeRemote({send,ready,changeMode,pause,toast,bridge}){
  const root=document.getElementById('remotePanel');
  root.innerHTML=`<div class="remote-bar"><button id="remoteBack" title="Return to gamepad">‹ Gamepad</button><span id="remoteStatus" role="status">Virtual mouse & keyboard</span><button id="remotePause">Pause</button><button id="remoteHelpButton" aria-label="Touchpad gestures" aria-expanded="false">?</button></div>
    <div id="remotePad" role="application" aria-label="PC touchpad. One finger moves the pointer; two fingers scroll or pinch to zoom; three fingers switch apps."></div>
    <button id="remoteKeyboardButton" aria-label="Open phone keyboard" aria-expanded="false" title="Keyboard"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true"><rect x="2" y="5" width="20" height="14" rx="3"/><path d="M5 9h2m2 0h2m2 0h2m2 0h2M5 12h2m2 0h2m2 0h2m2 0h2M7 16h10"/></svg></button>
    <section id="remoteKeyboard" aria-label="PC keyboard" hidden><div class="remote-keys" id="remoteKeys"></div><textarea id="remoteText" rows="1" aria-label="Type on your PC" placeholder="Type on your PC…" autocomplete="off" autocapitalize="off" spellcheck="false"></textarea><button id="remoteKeyboardClose" aria-label="Hide keyboard">⌄</button></section>
    <section id="remoteHelp" aria-label="Touchpad gestures" hidden><div class="remote-help-heading"><strong>Touchpad gestures</strong><button id="remoteHelpClose" aria-label="Close gesture help">×</button></div><dl>
      <dt>One finger</dt><dd>Move the pointer. Tap to click; double-tap to double-click.</dd>
      <dt>Hold, then move</dt><dd>Drag a window or select text. Lift to release. You can also double-tap and hold to drag.</dd>
      <dt>Two fingers</dt><dd>Slide up/down or sideways to scroll. Tap for right-click. Pinch or spread to zoom.</dd>
      <dt>Three fingers</dt><dd>Swipe left/right to switch apps. Swipe up for Task View; down to show the desktop. Tap for Search.</dd>
      <dt>Four fingers</dt><dd>Swipe left/right to switch virtual desktops. Up for Task View; down for the desktop. Tap for Quick Settings.</dd>
      </dl><div class="remote-mouse-buttons"><button data-click="left">Left click</button><button data-click="middle">Middle click</button><button data-click="right">Right click</button></div>
      <label>Pointer speed <input id="remoteSpeed" type="range" min="0.5" max="3" step="0.1" value="1.5"></label><label><input id="remoteNatural" type="checkbox" checked> Natural scrolling</label><p>Pinch zoom works in apps with Ctrl + wheel zoom. Three- and four-finger gestures require a phone that reports that many touches.</p></section>`;
  const $=id=>document.getElementById(id),pad=$('remotePad'),text=$('remoteText');
  let enabled=false,points=new Map(),gesture=null,holdTimer=0,frame=0,lastTap=0,dragging=false;
  let remainder={move:{dx:0,dy:0},scroll:{dx:0,dy:0}};
  let speed=1.5,natural=true,keyboard=false,composing=false,previous='',modifiers=new Set();
  const sentinel='\u200b';
  try{const saved=JSON.parse(localStorage.getItem('pocketpad-remote')||'{}');speed=clamp(Number(saved.speed)||1.5,.5,3);natural=saved.natural!==false;}catch{}
  $('remoteSpeed').value=speed;$('remoteNatural').checked=natural;
  function save(){try{localStorage.setItem('pocketpad-remote',JSON.stringify({speed,natural}));}catch{}}
  $('remoteSpeed').oninput=()=>{speed=Number($('remoteSpeed').value);save();};
  $('remoteNatural').onchange=()=>{natural=$('remoteNatural').checked;save();};
  function emit(data){
    if(!enabled||!ready())return false;
    if(data.action==='move'||data.action==='scroll'){
      const rest=remainder[data.action];
      for(const axis of ['dx','dy']){const total=(data[axis]||0)+rest[axis];data[axis]=Math.trunc(total);rest[axis]=total-data[axis];}
      if(!data.dx&&!data.dy)return true;
    }
    return send({type:'desktop',...data});
  }
  function clearText(){previous='';text.value=sentinel;}
  function clearModifiers(){modifiers.clear();$('remoteKeys').querySelectorAll('[data-modifier]').forEach(b=>b.setAttribute('aria-pressed','false'));}
  function key(name){emit({action:'key',key:name,modifiers:[...modifiers]});clearModifiers();clearText();}
  function hold(){if(!gesture||points.size!==1||gesture.max!==1||gesture.moved)return;dragging=true;gesture.drag=true;emit({action:'button',button:'left',down:true});clearText();}
  function rebase(){if(!gesture||!points.size)return;const values=[...points.values()];gesture.base=gesture.last=center(values);gesture.span=distance(values);gesture.zoom=0;gesture.kind=null;}
  function flush(){
    if(frame){cancelAnimationFrame(frame);frame=0;}
    if(!gesture||!points.size||!ready())return;
    const values=[...points.values()],pos=center(values),g=gesture;
    if(values.length<g.max||g.max>4)return; // Lifting a finger never turns a scroll into a click.
    const dx=pos.x-g.last.x,dy=pos.y-g.last.y,tx=pos.x-g.base.x,ty=pos.y-g.base.y;
    if(Math.hypot(tx,ty)>7){g.moved=true;clearTimeout(holdTimer);}
    if(g.max===1){
      if(g.doubleTap&&g.moved&&!dragging){dragging=true;g.drag=true;emit({action:'button',button:'left',down:true});clearText();}
      emit({action:'move',dx:dx*speed,dy:dy*speed});
    }else if(g.max===2){
      const span=distance(values),ratio=g.span>10?Math.log(Math.max(1,span)/g.span):0;
      if(!g.kind){
        if(Math.abs(ratio)>.065&&Math.abs(span-g.span)>Math.hypot(tx,ty)*1.3){g.kind='zoom';g.moved=true;}
        else if(Math.hypot(tx,ty)>7){g.kind='scroll';g.moved=true;}
      }
      if(g.kind==='zoom'){
        const steps=Math.trunc(ratio/.09)-g.zoom;
        if(steps){emit({action:'zoom',steps:clamp(steps,-5,5)});g.zoom+=clamp(steps,-5,5);}
      }else if(g.kind==='scroll'){
        // Send accumulated travel on the first classified frame, then deltas.
        const first=!g.scrolled;g.scrolled=true;
        emit({action:'scroll',dx:-(first?tx:dx)*3*(natural?1:-1),dy:(first?ty:dy)*3*(natural?1:-1)});
      }
    }else if(!g.fired&&Math.hypot(tx,ty)>55){
      let name;
      if(Math.abs(tx)>Math.abs(ty)*1.2)name=g.max===3?(tx<0?'app_next':'app_previous'):(tx<0?'desktop_next':'desktop_previous');
      else if(Math.abs(ty)>Math.abs(tx)*1.2)name=ty<0?'task_view':'show_desktop';
      if(name){emit({action:'gesture',name});g.fired=true;g.moved=true;clearText();}
    }
    g.last=pos;
  }
  pad.onpointerdown=e=>{
    if(!ready()||e.button>0)return;e.preventDefault();
    if(!points.size){gesture={start:performance.now(),max:1,moved:false,drag:false,fired:false,scrolled:false};}
    points.set(e.pointerId,{x:e.clientX,y:e.clientY});pad.setPointerCapture(e.pointerId);
    gesture.max=Math.max(gesture.max,points.size);clearTimeout(holdTimer);
    if(points.size>1&&dragging){emit({action:'button',button:'left',down:false});dragging=false;}
    rebase();
    if(points.size===1){
      gesture.doubleTap=performance.now()-lastTap<300;
      holdTimer=setTimeout(hold,420);
    }
  };
  pad.onpointermove=e=>{
    if(!points.has(e.pointerId))return;e.preventDefault();
    points.set(e.pointerId,{x:e.clientX,y:e.clientY});
    if(!frame)frame=requestAnimationFrame(()=>{frame=0;flush();});
  };
  function release(e){
    if(!points.has(e.pointerId))return;
    flush();clearTimeout(holdTimer);points.delete(e.pointerId);
    if(e.type==='pointercancel'||e.type==='lostpointercapture'){reset();return;}
    if(points.size)return;
    const g=gesture;gesture=null;
    if(dragging){emit({action:'button',button:'left',down:false});dragging=false;lastTap=0;return;}
    if(g&&g.max<=4&&!g.moved&&!g.drag&&performance.now()-g.start<350){
      if(g.max<=2){emit({action:'click',button:g.max===2?'right':'left'});clearText();lastTap=g.max===1?performance.now():0;}
      else emit({action:'gesture',name:g.max===3?'search':'quick_settings'});
    }else lastTap=0;
  }
  pad.onpointerup=release;pad.onpointercancel=release;pad.onlostpointercapture=release;
  pad.oncontextmenu=e=>e.preventDefault();
  function reset(transmit=true){
    if(transmit&&enabled&&ready())emit({action:'release'});
    clearTimeout(holdTimer);if(frame)cancelAnimationFrame(frame);frame=0;
    points.clear();gesture=null;dragging=false;lastTap=0;clearModifiers();
    remainder={move:{dx:0,dy:0},scroll:{dx:0,dy:0}};
    composing=false;clearText();
  }
  function closeKeyboard(){keyboard=false;$('remoteKeyboard').hidden=true;$('remoteKeyboardButton').setAttribute('aria-expanded','false');text.blur();bridge?.hideKeyboard?.();clearModifiers();clearText();}
  function openKeyboard(){
    if(!ready()){toast('Start the controller to type on your PC.');return;}
    keyboard=true;$('remoteHelp').hidden=true;$('remoteHelpButton').setAttribute('aria-expanded','false');
    $('remoteKeyboard').hidden=false;$('remoteKeyboardButton').setAttribute('aria-expanded','true');
    clearText();text.focus({preventScroll:true});text.setSelectionRange(text.value.length,text.value.length);bridge?.showKeyboard?.();
  }
  $('remoteKeyboardButton').onclick=()=>keyboard?closeKeyboard():openKeyboard();
  $('remoteKeyboardClose').onclick=closeKeyboard;
  $('remoteBack').onclick=()=>changeMode(false);$('remotePause').onclick=pause;
  $('remoteHelpButton').onclick=()=>{reset();closeKeyboard();$('remoteHelp').hidden=!$('remoteHelp').hidden;$('remoteHelpButton').setAttribute('aria-expanded',String(!$('remoteHelp').hidden));};
  $('remoteHelpClose').onclick=()=>{$('remoteHelp').hidden=true;$('remoteHelpButton').setAttribute('aria-expanded','false');};
  root.querySelectorAll('[data-click]').forEach(b=>b.onclick=()=>{emit({action:'click',button:b.dataset.click});clearText();});
  const keys=$('remoteKeys');
  for(const name of ['Ctrl','Alt','Shift','Win','Escape','Tab','ArrowLeft','ArrowUp','ArrowDown','ArrowRight','Backspace','Delete','Enter','Home','End','PageUp','PageDown',...Array.from({length:12},(_,i)=>'F'+(i+1))]){
    const b=document.createElement('button');b.type='button';b.textContent=({Escape:'Esc',ArrowLeft:'←',ArrowUp:'↑',ArrowDown:'↓',ArrowRight:'→',Backspace:'⌫',PageUp:'PgUp',PageDown:'PgDn'})[name]||name;
    b.onpointerdown=e=>e.preventDefault();
    if(['Ctrl','Alt','Shift','Win'].includes(name)){
      b.dataset.modifier=name;b.setAttribute('aria-pressed','false');b.onclick=()=>{if(modifiers.has(name))modifiers.delete(name);else modifiers.add(name);b.setAttribute('aria-pressed',String(modifiers.has(name)));text.focus({preventScroll:true});};
    }else{b.dataset.key=name;b.onclick=()=>{key(name);text.focus({preventScroll:true});};}
    keys.append(b);
  }
  function updateText(){
    if(composing)return;
    const value=text.value.replace(/^\u200b/,'');
    if(!ready()){clearText();return;}
    const before=[...previous],after=[...value];let prefix=0;
    while(prefix<before.length&&prefix<after.length&&before[prefix]===after[prefix])prefix++;
    for(let i=prefix;i<before.length;i++)emit({action:'key',key:'Backspace'});
    const inserted=after.slice(prefix).join('');
    if(inserted){
      if(modifiers.size){for(const letter of inserted)emit({action:'text',text:letter,modifiers:[...modifiers]});clearModifiers();clearText();return;}
      else for(let i=0;i<inserted.length;){const part=[...inserted.slice(i)].slice(0,512).join('');emit({action:'text',text:part});i+=part.length;}
    }
    previous=value;
    if(!text.value.startsWith(sentinel)){if(!value&&!before.length)emit({action:'key',key:'Backspace'});text.value=sentinel+value;}
    if(previous.length>512){previous=[...previous].slice(-128).join('');text.value=sentinel+previous;}
    text.setSelectionRange(text.value.length,text.value.length);
  }
  text.addEventListener('compositionstart',()=>{composing=true;});
  text.addEventListener('compositionend',()=>{composing=false;updateText();});
  text.oninput=e=>{if(!e.isComposing)updateText();};
  text.onbeforeinput=e=>{
    if(e.isComposing||composing)return;
    if(e.inputType==='insertLineBreak'||e.inputType==='insertParagraph'){e.preventDefault();key('Enter');}
    if(e.inputType==='deleteContentBackward'&&!previous){e.preventDefault();key('Backspace');}
  };
  text.onkeydown=e=>{
    if(e.isComposing||composing||e.keyCode===229)return;
    if(['Enter','Tab','Escape','Delete','ArrowLeft','ArrowRight','ArrowUp','ArrowDown','Home','End','PageUp','PageDown'].includes(e.key)||(e.key==='Backspace'&&!previous)){
      e.preventDefault();key(e.key);
    }
  };
  clearText();
  // Android's IME may resize the visual viewport instead of the layout viewport.
  function fit(){const height=window.visualViewport?.height||window.innerHeight;root.style.height=height+'px';root.classList.toggle('remote-compact',height<240);}
  window.visualViewport?.addEventListener('resize',fit);window.addEventListener('resize',fit);fit();
  return {
    reset,
    setEnabled(value){reset(false);enabled=value;root.hidden=!value;document.body.classList.toggle('remote-mode',value);closeKeyboard();$('remoteHelp').hidden=true;bridge?.setInputMode?.(value?'desktop':'gamepad');fit();},
    status(value){$('remoteStatus').textContent=value;$('remotePause').textContent=ready()?'Pause':'Resume';if(!ready()&&keyboard)closeKeyboard();},
  };
}
