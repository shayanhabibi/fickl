import assert from 'node:assert/strict';
import fs from 'node:fs';
import {performance} from 'node:perf_hooks';
const root = new URL('./temp/ordered/', import.meta.url);
const mods = await Promise.all(['baseline','candidate'].map(async v => ({map:await import(new URL(v+'/Map.js',root)),set:await import(new URL(v+'/Set.js',root))})));
const results = {node:process.version, platform:process.platform, correctness:{}, timings:[]};
function scenario(lib, kind) {
  const trace=[]; const comparer={Compare(a,b){trace.push([a,b]);return b-a;}};
  let m=lib.ofArray(kind==='map'?[[3,'a'],[1,'b'],[2,'c']]:[3,1,2],comparer);
  const snapshots=[];
  for(let i=0;i<300;i++) {
    const old=m, before=Array.from(old), oldTree=old.tree;
    const n=(i*97)%37;
    m=i%3===0?lib.remove(n,m):kind==='map'?lib.add(n,i,m):lib.add(n,m);
    const mark=trace.length;
    assert.equal(lib.count(m),Array.from(m).length);
    assert.equal(lib.count(m),Array.from(m).length);
    assert.equal(trace.length,mark);
    assert.deepEqual(Array.from(old),before); assert.equal(old.tree,oldTree);
    snapshots.push([Array.from(m),lib.count(old)]);
  }
  const it=m[Symbol.iterator](); const head=it.next(); lib.count(m); const rest=Array.from({[Symbol.iterator]:()=>it});
  assert.deepEqual([head.value,...rest],Array.from(m));
  // Count neither compares nor hashes, even with throwing comparator after construction.
  const throwing={Compare(){throw Error('unexpected compare')}};
  const one=lib.ofArray(kind==='map'?[[1,2]]:[1],throwing);
  assert.equal(lib.count(one),1);assert.equal(lib.count(one),1);
  return {snapshots,trace};
}
for(const kind of ['map','set']) {
 const a=scenario(mods[0][kind],kind),b=scenario(mods[1][kind],kind);
 assert.deepEqual(a,b);results.correctness[kind]={operations:300, traceCalls:a.trace.length, matched:true};
}
results.interopCounterexamples=[];
for(const kind of ['map','set']) {
 const effects=mods.map(({[kind]:lib})=>{
  const cmp={Compare:(a,b)=>a-b};const one=lib.ofArray(kind==='map'?[[1,1]]:[1],cmp);
  const fields=Object.keys(one);Object.freeze(one);let frozenCount;
  try {frozenCount=lib.count(one);} catch(e){frozenCount=e.constructor.name;}
  const a=lib.ofArray(kind==='map'?[[1,1]]:[1],cmp), b=lib.ofArray(kind==='map'?[[1,1],[2,2]]:[1,2],cmp);
  lib.count(a);a.tree=b.tree;
  return {fields,frozenCount,mutatedTreeCount:lib.count(a)};
 });
 results.interopCounterexamples.push({kind,baseline:effects[0],candidate:effects[1]});
}
if(!process.argv.includes('--check-only')) {
 const cmp={Compare:(a,b)=>a-b};
 for(const kind of ['map','set']) for(const size of [0,1,32,1024]) for(const workload of ['repeat-count','fresh-wrapper-count','update-count','update-only']) {
   const inputs=Array.from({length:size},(_,i)=>kind==='map'?[i,i]:i);
   const states=mods.map(m=>({lib:m[kind],value:m[kind].ofArray(inputs,cmp)}));
   const calls=states.map(({lib,value})=>{
     if(workload==='repeat-count')return ()=>lib.count(value);
     const Class=kind==='map'?lib.FSharpMap:lib.FSharpSet;
     if(workload==='fresh-wrapper-count')return ()=>lib.count(new Class(cmp,value.tree));
     return ()=>{const v=kind==='map'?lib.add(size+1,2,value):lib.add(size+1,value);return workload==='update-count'?lib.count(v):v.tree.h??1;};
   });
   function sample(fn,iterations){let sum=0;const start=performance.now();for(let i=0;i<iterations;i++)sum+=fn();return {ns:(performance.now()-start)*1e6/iterations,sum};}
   for(const fn of calls) sample(fn,10000);
   const probe=sample(calls[0],10000);const n=Math.min(2000000,Math.max(1000,Math.ceil(12e6/probe.ns)));
   const blocks=[];
   for(let block=0;block<9;block++) {global.gc?.();const order=block%2?[1,0,0,1]:[0,1,1,0],samples=[[],[]];for(const v of order)samples[v].push(sample(calls[v],n));assert.equal(samples[0][0].sum,samples[1][0].sum);const means=samples.map(s=>(s[0].ns+s[1].ns)/2);blocks.push({samples,changePercent:(means[1]/means[0]-1)*100});}
   const sorted=blocks.map(b=>b.changePercent).sort((a,b)=>a-b);
   results.timings.push({kind,size,workload,iterations:n,medianChangePercent:sorted[4],blocks});
 }
}
fs.writeFileSync(new URL(process.argv.includes('--check-only')?'ordered-correctness.json':'ordered-results.json',import.meta.url),JSON.stringify(results,null,2));
console.log(JSON.stringify({correctness:results.correctness,timings:results.timings.map(({blocks,...x})=>x)},null,2));
