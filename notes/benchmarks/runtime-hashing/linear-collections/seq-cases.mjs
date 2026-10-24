import * as B from './isolated/runtime/Seq.baseline.js';
import * as C from './isolated/runtime/Seq.candidate.js';
import {ofArray} from './isolated/runtime/List.js';
const consume=seq=>{let sum=0;for(const x of seq)sum+=x;return sum;};
export const cases=[];
for(const n of [32,1024])for(const type of ['array','list','generator'])for(const every of [1,10]){
 const values=Array.from({length:n},(_,i)=>i);
 const input=type==='array'?values:type==='list'?ofArray(values):{*[Symbol.iterator](){yield* values;}};
 const predicate=x=>x%every===0;
 cases.push({name:`filter-${type}-${n}-every${every}`,baseline:()=>consume(B.filter(predicate,input)),candidate:()=>consume(C.filter(predicate,input)),checksum:x=>x});
}
const nullable=Array.from({length:1024},(_,i)=>i%2?null:undefined);
cases.push({name:'filter-null-options',baseline:()=>Array.from(B.filter(()=>true,nullable)).length,candidate:()=>Array.from(C.filter(()=>true,nullable)).length,checksum:x=>x});
cases.push({name:'filter-exists-short',baseline:()=>B.exists(x=>x>=4,B.filter(x=>x%2===0,[0,1,2,3,4,5])),candidate:()=>C.exists(x=>x>=4,C.filter(x=>x%2===0,[0,1,2,3,4,5])),checksum:x=>x});
