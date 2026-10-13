import * as THREE from './vendor/three.module.js';
import {Flight,Practice,mapSticks,clamp} from './model.js';
const $=id=>document.getElementById(id), flight=new Flight();
let practice=new Practice(),cameraMode=0,route,marker,overviewSize=20,overviewZ=0,overviewHeight=0;
const stickValues={left:[0,0],right:[0,0]},keys=new Set(),stickReset=[];
function neutral(){keys.clear();for(const reset of stickReset)reset();}
function pause(value=true){neutral();flight.paused=value;$('pause').textContent=value?'繼續':'暫停';}
function reset(){neutral();flight.reset();practice=new Practice($('course').value,Number($('scale').value));$('pause').textContent='暫停';drawRoute();}
let renderer,scene,camera,drone,propellers=[];
try{
 renderer=new THREE.WebGLRenderer({antialias:true,alpha:false});renderer.setPixelRatio(Math.min(devicePixelRatio,1.5));
 renderer.setClearColor(0x91b3c4);$('scene').append(renderer.domElement);renderer.domElement.setAttribute('aria-label','即時 3D 飛行畫面');
 scene=new THREE.Scene();scene.fog=new THREE.Fog(0x91b3c4,180,450);
 scene.add(new THREE.HemisphereLight(0xd4edf7,0x52684a,2.2));const sun=new THREE.DirectionalLight(0xffffff,2.5);sun.position.set(30,60,20);scene.add(sun);
 camera=new THREE.PerspectiveCamera(48,1,.1,600);
 const ground=new THREE.Mesh(new THREE.PlaneGeometry(500,500),new THREE.MeshLambertMaterial({color:0x536e5b}));ground.rotation.x=-Math.PI/2;ground.position.y=-.04;scene.add(ground);
 const pad=new THREE.Mesh(new THREE.PlaneGeometry(220,160),new THREE.MeshLambertMaterial({color:0x263e47}));pad.rotation.x=-Math.PI/2;pad.position.set(0,-.025,-40);scene.add(pad);
 const grid=new THREE.GridHelper(220,110,0x64837b,0x3a5558);grid.position.z=-40;grid.position.y=-.015;scene.add(grid);
 function ring(radius,color){const m=new THREE.Mesh(new THREE.RingGeometry(radius-.045,radius,64),new THREE.MeshBasicMaterial({color,side:THREE.DoubleSide}));m.rotation.x=-Math.PI/2;return m;}
 const landing=ring(1,0x7de5db);landing.position.y=.015;scene.add(landing);
 const hcanvas=document.createElement('canvas');hcanvas.width=hcanvas.height=128;const ctx=hcanvas.getContext('2d');ctx.fillStyle='#96efe6';ctx.font='bold 90px sans-serif';ctx.textAlign='center';ctx.fillText('H',64,99);
 const h=new THREE.Mesh(new THREE.PlaneGeometry(1,1),new THREE.MeshBasicMaterial({map:new THREE.CanvasTexture(hcanvas),transparent:true}));h.rotation.x=-Math.PI/2;h.position.y=.02;scene.add(h);
 const safety=new THREE.Line(new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(-100,.02,4),new THREE.Vector3(100,.02,4)]),new THREE.LineBasicMaterial({color:0xf0b765}));scene.add(safety);
 drone=new THREE.Group();scene.add(drone);
 function box(w,h,d,color,x=0,y=0,z=0){const mesh=new THREE.Mesh(new THREE.BoxGeometry(w,h,d),new THREE.MeshLambertMaterial({color}));mesh.position.set(x,y,z);drone.add(mesh);return mesh;}
 box(.65,.22,.9,0xd8e6eb);box(.45,.2,.2,0xffa63c,0,.02,-.5);
 for(const x of [-.65,.65])for(const z of [-.65,.65]){
  const arm=box(Math.hypot(x,z),.07,.09,0x182b38,x/2,0,z/2);arm.rotation.y=-Math.atan2(z,x);
  const motor=new THREE.Mesh(new THREE.CylinderGeometry(.12,.12,.15,12),new THREE.MeshLambertMaterial({color:z<0?0xffa63c:0x26ddd1}));motor.position.set(x,.1,z);drone.add(motor);
  const prop=box(.75,.02,.07,0x142735,x,.19,z);propellers.push(prop);box(.055,.28,.055,0xc9dce6,x*.5,-.17,z*.5);
 }
 const beacon=new THREE.Mesh(new THREE.RingGeometry(1,1.08,40),new THREE.MeshBasicMaterial({color:0xfbc467,side:THREE.DoubleSide,depthTest:false,transparent:true,opacity:.8}));beacon.rotation.x=-Math.PI/2;beacon.position.y=-.15;drone.add(beacon);
 marker=ring(.85,0x4bfff0);scene.add(marker);
}catch(error){$('error').hidden=false;$('error').textContent='無法啟動 3D 畫面。請使用支援 WebGL 的瀏覽器並開啟硬體加速。'+error.message;}
function drawRoute(){
 if(!scene)return;
 if(route){scene.remove(route);route.geometry.dispose();route.material.dispose();}
 const points=[new THREE.Vector3(0,1.75,0),...practice.steps.map(p=>new THREE.Vector3(p.x,p.h+.25,-p.z))];
 route=new THREE.Line(new THREE.BufferGeometry().setFromPoints(points),new THREE.LineBasicMaterial({color:0x56e6de,transparent:true,opacity:.6}));scene.add(route);
 overviewSize=Math.max(20,...practice.steps.map(p=>Math.max(Math.abs(p.x)*2.2,Math.abs(p.z)*2.2,p.h*2.5)));
 overviewZ=-Math.max(0,...practice.steps.map(p=>p.z))/2;
 overviewHeight=Math.max(0,...practice.steps.map(p=>p.h))/2;
}
for(const side of ['left','right']){
 const element=$(side),knob=element.querySelector('.knob');let pointer=null;
 const clear=()=>{pointer=null;stickValues[side]=[0,0];knob.style.transform='translate(-50%,-50%)';};stickReset.push(clear);
 const move=e=>{if(e.pointerId!==pointer)return;const rect=element.getBoundingClientRect(),r=rect.width/2;let x=(e.clientX-rect.left-r)/r,y=-(e.clientY-rect.top-r)/r;const n=Math.max(1,Math.hypot(x,y));x/=n;y/=n;stickValues[side]=[x,y];knob.style.transform=`translate(calc(-50% + ${x*r*.65}px),calc(-50% - ${y*r*.65}px))`;};
 element.addEventListener('pointerdown',e=>{if(pointer!==null||flight.paused)return;e.preventDefault();pointer=e.pointerId;element.setPointerCapture(pointer);move(e);});
 element.addEventListener('pointermove',move);
 for(const type of ['pointerup','pointercancel','lostpointercapture'])element.addEventListener(type,e=>{if(e.pointerId===pointer)clear();});
}
$('takeoff').onclick=()=>flight.takeoff();$('land').onclick=()=>flight.land();$('reset').onclick=reset;$('pause').onclick=()=>pause(!flight.paused);
$('course').onchange=reset;$('scale').onchange=reset;
$('mode').onchange=()=>{neutral();const mode=$('mode').value;$('left-label').textContent=mode==='2'?'左桿：升降 / 轉向':mode==='1'?'左桿：前後 / 轉向':'左桿：前後 / 左右';$('right-label').textContent=mode==='2'?'右桿：前後 / 左右':mode==='1'?'右桿：升降 / 左右':'右桿：升降 / 轉向';};
$('sensitivity').oninput=neutral;
$('camera').onclick=()=>{cameraMode=(cameraMode+1)%3;$('camera').textContent='視角：'+['全景俯視','眼高操作員','跟隨'][cameraMode];};
$('fullscreen').onclick=async()=>{try{if(document.fullscreenElement)await document.exitFullscreen();else await $('app').requestFullscreen();}catch{$('status').textContent='瀏覽器未允許全螢幕；可繼續在目前畫面練習。';}};
$('help').onclick=()=>{pause();$('guide').showModal();};$('close-help').onclick=()=>$('guide').close();
window.addEventListener('blur',()=>pause());document.addEventListener('visibilitychange',()=>{if(document.hidden)pause();});
window.addEventListener('keydown',e=>{
 if(e.target.closest('input,select,dialog')||$('guide').open)return;
 if(['KeyW','KeyS','KeyA','KeyD','ArrowUp','ArrowDown','ArrowLeft','ArrowRight','Space','KeyP','KeyR','KeyC'].includes(e.code))e.preventDefault();
 keys.add(e.code);if(e.repeat)return;
 if(e.code==='Space'){if(flight.flying)flight.land();else flight.takeoff();}
 if(e.code==='KeyP')pause(!flight.paused);if(e.code==='KeyR')reset();if(e.code==='KeyC')$('camera').click();
});window.addEventListener('keyup',e=>keys.delete(e.code));
const k=key=>keys.has(key)?1:0;
function resize(){if(!renderer)return;const rect=$('scene').getBoundingClientRect();renderer.setSize(rect.width,rect.height);camera.aspect=rect.width/rect.height;camera.updateProjectionMatrix();}window.addEventListener('resize',resize);
drawRoute();resize();let previous=performance.now(),uiAt=0;
function frame(now){
 requestAnimationFrame(frame);if(!renderer)return;const dt=Math.min((now-previous)/1000,.05);previous=now;
 const [lx,ly]=stickValues.left,[rx,ry]=stickValues.right;
 flight.step(dt,mapSticks(clamp(lx+k('KeyD')-k('KeyA'),-1,1),clamp(ly+k('KeyW')-k('KeyS'),-1,1),clamp(rx+k('ArrowRight')-k('ArrowLeft'),-1,1),clamp(ry+k('ArrowUp')-k('ArrowDown'),-1,1),$('mode').value,Number($('sensitivity').value)/100),Number($('wind').value));
 practice.step(dt,flight);drone.position.set(flight.x,flight.h+.35,-flight.z);drone.rotation.y=-flight.yaw*Math.PI/180;
 for(const p of propellers)if(flight.flying&&!flight.paused)p.rotation.y+=dt*40;
 const t=practice.target;marker.visible=!!t;if(t)marker.position.set(t.x,t.h+.3,-t.z);
 if(cameraMode===0){const size=overviewSize/Math.min(1,camera.aspect/1.5);camera.position.set(size*.23,overviewHeight+size*.92,overviewZ+size*.8);camera.lookAt(0,overviewHeight,overviewZ);}
 if(cameraMode===1){camera.position.set(0,1.7,7);camera.lookAt(flight.x,flight.h+.35,-flight.z);}
 if(cameraMode===2){const a=flight.yaw*Math.PI/180;camera.position.set(flight.x-7*Math.sin(a),flight.h+4,-flight.z+7*Math.cos(a));camera.lookAt(drone.position);}
 renderer.render(scene,camera);
 if(now-uiAt>100){uiAt=now;
  $('telemetry').textContent=`高度 ${flight.h.toFixed(1)} m　速度 ${flight.speed.toFixed(1)} m/s　航向 ${Math.round(flight.yaw).toString().padStart(3,'0')}°`;
  $('status').textContent=flight.paused?'已暫停 · 按「繼續」恢復':flight.auto==='up'?'自動起飛至 1.5 m':flight.auto==='down'?'正在降落':flight.flying?'飛行中 · 橘色機頭為前方':'待命 · 點起飛開始';
  $('takeoff').disabled=flight.flying||flight.paused;$('land').disabled=!flight.flying||flight.paused;
  $('task').textContent=practice.finished?'練習完成。可降落或重置再練習。':t?`${practice.index+1}/${practice.steps.length} · ${t.label} · 高 ${t.h.toFixed(1)} m / 航向 ${((t.yaw+360)%360).toFixed(0)}°${t.hold?` · 穩定 ${practice.hold.toFixed(1)}/${t.hold}s`:''}`:'自由飛行 · W/S、A/D 與方向鍵，或雙搖桿操作。';
  $('progress').style.width=(practice.finished?100:practice.steps.length?practice.index/practice.steps.length*100:0)+'%';
 }
}requestAnimationFrame(frame);
