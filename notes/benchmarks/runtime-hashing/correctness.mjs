import assert from 'node:assert/strict';
import { pathToFileURL } from 'node:url';
import { resolve } from 'node:path';
const root = resolve(process.argv[2] ?? 'temp/runtime-hashing');
const load = async name => ({
  util: await import(pathToFileURL(`${root}/${name}/js/Util.js`)),
  types: await import(pathToFileURL(`${root}/${name}/js/Types.js`)),
});
const baseline = await load('baseline');
let assertions = 0;
function check(a,b,label) { assert.deepEqual(a,b,label); assertions++; }
function outcome(f) { try { return {value:f()}; } catch (e) { return {error:e.constructor.name,message:e.message}; } }
function runScenario(lib, kind, scenario) {
  const log=[];
  const container = kind==='array' ? [] : kind==='record' ? new (class R extends lib.types.Record {})() : {};
  const keys = kind==='array' ? ['0','1','2'] : ['a','b','c'];
  const hashable = (name, hash, action=()=>{}) => ({GetHashCode(){log.push(`hash:${name}`);action();return hash;}});
  if (scenario==='snapshot') {
    container[keys[0]]=hashable('a',1,()=>{container[keys[1]]=99;container.extra=5;});
    container[keys[1]]=hashable('b',2);
    container[keys[2]]=hashable('c',3);
  } else if (scenario==='getters' || scenario==='getter-throws') {
    for (let i=0;i<3;i++) Object.defineProperty(container,keys[i],{enumerable:true,configurable:true,get(){
      log.push(`get:${i}`); if(scenario==='getter-throws'&&i===1)throw new Error('getter failed');
      return hashable(`${i}`,i+1);
    }});
  } else if (scenario==='hash-throws') {
    container[keys[0]]=hashable('a',1);
    container[keys[1]]=hashable('b',2,()=>{throw new Error('hash failed');});
    container[keys[2]]=hashable('c',3);
  } else if (scenario==='delete') {
    container[keys[0]]=hashable('a',1,()=>{delete container[keys[1]];});
    container[keys[1]]=hashable('b',2);
    container[keys[2]]=hashable('c',3);
  } else if (scenario==='resize') {
    container[keys[0]]=hashable('a',1,()=>{if(kind==='array')container.length=1;});
    container[keys[1]]=2;container[keys[2]]=3;
  } else if (scenario==='keys') {
    container['10']=10;container['2']=2;container.z=3;container.a=4;
    container[Symbol('ignored')]=hashable('symbol',100);
    Object.defineProperty(container,'hidden',{value:hashable('hidden',200),enumerable:false});
  }
  const result=outcome(()=>kind==='array'?lib.util.arrayHash(container):lib.util.structuralHash(container));
  return {result,log,keys:Object.keys(container)};
}
let state=123456789;
const next=()=>{state^=state<<13;state^=state>>>17;state^=state<<5;return state>>>0;};
const atoms=[undefined,null,false,true,0,-0,NaN,Infinity,-Infinity,1,-1,2147483647,-2147483648,0.5,'','abc','λ','😀',0n,-1n,12345678901234567890n];
function randomValue(depth=0) {
  if(depth>2 || next()%3===0)return atoms[next()%atoms.length];
  const values=Array.from({length:next()%9},()=>randomValue(depth+1));
  return next()%2===0?values:Object.fromEntries(values.map((v,i)=>[`f${i}`,v]));
}
for(const name of ['array','record','object','combined']) {
  const candidate=await load(name);
  const start=assertions;
  for(const kind of ['array','record','object'])for(const scenario of ['snapshot','getters','getter-throws','hash-throws','delete','resize','keys'])
    check(runScenario(candidate,kind,scenario),runScenario(baseline,kind,scenario),`${name}/${kind}/${scenario}`);
  for(let i=0;i<5000;i++) {
    const value=randomValue();
    check(candidate.util.structuralHash(value),baseline.util.structuralHash(value),`${name}/random/${i}`);
  }
  for(const length of [0,1,2,18,19,32,1024]) {
    const values=Array.from({length},(_,i)=>atoms[i%atoms.length]);
    check(candidate.util.arrayHash(values),baseline.util.arrayHash(values),'array');
    const a=new(class extends candidate.types.Record{})(), b=new(class extends baseline.types.Record{})();
    values.forEach((v,i)=>{a[`p${i}`]=v;b[`p${i}`]=v;});
    check(a.GetHashCode(),b.GetHashCode(),'record');
    check(candidate.util.arrayHash(new Array(length)),baseline.util.arrayHash(new Array(length)),'holes');
    for(const C of [Int8Array,Uint8Array,Int16Array,Int32Array,Float32Array,Float64Array,BigInt64Array]) {
      const typed=C.from(Array.from({length},(_,i)=>C===BigInt64Array?BigInt(i):i/3));
      check(candidate.util.structuralHash(typed),baseline.util.structuralHash(typed),'typed');
    }
  }
  // Equality and comparison are unchanged, including nominal record distinction.
  const A=class extends candidate.types.Record {constructor(v){super();this.v=v;}};
  const B=class extends candidate.types.Record {constructor(v){super();this.v=v;}};
  check(candidate.util.equals(new A(1),new A(1)),true,'equal records');
  check(candidate.util.equals(new A(1),new B(1)),false,'distinct record types');
  check(candidate.util.compare(new A(1),new A(2))<0,true,'record ordering');
  check(new A(1).GetHashCode(),new A(1).GetHashCode(),'equal implies same hash');
  console.log(`${name}: ${assertions-start} differential/behavior assertions passed`);
}
if(process.argv.includes('--allocation-gate')) {
  // Instrument only the two allocation mechanisms being removed, not heap usage.
  for(const [name,kind] of [['array','array'],['record','record'],['object','object']]) {
    const candidate=await load(name);
    const values=[1,2,3];
    const record=new(class extends candidate.types.Record{})();Object.assign(record,{a:1,b:2,c:3});
    const originalArray=globalThis.Array, originalMap=Array.prototype.map;
    let arrays=0,maps=0;
    try {
      globalThis.Array=new Proxy(originalArray,{construct(t,args,newTarget){arrays++;return Reflect.construct(t,args,newTarget);}});
      originalArray.prototype.map=function(...args){maps++;return Reflect.apply(originalMap,this,args);};
      if(kind==='array')candidate.util.arrayHash(values);
      else candidate.util.structuralHash(kind==='record'?record:{a:1,b:2,c:3});
    } finally {globalThis.Array=originalArray;originalArray.prototype.map=originalMap;}
    assert.equal(kind==='array'?arrays:maps,0,`${name}: intermediate hash array must be eliminated`);
  }
  console.log('Allocation mechanism gates passed');
}
console.log(`PASS: ${assertions} assertions`);
