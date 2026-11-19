// Device appearance, motion, connection cues and a practice-only walkthrough.
const byId=id=>document.getElementById(id);
export const bridge=window.PocketPadNative;
export let preferences={appearance:'matte',animations:true,haptics:true};
try{Object.assign(preferences,JSON.parse(localStorage.getItem('pocketpad-experience'))||{});}catch{}
try{if(bridge?.getPreferences)Object.assign(preferences,JSON.parse(bridge.getPreferences()));}catch{}
let connection='',tourStep=0,priorGyro='off',tourOpen=false,practiceMotionActive=false;
export function applyPreferences(next={},persist=false){
  Object.assign(preferences,next);
  if(!['matte','oled','glass'].includes(preferences.appearance))preferences.appearance='matte';
  preferences.animations=preferences.animations!==false;preferences.haptics=preferences.haptics!==false;
  document.body.dataset.appearance=preferences.appearance;
  document.body.classList.toggle('motion-off',!preferences.animations);
  if(byId('appearanceChoice')){byId('appearanceChoice').value=preferences.appearance;byId('richMotion').checked=preferences.animations;byId('experienceHaptics').checked=preferences.haptics;}
  try{localStorage.setItem('pocketpad-experience',JSON.stringify(preferences));}catch{}
  if(persist&&bridge?.savePreferences)bridge.savePreferences(JSON.stringify(preferences));
}
export function connectionState(state){
  if(connection===state)return;const previous=connection;connection=state;
  const visual=byId('connectionVisual');if(!visual)return;
  visual.dataset.state=state;document.body.dataset.connection=state;
  const names={connecting:'Connecting',ready:'Ready',reconnecting:'Reconnecting',paused:'Paused',preview:'Offline preview',busy:'Controller in use'};
  byId('connectionTitle').textContent=names[state]||state;
  visual.querySelector('span').textContent=state==='ready'?'✓':state==='reconnecting'?'↻':state==='paused'?'Ⅱ':'⌁';
  if(preferences.animations&&previous!==state){visual.classList.remove('state-arrived');void visual.offsetWidth;visual.classList.add('state-arrived');}
  if(preferences.haptics&&['ready','reconnecting'].includes(state)){
    if(bridge?.connectionCue)bridge.connectionCue(state);
    else try{navigator.vibrate?.(state==='ready'?[16,35,16]:[12,55,12]);}catch{}
  }
}
export function initializeExperience(){
  applyPreferences();
  byId('appearanceChoice').onchange=()=>applyPreferences({appearance:byId('appearanceChoice').value},true);
  byId('richMotion').onchange=()=>applyPreferences({animations:byId('richMotion').checked},true);
  byId('experienceHaptics').onchange=()=>applyPreferences({haptics:byId('experienceHaptics').checked},true);
  byId('playerSettingsButton').onclick=()=>{
    for(const id of ['playerPresetMenu','playerSizeMenu','playerMotionMenu'])byId(id).hidden=true;
    byId('playerSettingsMenu').hidden=!byId('playerSettingsMenu').hidden;
  };
  byId('replayWalkthrough').onclick=()=>{byId('playerSettingsMenu').hidden=true;openWalkthrough();};
  byId('tourNext').onclick=()=>{if(tourStep===3)finishWalkthrough();else{tourStep++;renderTour();}};
  byId('tourBack').onclick=()=>{tourStep=Math.max(0,tourStep-1);renderTour();};
  byId('tourSkip').onclick=finishWalkthrough;
  window.addEventListener('pocketpad-preferences',e=>applyPreferences(e.detail||{}));
  window.addEventListener('pocketpad-motion',e=>{
    if(!tourOpen||tourStep!==3)return;
    const knob=byId('tourGyroDot');if(!knob)return;
    const roll=Math.max(-30,Math.min(30,Number(e.detail?.roll)||0));knob.style.transform=`translateX(${roll*2}px)`;
    byId('tourFeedback').textContent=`Tilt: ${Math.round(roll)}° · practice only`;
  });
}
export function offerWalkthrough(){
  let done=false;
  try{done=localStorage.getItem('pocketpad-tour-complete')==='1';if(bridge?.tourCompleted)done=bridge.tourCompleted();}catch{}
  const forced=new URLSearchParams(location.search).get('tour')==='1';
  if((!done||forced)&&!tourOpen)openWalkthrough();
}
export function openWalkthrough(){
  window.dispatchEvent(new Event('pocketpad-pause'));
  tourStep=0;tourOpen=true;byId('walkthrough').hidden=false;
  priorGyro=bridge?.getMotionMode?.()||'off';renderTour();
}
function finishWalkthrough(){
  tourOpen=false;byId('walkthrough').hidden=true;
  restorePracticeMotion();
  try{localStorage.setItem('pocketpad-tour-complete','1');bridge?.completeTour?.();}catch{}
}
function renderTour(){
  const stages=[
    ['Pair your phone','In PocketPad on your PC, open Connect phone. Scan its controller QR from the Android app. Both devices need the same local network.'],
    ['Find your size','Use Size on your phone for overall scaling. To change one button, select it in the PC editor and drag its corner. Try the practice slider.'],
    ['Touch and click','The PS5 touchpad tracks two fingers. Tap with two fingers to click, or use the separate click button. Try moving two fingers here.'],
    ['Tilt, then recenter','In the Android Gyro menu, choose steering or aiming and tap Recenter while holding the phone comfortably. Practice here never controls a game.']
  ];
  const [title,description]=stages[tourStep];byId('tourTitle').textContent=title;byId('tourDescription').textContent=description;
  byId('tourProgress').textContent=`GETTING STARTED · ${tourStep+1} / 4`;byId('tourBack').disabled=tourStep===0;byId('tourNext').textContent=tourStep===3?'Finish':'Next';
  byId('tourDots').innerHTML=stages.map((_,i)=>`<i class="${i===tourStep?'active':''}"></i>`).join('');
  byId('tourFeedback').textContent='';const pane=byId('tourPractice');pane.replaceChildren();pane.className='tour-practice';
  if(preferences.animations){const card=byId('walkthrough').querySelector('.tour-card');card.classList.remove('tour-arrive');void card.offsetWidth;card.classList.add('tour-arrive');}
  if(tourStep!==3)restorePracticeMotion();
  if(tourStep===0){
    pane.innerHTML='<div class="tour-pair"><div>▣<small>PC</small></div><span>⌁</span><div>▯<small>PHONE</small></div></div><button id="tourPairTry">Test connection indicator</button>';
    byId('tourPairTry').onclick=()=>{pane.classList.add('practice-ready');byId('tourFeedback').textContent='Ready · the real connection will also give a small haptic cue.';if(preferences.haptics)bridge?.connectionCue?.('ready');};
  }else if(tourStep===1){
    pane.innerHTML='<div class="tour-size-area"><div id="tourSizeSample">×</div></div><input id="tourSizeRange" aria-label="Practice control size" type="range" min="50" max="110" value="70">';
    byId('tourSizeRange').oninput=e=>{const n=Number(e.target.value);byId('tourSizeSample').style.width=n+'px';byId('tourSizeSample').style.height=n+'px';byId('tourFeedback').textContent=`${n} × ${n} · practice only`;};
  }else if(tourStep===2){
    pane.innerHTML='<div id="tourTouchSurface" class="tour-touch"><span>Touch here with one or two fingers</span></div><button id="tourClickTry">Try touchpad click</button>';
    const area=byId('tourTouchSurface'),contacts=new Map();
    const draw=()=>{area.querySelectorAll('i').forEach(n=>n.remove());for(const point of contacts.values()){const marker=document.createElement('i');marker.style.left=point.x+'%';marker.style.top=point.y+'%';area.append(marker);}byId('tourFeedback').textContent=`${contacts.size} / 2 fingers · no game input`;};
    area.onpointerdown=e=>{if(contacts.size>=2)return;e.preventDefault();area.setPointerCapture(e.pointerId);const r=area.getBoundingClientRect();contacts.set(e.pointerId,{x:(e.clientX-r.x)/r.width*100,y:(e.clientY-r.y)/r.height*100});draw();};
    area.onpointermove=e=>{if(!contacts.has(e.pointerId))return;const r=area.getBoundingClientRect();contacts.set(e.pointerId,{x:Math.max(0,Math.min(100,(e.clientX-r.x)/r.width*100)),y:Math.max(0,Math.min(100,(e.clientY-r.y)/r.height*100))});draw();};
    area.onpointerup=area.onpointercancel=e=>{contacts.delete(e.pointerId);draw();};
    byId('tourClickTry').onclick=()=>{area.classList.toggle('clicked');byId('tourFeedback').textContent=area.classList.contains('clicked')?'Click pressed · practice only':'Click released';};
  }else{
    pane.innerHTML='<div class="tour-gyro-track"><div id="tourGyroDot"></div></div><input id="tourGyroRange" aria-label="Practice tilt" type="range" min="-30" max="30" value="0"><button id="tourRecenterTry">Recenter</button>';
    if(bridge?.hasMotion?.()){practiceMotionActive=true;if(bridge.beginPracticeMotion)bridge.beginPracticeMotion('steer');else bridge.setMotion('steer');}
    byId('tourGyroRange').oninput=e=>{byId('tourGyroDot').style.transform=`translateX(${Number(e.target.value)*2}px)`;byId('tourFeedback').textContent=`Tilt: ${e.target.value}° · practice only`;};
    byId('tourRecenterTry').onclick=()=>{bridge?.recenter?.();byId('tourGyroRange').value=0;byId('tourGyroDot').style.transform='translateX(0)';byId('tourFeedback').textContent='Centered. Your comfortable grip is zero.';};
  }
}
function restorePracticeMotion(){if(!practiceMotionActive)return;practiceMotionActive=false;if(bridge?.endPracticeMotion)bridge.endPracticeMotion();else if(bridge?.setMotion)bridge.setMotion(priorGyro);}
window.addEventListener('pagehide',restorePracticeMotion);
window.addEventListener('pocketpad-pause',()=>{if(tourOpen)restorePracticeMotion();});
applyPreferences();
