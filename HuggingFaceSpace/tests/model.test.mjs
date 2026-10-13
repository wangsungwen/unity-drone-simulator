import test from 'node:test';
import assert from 'node:assert/strict';
import {Flight,Practice,mapSticks,axis,makeCourse,delta} from '../model.js';
function run(f,n=600,input=[0,0,0,0],wind=0){for(let i=0;i<n;i++)f.step(1/60,input,wind);}
test('deadzone, nonfinite rejection, three stick modes',()=>{
 assert.equal(axis(.07),0);assert.equal(axis(NaN),0);
 assert.deepEqual(mapSticks(1,-1,-1,1,'2',1),[-1,1,1,-1]);
 assert.deepEqual(mapSticks(1,-1,-1,1,'1',1),[1,1,-1,-1]);
 assert.deepEqual(mapSticks(1,-1,-1,1,'3',1),[1,-1,-1,1]);
});
test('takeoff settles near 1.5m, hover, landing and reset',()=>{
 const f=new Flight();run(f);assert.equal(f.h,0);f.takeoff();run(f);assert.ok(Math.abs(f.h-1.5)<.06);assert.equal(f.auto,null);
 run(f);assert.ok(Math.abs(f.h-1.5)<.06);assert.equal(f.x,0);f.land();run(f);assert.equal(f.h,0);assert.equal(f.flying,false);
 f.reset();assert.equal(f.time,0);
});
test('motion follows heading and pause freezes state',()=>{
 const f=new Flight();f.takeoff();run(f);f.yaw=90;run(f,60,[0,0,1,0]);assert.ok(f.x>3);assert.ok(Math.abs(f.z)<.01);
 f.paused=true;const saved=JSON.stringify(f);run(f,60,[1,1,1,1]);assert.equal(JSON.stringify(f),saved);
});
test('wind, altitude and field boundaries are bounded',()=>{
 const f=new Flight();f.takeoff();run(f);run(f,60,[0,0,0,0],.7);assert.ok(f.x>.3);
 run(f,10000,[1,0,1,1]);assert.equal(f.h,40);assert.equal(f.x,110);assert.equal(f.z,120);
});
test('course dimensions and figure eight continuity match selected scale',()=>{
 const a=makeCourse('rectangle',2);assert.equal(Math.max(...a.map(p=>p.x)),6);assert.equal(Math.max(...a.map(p=>p.z)),5);
 const b=makeCourse('circuit',2);assert.equal(Math.max(...b.map(p=>p.x)),80);assert.equal(Math.max(...b.map(p=>p.z)),40);assert.equal(Math.max(...b.map(p=>p.h)),20);
 const c=makeCourse('eight');for(let i=1;i<c.length;i++)assert.ok(Math.hypot(c[i].x-c[i-1].x,c[i].z-c[i-1].z)<=1.21);
});
test('hover requires continuous hold and valid heading',()=>{
 const p=new Practice('hover');const f=new Flight();f.flying=true;f.h=1.5;
 for(let i=0;i<120;i++)p.step(1/60,f);assert.ok(p.hold>1.9);f.x=2;p.step(1/60,f);assert.equal(p.hold,0);f.x=0;
 for(let i=0;i<301;i++)p.step(1/60,f);assert.equal(p.index,1);
 for(let i=0;i<301;i++)p.step(1/60,f);assert.equal(p.index,1);f.yaw=90;
 for(let i=0;i<301;i++)p.step(1/60,f);assert.equal(p.index,2);
});
test('landing target requires touchdown inside landing area',()=>{
 const p=new Practice('landing'),f=new Flight();f.flying=true;f.h=1.5;
 for(let i=0;i<301;i++)p.step(1/60,f);assert.equal(p.index,1);
 f.h=0;f.flying=false;f.x=.8;for(let i=0;i<60;i++)p.step(1/60,f);assert.equal(p.finished,false);
 f.x=0;for(let i=0;i<31;i++)p.step(1/60,f);assert.equal(p.finished,true);
});
test('angle wrap and empty free course',()=>{assert.equal(delta(350,10),20);assert.deepEqual(makeCourse('free'),[]);});
