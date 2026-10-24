import fs from 'node:fs';
import {performance} from 'node:perf_hooks';
const mode=process.argv[2]??'list';
const output=process.argv[3]??`${mode}-timing.json`;
let cases;
if(mode==='list') {
 const B=await import('./isolated/runtime/List.baseline.js'), C=await import('./isolated/runtime/List.candidate.js');
 cases=[0,1,8,32,128,1024,8192].map(n=>{const x=B.ofArray(Array.from({length:n},(_,i)=>i)),y=B.ofArray([99]);return {name:`append-${n}`,baseline:()=>B.append(x,y),candidate:()=>C.append(x,y),checksum:r=>r.head};});
} else {cases=(await import('./seq-cases.mjs')).cases;}
let sink;
function run(fn,ms){let count=0;const start=performance.now();let end=start;do{for(let i=0;i<128;i++){sink=fn();count++;}end=performance.now();}while(end-start<ms);globalThis.escapeResult=sink;return {ns:(end-start)*1e6/count,count};}
const results=[];
for(const c of cases){
 const b=c.checksum(c.baseline()),v=c.checksum(c.candidate());if(b!==v)throw new Error(`checksum ${c.name}`);
 run(c.baseline,40);run(c.candidate,40);
 const blocks=[];
 for(let i=0;i<9;i++){globalThis.gc?.();const order=i%2?['candidate','baseline','baseline','candidate']:['baseline','candidate','candidate','baseline'];const samples=order.map(variant=>({variant,...run(c[variant],20)}));const avg=k=>samples.filter(x=>x.variant===k).reduce((a,x)=>a+x.ns,0)/2;blocks.push({samples,baselineNs:avg('baseline'),candidateNs:avg('candidate'),percent:(avg('candidate')/avg('baseline')-1)*100});}
 const median=a=>a.toSorted((a,b)=>a-b)[Math.floor(a.length/2)];
 results.push({name:c.name,checksum:b,medianPercent:median(blocks.map(x=>x.percent)),medianBaselineNs:median(blocks.map(x=>x.baselineNs)),medianCandidateNs:median(blocks.map(x=>x.candidateNs)),blocks});
}
fs.writeFileSync(new URL(output,import.meta.url),JSON.stringify({node:process.version,platform:process.platform,mode,results},null,2));console.log(JSON.stringify(results.map(({blocks,...x})=>x),null,2));
