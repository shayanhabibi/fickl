import assert from 'node:assert/strict';
import fs from 'node:fs';
import * as B from './isolated/runtime/Seq.baseline.js';
import * as C from './isolated/runtime/Seq.candidate.js';
import {some} from './isolated/runtime/Option.js';
import {ofArray} from './isolated/runtime/List.js';
const move='System.Collections.IEnumerator.MoveNext',current='System.Collections.Generic.IEnumerator`1.get_Current';
let checks=0;
const check=(a,b)=>{assert.deepEqual(a,b);checks++;};
const errors={open:new Error('open'),move:new Error('move'),current:new Error('current'),predicate:new Error('predicate'),dispose:new Error('dispose')};
function scenario(S,mode) {
 const trace=[], output=[];let opens=0;
 const source={GetEnumerator(){trace.push('open');opens++;if(mode==='throw-open')throw errors.open;let i=-1;return {
  [move](){trace.push(`move:${i+1}`);if(mode==='throw-move'&&i===1)throw errors.move;i++;return i<5;},
  [current](){trace.push(`current:${i}`);if(mode==='throw-current'&&i===2)throw errors.current;return i;},
  Dispose(){trace.push('dispose');if(mode==='throw-dispose')throw errors.dispose;}
 };}};
 const pred=x=>{trace.push(`predicate:${x}`);if(mode==='throw-predicate'&&x===2)throw errors.predicate;return mode==='none'?false:mode==='all'?true:x%2===0;};
 const seq=S.filter(pred,source); trace.push('constructed');
 try {
  if(mode==='unopened')return {trace,output,opens};
  if(mode==='short') {output.push(S.exists(x=>x===2,seq));}
  else {
   for(let round=0;round<(mode==='twice'?2:1);round++){
    const e=seq.GetEnumerator();
    if(mode==='dispose-before-move'){e.Dispose();e.Dispose();output.push(e[move]());continue;}
    if(mode==='current-before'){try{e[current]();}catch(ex){trace.push(`before:${ex.message}`);}}
    try{while(e[move]()){output.push(e[current]());if(mode==='break')break;}}
    finally{e.Dispose();}
    if(mode==='after-end'){output.push(e[move]());try{e[current]();}catch(ex){trace.push(`after:${ex.message}`);}}
   }
  }
 }catch(ex){trace.push(`error:${ex.message}`);assert.equal(ex,errors[ex.message]);checks++;}
 return {trace,output,opens};
}
const scenarios={};
for(const mode of ['unopened','all','none','mixed','twice','short','break','current-before','after-end','dispose-before-move','throw-open','throw-move','throw-current','throw-predicate','throw-dispose']) {
 const baseline=scenario(B,mode),candidate=scenario(C,mode);check(candidate,baseline);scenarios[mode]=baseline;
}
for(let n=0;n<100;n++)for(let k=1;k<=8;k++){
 const xs=Array.from({length:n},(_,i)=>i-30);const pred=x=>x%k===0;
 check(Array.from(C.filter(pred,xs)),Array.from(B.filter(pred,xs)));check(Array.from(C.filter(pred,xs)),xs.filter(pred));
}
const values=[null,undefined,some(null),some(undefined),some(some(null)),0,false,'',NaN,{x:1}];
for(const accept of [true,false]){const a=Array.from(B.filter(()=>accept,values)),b=Array.from(C.filter(()=>accept,values));check(a,b);if(accept)for(let i=0;i<values.length;i++){assert.equal(b[i],values[i]);checks++;}}
for(const S of [B,C]){let count=0;const xs=[1,2,3];const seq=S.filter(x=>{count++;if(x===1)xs[1]=20;return true;},xs);check(count,0);check(Array.from(seq),[1,20,3]);check(count,3);}
for(const S of [B,C]){const seq=S.filter(()=>true,null);let threw=false;try{seq.GetEnumerator();}catch{threw=true;}check(threw,true);}
for(const source of [ofArray([1,2,3]),new Int32Array([1,2,3]),{*[Symbol.iterator](){yield 1;yield 2;yield 3;}},[1,,3]]) {
 check(Array.from(C.filter(x=>x!==2,source)),Array.from(B.filter(x=>x!==2,source)));
}
const result={checks,scenarios};fs.writeFileSync(new URL('./seq-correctness.json',import.meta.url),JSON.stringify(result,null,2));console.log(JSON.stringify({checks,scenarios:Object.keys(scenarios)}));
