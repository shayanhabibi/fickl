import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { stripTypeScriptTypes } from 'node:module';
import { createHash } from 'node:crypto';
import { performance } from 'node:perf_hooks';
import assert from 'node:assert/strict';

const here=dirname(fileURLToPath(import.meta.url));
const source=resolve(here,'../../../../../../src/fable-library-ts/String.ts');
// Source path may be supplied when using an alternative worktree layout.
const sourcePath=process.env.FABLE_STRING_SOURCE ?? source;
const text=readFileSync(sourcePath,'utf8');
const baseline=text.match(/export function filter\(pred: \(char: string\) => boolean, x: string\) \{[\s\S]*?\n\}/)?.[0];
assert.ok(baseline,'Expected actual String.filter implementation');
const candidate=`export function filter(pred: (char: string) => boolean, x: string) {
  // Keep legacy behavior for null and non-primitive interop values.
  if (typeof x !== "string") {
    return (x as string).split("").filter((c) => pred(c)).join("");
  }
  let result = "";
  for (let i = 0; i < x.length; i++) {
    const c = x[i];
    if (pred(c)) { result += c; }
  }
  return result;
}`;
const dest=resolve(here,'../../../../temp/runtime-string-helpers');
mkdirSync(dest,{recursive:true});
for(const [name,code] of Object.entries({baseline,candidate})) {
  writeFileSync(resolve(dest,`${name}.ts`),code);
  writeFileSync(resolve(dest,`${name}.mjs`),stripTypeScriptTypes(code));
}
writeFileSync(resolve(here,'candidate.ts'),candidate+'\n');
const b=(await import(pathToFileURL(resolve(dest,'baseline.mjs')))).filter;
const c=(await import(pathToFileURL(resolve(dest,'candidate.mjs')))).filter;
const mode=process.argv[2] ?? 'check';
const metadata={source:sourcePath,sha256:createHash('sha256').update(text).digest('hex'),node:process.version,v8:process.versions.v8};
let checks=0;
if(mode==='check') {
  let seed=912740;
  const next=()=>{seed=(Math.imul(seed,1664525)+1013904223)|0;return seed>>>0;};
  const inputs=['','a','a\0b','😀𐐀','\ud800x\udfff','\r\n','e\u0301','漢字'];
  for(let j=0;j<1200;j++)inputs.push(String.fromCharCode(...Array.from({length:next()%128},()=>next()%65536)));
  for(const input of inputs)for(const behavior of ['all','none','even','stateful','throw']) {
    function run(fn){const trace=[];let i=0;try{return {value:fn(ch=>{trace.push(ch.charCodeAt(0));i++;if(behavior==='throw'&&i===3)throw Error('predicate');return behavior==='all'||(behavior==='even'&&ch.charCodeAt(0)%2===0)||(behavior==='stateful'&&i%3===0);},input),trace};}catch(e){return {error:[e.constructor.name,e.message],trace};}}
    assert.deepEqual(run(c),run(b));checks++;
  }
  for(const input of [null,undefined,new String('abc')]) {
    function run(fn){try{return {value:fn(()=>true,input)};}catch(e){return {error:[e.constructor.name,e.message]};}}
    assert.deepEqual(run(c),run(b));checks++;
  }
  const halves=[];assert.equal(c(ch=>{halves.push(ch.charCodeAt(0));return true;},'😀'),'😀');
  assert.deepEqual(halves,[0xd83d,0xde00]);checks++;
  writeFileSync(resolve(here,'correctness.json'),JSON.stringify({...metadata,checks,status:'passed',utf16Trace:halves},null,2));
  console.log(`PASS: ${checks} String.filter differential checks`);
} else if(mode==='bench') {
  let sink=0;
  const batch=(fn,input,pred,n)=>{let sum=0;for(let i=0;i<n;i++){const s=fn(pred,input);sum=(sum+s.length+(s.charCodeAt(s.length>>>1)||0))|0;}sink^=sum;return sum;};
  const median=xs=>[...xs].sort((a,b)=>a-b)[xs.length>>>1];
  const results=[];
  for(const length of [0,8,64,1024,16384])for(const selection of ['all','half','none']) {
    const input='a😀b漢字\0'.repeat(Math.ceil(length/7)).slice(0,length);
    const pred=selection==='all'?()=>true:selection==='none'?()=>false:ch=>ch.charCodeAt(0)%2===0;
    for(const fn of [b,c]){const until=performance.now()+30;do{batch(fn,input,pred,64);}while(performance.now()<until);}
    let n=64,elapsed;do{const start=performance.now();batch(b,input,pred,n);elapsed=performance.now()-start;if(elapsed<15)n*=2;}while(elapsed<15);
    n=Math.max(1,Math.round(n*20/elapsed));
    const rounds=[];
    for(let r=0;r<9;r++){
      globalThis.gc?.();const samples={baseline:[],candidate:[]};let expected;
      for(const label of r%2?['candidate','baseline','baseline','candidate']:['baseline','candidate','candidate','baseline']){
        const start=performance.now(),sum=batch(label==='baseline'?b:c,input,pred,n),ns=(performance.now()-start)*1e6/n;
        if(expected!==undefined)assert.equal(sum,expected);expected=sum;samples[label].push(ns);
      }
      const baseline_ns=(samples.baseline[0]+samples.baseline[1])/2,candidate_ns=(samples.candidate[0]+samples.candidate[1])/2;
      rounds.push({baseline_ns,candidate_ns,ratio:candidate_ns/baseline_ns});
    }
    results.push({length,selection,iterations:n,baseline_ns:median(rounds.map(x=>x.baseline_ns)),candidate_ns:median(rounds.map(x=>x.candidate_ns)),paired_change_percent:100*(median(rounds.map(x=>x.ratio))-1),wins:rounds.filter(x=>x.ratio<1).length,rounds});
  }
  writeFileSync(resolve(here,process.argv[3]??'timings.json'),JSON.stringify({...metadata,method:'9 alternating ABBA/BAAB blocks; output length and middle code unit consumed',results,sink},null,2));
  console.log(JSON.stringify(results.map(({length,selection,paired_change_percent,wins})=>({length,selection,paired_change_percent,wins})),null,2));
} else throw Error('Expected check or bench');
