import {readFileSync} from 'node:fs';
import {resolve} from 'node:path';
import {pathToFileURL} from 'node:url';
import {performance} from 'node:perf_hooks';
const [directory,variant,configPath]=process.argv.slice(2);
const base=resolve(directory,variant,'js');
const {structuralHash,equals}=await import(pathToFileURL(`${base}/Util.js`));
const {Record}=await import(pathToFileURL(`${base}/Types.js`));
class R extends Record {constructor(values){super();for(let i=0;i<values.length;i++)this[`f${i}`]=values[i];}}
const cases=[];
for(const n of [0,1,4,32,1024]) {
  const pool=Array.from({length:32},(_,j)=>Array.from({length:n},(_,i)=>i+j));
  cases.push({name:`array-${n}`,run:i=>structuralHash(pool[i&31])});
}
for(const kind of ['record','object'])for(const n of [4,16,64]) {
  const pool=Array.from({length:32},(_,j)=>{
    const values=Array.from({length:n},(_,i)=>i+j);
    return kind==='record'?new R(values):Object.fromEntries(values.map((v,i)=>[`f${i}`,v]));
  });
  cases.push({name:`${kind}-${n}`,run:i=>structuralHash(pool[i&31])});
}
const nested=Array.from({length:32},(_,j)=>new R([
  {a:[j,1,2,3],b:[4,5,6,7]},new R([j,'nested',true,[8,9,10]])
]));
cases.push({name:'nested',run:i=>structuralHash(nested[i&31])});
// Hash-bucket lookup using the actual runtime hash and equality, not native Map identity.
const keys=Array.from({length:256},(_,i)=>new R([i,`key-${i}`,i%7]));
const probes=Array.from({length:256},(_,i)=>new R([i,`key-${i}`,i%7]));
const buckets=new Map();
for(let i=0;i<keys.length;i++) {
  const hash=structuralHash(keys[i]), bucket=buckets.get(hash)??[];
  bucket.push([keys[i],i]);buckets.set(hash,bucket);
}
cases.push({name:'hash-lookup',run:i=>{
  const key=probes[i&255],bucket=buckets.get(structuralHash(key));
  for(const [k,v] of bucket)if(equals(k,key))return v;
  throw new Error('lookup failed');
}});
let sink=0;
function batch(run,count) {let sum=0;for(let i=0;i<count;i++)sum=(sum+run(i))|0;sink^=sum;return sum;}
function warm(run) {const until=performance.now()+25;do{batch(run,256);}while(performance.now()<until);}
if(configPath==='--calibrate') {
  const config={};
  for(const {name,run} of cases) {
    warm(run);let count=1024,elapsed;
    do{const start=performance.now();batch(run,count);elapsed=performance.now()-start;if(elapsed<20)count*=2;}while(elapsed<20);
    config[name]=Math.min(20_000_000,Math.max(1024,Math.round(count*100/elapsed)));
  }
  console.log(JSON.stringify(config));
} else {
  const config=JSON.parse(readFileSync(configPath,'utf8'));
  const results=[];
  for(const {name,run} of cases) {
    warm(run);globalThis.gc?.();
    const count=config[name], start=performance.now(),checksum=batch(run,count),ms=performance.now()-start;
    results.push({name,count,ms,nsPerOp:ms*1e6/count,checksum});
  }
  console.log(JSON.stringify({variant,node:process.version,v8:process.versions.v8,results,sink}));
}
