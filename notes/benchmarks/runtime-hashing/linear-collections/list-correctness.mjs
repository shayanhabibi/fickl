import assert from 'node:assert/strict';
import fs from 'node:fs';
import * as B from './isolated/runtime/List.baseline.js';
import * as C from './isolated/runtime/List.candidate.js';
import * as BC from './isolated/runtime/List.baseline.counted.js';
import * as CC from './isolated/runtime/List.candidate.counted.js';
import {collect} from './isolated/runtime/Array.js';
let checks=0;
const check=(a,b)=>{assert.deepEqual(a,b); checks++;};
for(let n=0;n<=150;n++) for(const m of [0,1,5,50]) {
    const objects=Array.from({length:n},(_,i)=>({i}));
    const rightObjects=Array.from({length:m},(_,i)=>({right:i}));
    const left=B.ofArray(objects), right=B.ofArray(rightObjects);
    const before=Array.from(left), beforeRight=Array.from(right);
    const a=B.append(left,right), b=C.append(left,right);
    check(Array.from(a),Array.from(b)); check(Array.from(b),objects.concat(rightObjects));
    let tail=b; for(let i=0;i<n;i++){assert.equal(tail.head,objects[i]); checks++; tail=tail.tail;}
    assert.equal(tail,right); checks++; check(Array.from(left),before); check(Array.from(right),beforeRight);
    if(n===0){assert.equal(b,right);checks++;}
}
const allocations=[];
for(const n of [0,1,8,128,4096]) {
    const xs=B.ofArray(Array.from({length:n},(_,i)=>i)), ys=B.ofArray([123]);
    globalThis.listAllocations=0; BC.append(xs,ys); const baseline=globalThis.listAllocations;
    globalThis.listAllocations=0; CC.append(xs,ys); const candidate=globalThis.listAllocations;
    check(baseline,2*n+1); check(candidate,n+1); allocations.push({n,baseline,candidate});
}
function arrayCase(fn) {
    const shared=[0], events=[];
    const result=fn(x=>{events.push(x);shared[0]=x;return shared;},[1,2]);
    return {result:Array.from(result),events};
}
const streaming=(f,xs)=>{const out=[];for(const x of xs)for(const y of f(x))out.push(y);return out;};
const arrayBaseline=arrayCase(collect), arrayRejected=arrayCase(streaming);
check(arrayBaseline.result,[2,2]);check(arrayRejected.result,[1,2]);check(arrayBaseline.events,arrayRejected.events);
const result={checks,allocations,arrayCollectRejected:{baseline:arrayBaseline,streaming:arrayRejected}};
fs.writeFileSync(new URL('./list-correctness.json',import.meta.url),JSON.stringify(result,null,2));console.log(JSON.stringify(result));
