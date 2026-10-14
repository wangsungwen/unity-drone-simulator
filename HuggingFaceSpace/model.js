export const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
export const delta=(a,b)=>((b-a+540)%360)-180;
export function axis(v,s=0.7){return Number.isFinite(v)&&Math.abs(v)>.08?Math.sign(v)*(Math.min(1,Math.abs(v))-.08)/.92*s:0;}
export function mapSticks(lx,ly,rx,ry,mode='2',s=.7){
 const values=mode==='1'?[ry,lx,ly,rx]:mode==='3'?[ry,rx,ly,lx]:[ly,lx,ry,rx];
 return values.map(v=>axis(v,s));
}
export class Flight {
 constructor(){this.reset();}
 reset(){Object.assign(this,{x:0,z:0,h:0,yaw:0,vx:0,vz:0,vh:0,flying:false,auto:null,paused:false,time:0,tiltPitch:0,tiltRoll:0});}
 takeoff(){if(!this.flying&&!this.paused){this.flying=true;this.auto='up';}}
 land(){if(this.flying&&!this.paused)this.auto='down';}
 step(dt,input=[0,0,0,0],wind=0){
  dt=clamp(dt,0,.05); if(this.paused)return;
  const tiltGain=1-Math.exp(-5*dt);
  const tilting=this.flying&&this.auto!=='down';
  // Three.js nose points -Z; negative X pitches it down. Match Unity's 16 degree response.
  this.tiltPitch+=((tilting?-clamp(input[2],-1,1)*16:0)-this.tiltPitch)*tiltGain;
  this.tiltRoll+=((tilting?-clamp(input[3],-1,1)*16:0)-this.tiltRoll)*tiltGain;
  if(!this.flying)return;
  this.time+=dt; const [throttle,yaw,pitch,roll]=input;
  this.yaw=(this.yaw+yaw*75*dt+360)%360; const r=this.yaw*Math.PI/180;
  const gain=1-Math.exp(-4*dt);
  const forward=this.auto==='down'?0:pitch*5, side=this.auto==='down'?0:roll*5;
  this.vx+=((forward*Math.sin(r)+side*Math.cos(r)+wind)-this.vx)*gain;
  this.vz+=((forward*Math.cos(r)-side*Math.sin(r))-this.vz)*gain;
  let lift=throttle*3;
  if(this.auto==='up'){lift=clamp((1.5-this.h)*2,0,1.5);if(this.h>1.47){this.auto=null;this.h=1.5;this.vh=0;lift=0;}}
  if(this.auto==='down')lift=-.7;
  this.vh+=(lift-this.vh)*gain;
  this.x=clamp(this.x+this.vx*dt,-110,110);this.z=clamp(this.z+this.vz*dt,-40,120);
  this.h=clamp(this.h+this.vh*dt,0,40);
  if(this.h===0 && this.auto!=='up'){this.flying=false;this.auto=null;this.vx=this.vz=this.vh=0;}
 }
 get speed(){return Math.hypot(this.vx,this.vz,this.vh);}
}
export function makeCourse(kind,scale=1){
 const steps=[];
 const add=(x,z,h=1.5,yaw=0,hold=0,label='沿航線飛行')=>steps.push({x,z,h,yaw,hold,label});
 const travel=(x,z,h=1.5,yaw=null)=>{
  const p=steps.at(-1)||{x:0,z:0,h:1.5};const n=Math.max(1,Math.ceil(Math.hypot(x-p.x,z-p.z,h-p.h)/1.2));
  const heading=yaw===null?Math.atan2(x-p.x,z-p.z)*180/Math.PI:yaw;
  for(let i=1;i<=n;i++)add(p.x+(x-p.x)*i/n,p.z+(z-p.z)*i/n,p.h+(h-p.h)*i/n,heading);
 };
 if(kind==='free')return steps;
 add(0,0,1.5,0,5,'H 點朝外穩定停懸 5 秒');
 if(kind==='hover')for(let i=1;i<=4;i++)add(0,0,1.5,i*90%360,5,'順時針轉 90°，停懸 5 秒');
 if(kind==='rectangle')for(const [x,z] of [[-6,0],[-6,5],[6,5],[6,0],[0,0],[6,0],[6,5],[-6,5],[-6,0],[0,0]]){
  travel(x,z);const p=steps.at(-1);add(x,z,1.5,p.yaw,5,'矩形轉角停懸 5 秒');
 }
 if(kind==='eight'){
  travel(0,6*scale,1.5,0);
  for(let side=0;side<2;side++)for(let i=1;i<=72;i++){
   const a=i*Math.PI*2/72;
   add((side===0?-6+6*Math.cos(a):6-6*Math.cos(a))*scale,(6+6*Math.sin(a))*scale,1.5,(side===0?-1:1)*a*180/Math.PI,0,side===0?'左圈逆時針':'右圈順時針');
  }
  travel(0,0,1.5,0);
 }
 if(kind==='sideways')for(const heading of [270,90]){
  add(0,0,1.5,heading,5,'轉向側面，停懸 5 秒');
  for(const x of [heading===270?-6:6,heading===270?6:-6,0]){travel(x*scale,0,1.5,heading);add(x*scale,0,1.5,heading,5,'保持機頭方向，停懸 5 秒');}
 }
 if(kind==='circuit'){
  travel(0,0,20,0);
  for(const [x,z] of [[-40,0],[-40,20],[40,20],[40,0],[0,0]])travel(x*scale,z*scale,20);
  travel(0,0,1.5,0);
 }
 if(kind==='hover'||kind==='landing')add(0,0,0,0,.5,'降落 H 圈內');
 else add(0,0,1.5,0,5,'返回 H 朝外停懸，完成練習');
 return steps;
}
export class Practice {
 constructor(kind='free',scale=1){this.steps=makeCourse(kind,scale);this.index=0;this.hold=0;this.elapsed=0;this.finished=false;}
 step(dt,f){
  if(f.paused||!this.steps.length||this.finished)return;
  dt=clamp(dt,0,.05);this.elapsed+=f.flying?dt:0;const p=this.steps[this.index];
  const horizontal=Math.hypot(f.x-p.x,f.z-p.z), low=p.h===0;
  const position=horizontal<(low?.5:p.h===20?2:p.hold?.55:1);
  const altitude=Math.abs(f.h-p.h)<(low?.05:.5);
  const direction=Math.abs(delta(f.yaw,p.yaw))<(p.hold?15:35);
  const stable=!p.hold||f.speed<=.5;
  const inside=position&&altitude&&direction&&stable&&(low?!f.flying:f.flying);
  this.hold=inside?this.hold+dt:0;
  if(inside&&this.hold>=p.hold){this.hold=0;this.index++;if(this.index===this.steps.length)this.finished=true;}
 }
 get target(){return this.finished?null:this.steps[this.index];}
}
