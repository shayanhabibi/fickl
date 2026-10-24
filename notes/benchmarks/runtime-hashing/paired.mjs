import {readFileSync,writeFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {pathToFileURL} from 'node:url';
import {performance} from 'node:perf_hooks';
const [directory,variant,output]=process.argv.slice(2);
async function fixtures(name) {
  const base=resolve(directory,name,'js');
  const {structuralHash,equals}=await import(pathToFileURL(`${base}/Util.js`));
  const {Record}=await import(pathToFileURL(`${base}/Types.js`));
  class R extends Record {constructor(values){super();for(let i=0;i<values.length;i++)this[`f${i}`]=values[i];}}
  const cases=new Map();
  const add=(name,pool)=>cases.set(name,i=>structuralHash(pool[i&31]));
  add('control-number',Array.from({length:32},(_,i)=>i+1));
  for(const n of [0,1,4,32,1024])if(variant==='array'||n===32)
    add(`array-${n}`,Array.from({length:32},(_,j)=>Array.from({length:n},(_,i)=>i+j)));
  if(variant==='array') {
    add('array-mixed',Array.from({length:32},(_,j)=>Array.from({length:64},(_,i)=>[j,NaN,null,'text',true,42n][i%6])));
    add('typed-float64',Array.from({length:32},(_,j)=>Float64Array.from({length:64},(_,i)=>j+i/3)));
    add('array-custom-hash',Array.from({length:32},(_,j)=>Array.from({length:32},(_,i)=>({GetHashCode(){return i+j;}}))));
  } else {
    for(const n of [4,16,64])add(`${variant}-${n}`,Array.from({length:32},(_,j)=>{
      const values=Array.from({length:n},(_,i)=>i+j);
      return variant==='record'?new R(values):Object.fromEntries(values.map((v,i)=>[`f${i}`,v]));
    }));
    const make=values=>variant==='record'?new R(values):Object.fromEntries(values.map((v,i)=>[`f${i}`,v]));
    add('nested',Array.from({length:32},(_,j)=>make([[j,1,2,3],make([j,'text',true])])));
    const keys=Array.from({length:256},(_,i)=>make([i,`key-${i}`,i%7]));
    const probes=Array.from({length:256},(_,i)=>make([i,`key-${i}`,i%7]));
    const buckets=new Map();
    for(let i=0;i<keys.length;i++){const hash=structuralHash(keys[i]),bucket=buckets.get(hash)??[];bucket.push([keys[i],i]);buckets.set(hash,bucket);}
    cases.set('hash-lookup',i=>{const key=probes[i&255];for(const [k,v] of buckets.get(structuralHash(key)))if(equals(k,key))return v;throw Error('lookup failed');});
  }
  return cases;
}
const baseline=await fixtures('baseline'),candidate=await fixtures(variant);
let sink=0;
function batch(run,n){let sum=0;for(let i=0;i<n;i++)sum=(sum+run(i))|0;sink^=sum;return sum;}
const median=xs=>[...xs].sort((a,b)=>a-b)[Math.floor(xs.length/2)];
const results=[];
for(const [name,b] of baseline) {
  const c=candidate.get(name);
  for(const run of [b,c]){const until=performance.now()+50;do{batch(run,512);}while(performance.now()<until);}
  let n=1024,ms;
  do{const start=performance.now();batch(b,n);ms=performance.now()-start;if(ms<20)n*=2;}while(ms<20);
  n=Math.max(1024,Math.min(20_000_000,Math.round(n*25/ms)));
  const rounds=[];
  for(let round=0;round<11;round++) {
    globalThis.gc?.();
    const order=round%2?['candidate','baseline','baseline','candidate']:['baseline','candidate','candidate','baseline'];
    const samples={baseline:[],candidate:[]};let expected;
    for(const label of order) {
      const start=performance.now(),sum=batch(label==='baseline'?b:c,n),elapsed=performance.now()-start;
      if(expected!==undefined&&sum!==expected)throw Error(`checksum mismatch ${name}`);expected=sum;
      samples[label].push(elapsed*1e6/n);
    }
    const bns=(samples.baseline[0]+samples.baseline[1])/2,cns=(samples.candidate[0]+samples.candidate[1])/2;
    rounds.push({baseline_ns:bns,candidate_ns:cns,ratio:cns/bns,checksum:expected});
  }
  const result={name,iterations:n,baseline_ns:median(rounds.map(r=>r.baseline_ns)),candidate_ns:median(rounds.map(r=>r.candidate_ns)),
    median_paired_change_percent:100*(median(rounds.map(r=>r.ratio))-1),candidate_wins:rounds.filter(r=>r.ratio<1).length,rounds};
  results.push(result);
}
writeFileSync(output,JSON.stringify({variant,node:process.version,v8:process.versions.v8,method:'11 alternating ABBA/BAAB blocks; 2 samples per variant per block',results,sink},null,2));
console.log(JSON.stringify(results.map(({name,baseline_ns,candidate_ns,median_paired_change_percent,candidate_wins})=>({name,baseline_ns,candidate_ns,median_paired_change_percent,candidate_wins})),null,2));
