import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL,fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
const here=path.dirname(fileURLToPath(import.meta.url));
const runtime=path.resolve(here,'../../../../temp/runtime-hashing/full-runtime');
const {Record}=await import(pathToFileURL(path.join(runtime,'Types.js')));
const {equals}=await import(pathToFileURL(path.join(runtime,'Util.js')));
class Plain extends Record {constructor(N,S){super();this.N=N;this.S=S;}}
class Other extends Record {constructor(N,S){super();this.N=N;this.S=S;}}
// Models an eligible typed F# equality callsite; nonnull left has exact Plain layout.
function scoped(a,b) {
  if(a===b)return true;
  if(a==null)return b==null;
  if(b==null || Object.getPrototypeOf(b)!==Plain.prototype)return false;
  if(a.N!==b.N)return false;
  const leftString=a.S, rightString=b.S;
  return leftString===rightString || (leftString==null && rightString==null);
}
const original=(a,b)=>a===b || (a==null?b==null:b!=null && Object.getPrototypeOf(b)===Plain.prototype && a.N===b.N && a.S===b.S);
const values=[null,undefined];
for(const n of [-2147483648,-1,-0,0,1,2147483647])
  for(const s of [null,undefined,'','a','😀','\ud800','a\0b'])values.push(new Plain(n,s));
let checks=0;
for(const a of values)for(const b of [...values,new Other(1,'a')]){assert.equal(scoped(a,b),equals(a,b));checks++;}
const nullish={baseline:equals(new Plain(1,null),new Plain(1,undefined)),original:original(new Plain(1,null),new Plain(1,undefined)),expanded:scoped(new Plain(1,null),new Plain(1,undefined))};
assert.deepEqual(nullish,{baseline:true,original:false,expanded:true});checks++;
const traces=[];
for(const [n,s,t] of [[1,null,undefined],[1,'a','b'],[2,'a','a']]) {
  const runs=[];
  for(const fn of [equals,scoped]){
    const trace=[];const make=(tag,N,S)=>Object.defineProperties(new Plain(N,S),{N:{enumerable:true,get(){trace.push(tag+'.N');return N;}},S:{enumerable:true,get(){trace.push(tag+'.S');return S;}}});
    const value=fn(make('left',1,s),make('right',n,t));runs.push({value,trace});
  }
  assert.deepEqual(runs[0],runs[1]);checks++;traces.push(runs[0]);
}
let seed=3921;
for(let i=0;i<2000;i++){
  seed=(Math.imul(seed,1664525)+1013904223)|0;
  const a=new Plain(seed,String(seed));
  const b=i%3===0?a:new Plain(i%3===1?seed:seed^1,String(seed));
  assert.equal(scoped(a,b),equals(a,b));checks++;
}
fs.writeFileSync(path.join(here,'records-scoped-results.json'),JSON.stringify({node:process.version,checks,nullish,traces,timingPerformed:false},null,2));
console.log(JSON.stringify({checks,nullish},null,2));
