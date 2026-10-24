import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL,fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
const here=path.dirname(fileURLToPath(import.meta.url));
const {Record}=await import(pathToFileURL(path.resolve(here,'../../../../temp/runtime-hashing/full-runtime/Types.js')));
class Plain extends Record{constructor(N,S){super();this.N=N;this.S=S;}}
const baseline=(a,b)=>a.Equals(b);
const candidate=(a,b)=>a===b || (a==null ? b==null : b!=null && Object.getPrototypeOf(b)===Plain.prototype && a.N===b.N && a.S===b.S);
const cases={};
for(const mode of ['equal','unequalFirst','unequalLast','same']){
 cases[mode]=Array.from({length:256},(_,i)=>{const a=new Plain(i,`s${i}`);return [a,mode==='same'?a:mode==='null'?null:new Plain(mode==='unequalFirst'?i+1:i,mode==='unequalLast'?`different${i}`:`s${i}`)];});
}
function sample(fn,values,ms){let calls=0,sum=0;const begin=performance.now();let elapsed=0;do{for(let i=0;i<8192;i++){const pair=values[i&255];sum+=fn(pair[0],pair[1])?1:0;}calls+=8192;elapsed=performance.now()-begin;}while(elapsed<ms);return {ns:elapsed*1e6/calls,calls,sum};}
const results={};
for(const [name,values] of Object.entries(cases)){
 for(const [a,b] of values)assert.equal(baseline(a,b),candidate(a,b));
 sample(baseline,values,50);sample(candidate,values,50);
 const blocks=[];
 for(let i=0;i<9;i++){
  const order=i%2?['candidate','baseline','baseline','candidate']:['baseline','candidate','candidate','baseline'];
  const samples=order.map(variant=>({variant,...sample(variant==='baseline'?baseline:candidate,values,25)}));
  for(const s of samples)assert.equal(s.sum/s.calls,['same','equal'].includes(name)?1:0);
  const mean=v=>samples.filter(s=>s.variant===v).reduce((sum,s)=>sum+s.ns,0)/2;
  blocks.push({samples,changePercent:(mean('candidate')/mean('baseline')-1)*100});
 }
 const changes=blocks.map(b=>b.changePercent).sort((a,b)=>a-b);
 results[name]={medianChangePercent:changes[4],blocks};
}
fs.writeFileSync(path.join(here,'records-timing-results.json'),JSON.stringify({node:process.version,results},null,2));
console.log(JSON.stringify(Object.fromEntries(Object.entries(results).map(([k,v])=>[k,v.medianChangePercent])),null,2));
