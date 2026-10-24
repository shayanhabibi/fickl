import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL,fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
const here=path.dirname(fileURLToPath(import.meta.url));
const runtime=path.resolve(here,'../../../../temp/runtime-hashing/full-runtime');
const {Record}=await import(pathToFileURL(path.join(runtime,'Types.js')));
const {equals}=await import(pathToFileURL(path.join(runtime,'Util.js')));
class Plain extends Record{constructor(N,S){super();this.N=N;this.S=S;}}
class Nested extends Record{constructor(P,Flag){super();this.P=P;this.Flag=Flag;}}
class Other extends Record{constructor(N,S){super();this.N=N;this.S=S;}}
const baseline=(a,b)=>equals(a,b);
// Intended post-evaluation callsite emission for an eligible static type.
const candidate=(a,b)=>a===b || equals(a,b);
let checks=0;
const values=[null,undefined,new Plain(1,'a'),new Plain(1,'a'),new Plain(2,'a'),new Other(1,'a'),new Nested(new Plain(1,'a'),true),new Nested(new Plain(1,'a'),true)];
for(const a of values)for(const b of values){assert.equal(candidate(a,b),baseline(a,b));checks++;}
// Real emission must use temporaries, not duplicate an arbitrary operand expression.
const effects=[];
for(const throwing of ['none','left','right']){
 const runs=[];
 for(const fn of [baseline,candidate]){
  const trace=[];const value=new Plain(1,'a');
  function operand(side){trace.push(side);if(throwing===side)throw new Error(side);return value;}
  let result;try{const left=operand('left');const right=operand('right');result=fn(left,right);}catch(e){result=e.message;}
  runs.push({trace,result});
 }
 assert.deepEqual(runs[0],runs[1]);checks++;effects.push({throwing,runs});
}
// Mutation is not itself disqualifying for an identity-positive reflexive primitive record.
const mutable=new Plain(1,'a');mutable.N=2;assert.equal(candidate(mutable,mutable),true);checks++;
const output={checks,effects};
if(process.argv.includes('--timing')){
 function sample(fn,values,ms){let calls=0,sum=0;const begin=performance.now();let elapsed;do{for(let i=0;i<8192;i++){const [a,b]=values[i&255];sum+=fn(a,b)?1:0;}calls+=8192;elapsed=performance.now()-begin;}while(elapsed<ms);return {ns:elapsed*1e6/calls,calls,sum};}
 output.node=process.version;output.timings={};
 for(const mode of ['same','mixed','distinctEqual','distinctUnequal','nestedSame','nestedDistinct']){
  const values=Array.from({length:256},(_,i)=>{let a=new Plain(i,`s${i}`),b=new Plain(mode==='distinctUnequal'?i+1:i,`s${i}`);if(mode.startsWith('nested')){a=new Nested(a,true);b=new Nested(b,true);}if(mode==='same'||mode==='nestedSame'||mode==='mixed'&&i%2===0)b=a;return [a,b];});
  sample(baseline,values,50);sample(candidate,values,50);const blocks=[];
  for(let i=0;i<9;i++){
   const order=i%2?['candidate','baseline','baseline','candidate']:['baseline','candidate','candidate','baseline'];
   const samples=order.map(variant=>({variant,...sample(variant==='baseline'?baseline:candidate,values,25)}));
   for(const s of samples)assert.equal(s.sum/s.calls,mode==='distinctUnequal'?0:1);
   const mean=v=>samples.filter(s=>s.variant===v).reduce((sum,s)=>sum+s.ns,0)/2;
   blocks.push({samples,changePercent:(mean('candidate')/mean('baseline')-1)*100});
  }
  output.timings[mode]={medianChangePercent:blocks.map(x=>x.changePercent).sort((a,b)=>a-b)[4],blocks};
 }
}
fs.writeFileSync(path.join(here,process.argv.includes('--timing')?'callsite-timing-results.json':'callsite-results.json'),JSON.stringify(output,null,2));
console.log(JSON.stringify({checks,timings:output.timings&&Object.fromEntries(Object.entries(output.timings).map(([k,v])=>[k,v.medianChangePercent]))},null,2));
