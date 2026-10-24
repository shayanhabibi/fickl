import assert from 'node:assert/strict';
import fs from 'node:fs';
import {performance} from 'node:perf_hooks';
import * as baseline from './temp/baseline/StructFixture.js';
import * as candidate from './temp/candidate/StructFixture.js';
const results={node:process.version,correctness:{},timings:[]};
const expected=[1,9,7,7,4,5,6,8,9];
const observations=[baseline,candidate].map(m=>Array.from(m.observe()));
assert.deepEqual(observations[0],observations[1]);
results.correctness.structObservations={dotnet:expected,baseline:observations[0],candidate:observations[1],fullConformance:JSON.stringify(expected)===JSON.stringify(observations[0])};
const reference=(n,seed)=>{let sum=seed;for(let i=0;i<n;i++){const x=(sum+i)|0,y=(sum-i)|0,q=(y+3)|0;sum=((q^x)+y)|0;}return sum|0;};
const dotnet=JSON.parse(fs.readFileSync(new URL('./dotnet-results.json',import.meta.url))).stdout;
const kernelLines=[...dotnet.matchAll(/KERNEL=(\d+),(-?\d+)/g)];
assert.equal(kernelLines.length,5);
for(const [,n,value]of kernelLines){assert.equal(baseline.localKernel(+n,123),+value);assert.equal(candidate.localKernel(+n,123),+value);}
results.correctness.dotnetKernelCases=kernelLines.length;
let checks=0;
for(const n of [0,1,2,10,100,10000])for(const seed of [0,1,-1,123,2147483647,-2147483648]){assert.equal(baseline.localKernel(n,seed),candidate.localKernel(n,seed));assert.equal(candidate.localKernel(n,seed),reference(n,seed));checks++;}
let seed=71;
for(let i=0;i<1000;i++){seed=(Math.imul(seed,1664525)+1013904223)|0;const n=(seed>>>0)%1000;assert.equal(baseline.localKernel(n,seed),candidate.localKernel(n,seed));checks++;}
results.correctness.kernelDifferentials=checks;
results.correctness.boundaries=[];
for(const [name,m]of [['baseline',baseline],['candidate',candidate]]) {
 const boxed=m.boxPoint(4),closure=m.closurePoint(8),p=closure();
 assert.ok(boxed instanceof m.Point);assert.equal(boxed.GetHashCode(),m.boxPoint(4).GetHashCode());assert.equal(m.interfaceValue(6),6);
 assert.deepEqual(Object.keys(boxed),['X','Y']);assert.equal(p.X,8);assert.equal(p.Y,9);
 // Scalar-only escaped replacement cannot preserve JS-visible field/prototype contract.
 const unsafe=4;const boundary={name,boxedFields:Object.keys(boxed),boxedIsPoint:boxed instanceof m.Point,boxedHash:boxed.GetHashCode(),closureFields:Object.keys(p),interfaceResult:m.interfaceValue(6),unsafeScalarHasX:unsafe.X!==undefined};
 results.correctness.boundaries.push(boundary);
}
if(!process.argv.includes('--check-only'))for(const n of [1,32,1024,10000]) {
 const fns=[baseline.localKernel,candidate.localKernel];
 function sample(fn,iterations){let sum=0;const start=performance.now();for(let i=0;i<iterations;i++)sum=(sum+fn(n,i))|0;return {ns:(performance.now()-start)*1e6/iterations,sum};}
 for(const fn of fns)sample(fn,10000);
 const iterations=Math.min(2000000,Math.max(500,Math.ceil(20e6/sample(fns[0],10000).ns))),blocks=[];
 for(let b=0;b<13;b++){global.gc?.();const order=b%2?[1,0,0,1]:[0,1,1,0],samples=[[],[]];for(const v of order)samples[v].push(sample(fns[v],iterations));assert.equal(samples[0][0].sum,samples[1][0].sum);const means=samples.map(s=>(s[0].ns+s[1].ns)/2);blocks.push({samples,changePercent:(means[1]/means[0]-1)*100});}
 const sorted=blocks.map(b=>b.changePercent).sort((a,b)=>a-b);results.timings.push({n,iterations,medianChangePercent:sorted[6],blocks});
}
fs.writeFileSync(new URL(process.argv.includes('--check-only')?'correctness.json':(process.argv[2]??'results.json'),import.meta.url),JSON.stringify(results,null,2));
console.log(JSON.stringify({correctness:results.correctness,timings:results.timings.map(({blocks,...rest})=>rest)},null,2));
