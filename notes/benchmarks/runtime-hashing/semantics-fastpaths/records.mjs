import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL,fileURLToPath} from 'node:url';
import assert from 'node:assert/strict';
const here=path.dirname(fileURLToPath(import.meta.url));
const runtime=path.resolve(here,'../../../../temp/runtime-hashing/full-runtime');
const {Record}=await import(pathToFileURL(path.join(runtime,'Types.js')));
const {equals,structuralHash}=await import(pathToFileURL(path.join(runtime,'Util.js')));
class Plain extends Record{constructor(N,S){super();this.N=N;this.S=S;}}
class FloatRecord extends Record{constructor(F){super();this.F=F;}}
class Shallow extends Record{constructor(Items){super();this.Items=Items;}}
let calls=0;
class Odd extends Record{constructor(N){super();this.N=N;}Equals(){calls++;return false;}GetHashCode(){return 0;}}
class Nested extends Record{constructor(Item){super();this.Item=Item;}}
class Other extends Record{constructor(N,S){super();this.N=N;this.S=S;}}
const a=new Plain(1,'a'),b=new Plain(1,'a'),n=new FloatRecord(NaN),o=new Odd(1);
const observations={distinctEqual:equals(a,b),reference:a===b,nanScalar:equals(NaN,NaN),nanRecordSelf:equals(n,n),nanRecordCopy:equals(n,new FloatRecord(NaN)),customSelf:equals(o,o),customCalls:calls,customDirect:o.Equals(o)};
const nested=new Nested(o);const before=calls;observations.nestedCustomSelf=equals(nested,nested);observations.nestedCustomCalls=calls-before;
const x=new Shallow([1]),y=new Shallow([1]);
observations.shallowBefore=equals(x,y);const oldHash=structuralHash(x);x.Items[0]=2;
observations.shallowAfter=equals(x,y);observations.hashChanged=structuralHash(x)!==oldHash;
// Candidate: compiler-generated direct fields, for exactly this closed int/string record only.
function specialized(a,b){return a===b || (a==null ? b==null : b!=null && Object.getPrototypeOf(b)===Plain.prototype && a.N===b.N && a.S===b.S);}
let checks=0;
const values=[null,undefined,...Array.from({length:30},(_,i)=>new Plain(i%5,`${i%3}`))];
for(const left of values)for(const right of [...values,new Other(1,'a')]){assert.equal(specialized(left,right),equals(left,right));checks++;}
class Derived extends Plain{}
assert.equal(specialized(a,new Derived(1,'a')),false);checks++;
const traces=[];
for(const fn of [equals,specialized]){
 const trace=[];const make=tag=>Object.defineProperties(new Plain(1,'a'),{N:{enumerable:true,get(){trace.push(tag+'.N');return 1;}},S:{enumerable:true,get(){trace.push(tag+'.S');return 'a';}}});
 assert.equal(fn(make('a'),make('b')),true);traces.push(trace);checks++;
}
assert.deepEqual(traces[0],traces[1]);checks++;
observations.fieldReadOrder=traces;
assert.equal(observations.distinctEqual,true);assert.equal(observations.reference,false);
assert.equal(observations.shallowBefore,true);assert.equal(observations.shallowAfter,false);
fs.writeFileSync(path.join(here,'records-results.json'),JSON.stringify({observations,specializedChecks:checks},null,2));
console.log(JSON.stringify({observations,specializedChecks:checks},null,2));
