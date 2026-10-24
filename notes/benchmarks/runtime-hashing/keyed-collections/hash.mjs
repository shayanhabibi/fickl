import assert from 'node:assert/strict';
import fs from 'node:fs';
import {performance} from 'node:perf_hooks';
const root=new URL('./temp/hash/',import.meta.url);
const mods=await Promise.all(['baseline','candidate'].map(async v=>({map:await import(new URL(v+'/MutableMap.js',root)),set:await import(new URL(v+'/MutableSet.js',root))})));
const create=(lib,kind,cmp)=>new (kind==='map'?lib.Dictionary:lib.HashSet)([],cmp);
const results={node:process.version,platform:process.platform,correctness:[],timings:[]};
const attempt=f=>{try{return {value:f()};}catch(e){return {error:e.constructor.name,message:e.message};}};
function scenario(lib,kind,hashMode) {
 const trace=[],snapshots=[];
 const cmp={GetHashCode(k){trace.push(['h',k.id]);return hashMode==='collision'?0:k.id%11;},Equals(a,b){trace.push(['e',a.id,b.id]);return a.id===b.id;}};
 const c=create(lib,kind,cmp);
 const add=(k,v)=>kind==='map'?c.set(k,v):c.add(k);
 for(let i=0;i<700;i++) {
   const k={id:(i*31)%71},op=i%7;
   let value;
   if(op<3){add(k,i);value='add';}
   else if(op===3)value=c.delete(k);
   else if(op===4)value=c.has(k);
   else if(op===5)value=kind==='map'?attempt(()=>c.get(k)):c.has(k);
   else if(i%49===6){c.clear();value='clear';}
   else value=c.size;
   snapshots.push([value,c.size,Array.from(c)]);
 }
 // Active iterators remain live under same-bucket and different-bucket changes.
 c.clear();add({id:1},1);add({id:2},2);add({id:3},3);
 const iterator=c[Symbol.iterator]();const events=[iterator.next()];
 add({id:4},4);c.delete({id:2});events.push(iterator.next());c.clear();add({id:5},5);
 for(let i=0;i<10;i++){const x=iterator.next();events.push(x);if(x.done)break;}
 return {trace,snapshots,events};
}
function effects(lib,kind,mode,operation) {
 const trace=[];let c,armed=false;
 const cmp={GetHashCode(k){trace.push(['h',k.id]);if(armed&&mode==='hash-clear'){armed=false;c.clear();}if(armed&&mode==='hash-throw')throw Error('hash throw');return 0;},Equals(a,b){trace.push(['e',a.id,b.id]);if(armed){armed=false;if(mode==='equal-clear')c.clear();if(mode==='equal-add')kind==='map'?c.set({id:9},9):c.add({id:9});if(mode==='equal-delete')c.delete({id:1});if(mode==='equal-throw')throw Error('equal throw');}return a.id===b.id;}};
 c=create(lib,kind,cmp);kind==='map'?c.set({id:1},1):c.add({id:1});kind==='map'?c.set({id:2},2):c.add({id:2});armed=true;
 const key={id:2};const value=attempt(()=>{
  if(operation==='has')return c.has(key);
  if(operation==='delete')return c.delete(key);
  if(operation==='get')return kind==='map'?c.get(key):c.has(key);
  if(operation==='add'&&kind==='map')return lib.Dictionary__Add_5BDDA1(c,key,8);
  if(kind==='map')c.set(key,8);else c.add(key);return 'updated';
 });return {value,trace,entries:Array.from(c),count:c.size};
}
for(const kind of ['map','set']) {
 for(const hashMode of ['collision','buckets']) {const a=scenario(mods[0][kind],kind,hashMode),b=scenario(mods[1][kind],kind,hashMode);assert.deepEqual(a,b);results.correctness.push({kind,hashMode,operations:700,traceCalls:a.trace.length,matched:true});}
 for(const mode of ['hash-clear','hash-throw','equal-clear','equal-add','equal-delete','equal-throw'])for(const operation of ['has','delete','get','set','add']){const a=effects(mods[0][kind],kind,mode,operation),b=effects(mods[1][kind],kind,mode,operation);assert.deepEqual(a,b);results.correctness.push({kind,mode,operation,matched:true,observed:a});}
 // Mechanism gate: instrument only the private native hash map, not user comparisons.
 const counts=mods.map(({[kind]:lib})=>{const c=create(lib,kind,{GetHashCode:k=>k,Equals:(a,b)=>a===b});kind==='map'?c.set(1,2):c.add(1);let has=0,get=0;const hm=c.hashMap;hm.has=function(k){has++;return Map.prototype.has.call(this,k)};hm.get=function(k){get++;return Map.prototype.get.call(this,k)};c.has(1);c.has(2);return {has,get};});
 assert.deepEqual(counts,[{has:2,get:1},{has:0,get:2}]);results.correctness.push({kind,mechanism:counts});
}
if(!process.argv.includes('--check-only')) {
 for(const kind of ['map','set']) for(const collision of [false,true]) for(const workload of ['hit','miss','update','remove-add']) {
  const nkeys=collision?16:1024,keys=Array.from({length:nkeys+1},(_,id)=>({id}));
  const calls=mods.map(({[kind]:lib})=>{
   const c=create(lib,kind,{GetHashCode:k=>collision?0:k.id,Equals:(a,b)=>a.id===b.id});
   for(let i=0;i<nkeys;i++)kind==='map'?c.set(keys[i],i):c.add(keys[i]);
   let i=0;return ()=>{const j=(i++)%nkeys,k=keys[j];if(workload==='hit')return +c.has(k);if(workload==='miss')return +c.has(keys[nkeys]);if(workload==='update'){kind==='map'?c.set(k,j):c.add(k);return 1;}const result=c.delete(k);kind==='map'?c.set(k,j):c.add(k);return +result;};
  });
  function sample(fn,n){let sum=0;const t=performance.now();for(let i=0;i<n;i++)sum+=fn();return {ns:(performance.now()-t)*1e6/n,sum};}
  for(const fn of calls)sample(fn,20000);
  const n=Math.min(2000000,Math.max(1000,Math.ceil(12e6/sample(calls[0],20000).ns))),blocks=[];
  for(let block=0;block<11;block++){global.gc?.();const order=block%2?[1,0,0,1]:[0,1,1,0],samples=[[],[]];for(const v of order)samples[v].push(sample(calls[v],n));assert.equal(samples[0][0].sum,samples[1][0].sum);const means=samples.map(s=>(s[0].ns+s[1].ns)/2);blocks.push({samples,changePercent:(means[1]/means[0]-1)*100});}
  const sorted=blocks.map(b=>b.changePercent).sort((a,b)=>a-b);results.timings.push({kind,collision,nkeys,workload,iterations:n,medianChangePercent:sorted[5],blocks});
 }
}
fs.writeFileSync(new URL(process.argv.includes('--check-only')?'hash-correctness.json':(process.argv[2]??'hash-results.json'),import.meta.url),JSON.stringify(results,null,2));
console.log(JSON.stringify({correctnessChecks:results.correctness.length,timings:results.timings.map(({blocks,...x})=>x)},null,2));
