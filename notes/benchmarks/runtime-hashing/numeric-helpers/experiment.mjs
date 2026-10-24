import { readFileSync,writeFileSync,mkdirSync } from 'node:fs';
import { resolve,dirname } from 'node:path';
import { fileURLToPath,pathToFileURL } from 'node:url';
import { stripTypeScriptTypes } from 'node:module';
import { createHash } from 'node:crypto';
import { performance } from 'node:perf_hooks';
import assert from 'node:assert/strict';

const here=dirname(fileURLToPath(import.meta.url)),main=resolve(here,'../../../../../..');
const source=resolve(main,'src/fable-library-ts/Double.ts');
const original=readFileSync(source,'utf8');
const old=`export function parse(str: string): number {
  const defValue = new FSharpRef(0);
  if (tryParse(str, defValue)) {
    return defValue.contents;
  } else {
    throw new Exception(\`The input string \${str} was not in a correct format.\`);
  }
}`;
const replacement=`export function parse(str: string): number {
  if (str != null && /\\S/.test(str)) {
    const v = +str.replace("_", "");
    if (!Number.isNaN(v)) { return v; }
  }
  throw new Exception(\`The input string \${str} was not in a correct format.\`);
}`;
assert.equal(original.split(old).length,2,'Baseline changed; inspect source');
const dest=resolve(here,'../../../../temp/runtime-numeric-helpers');mkdirSync(dest,{recursive:true});
for(const [name,text] of Object.entries({baseline:original,candidate:original.replace(old,replacement)})) {
  const imports=text.replaceAll('"./Types.ts"',JSON.stringify(pathToFileURL(resolve(main,'temp/fable-library-js/Types.js')).href)).replaceAll('"./Util.ts"',JSON.stringify(pathToFileURL(resolve(main,'temp/fable-library-js/Util.js')).href));
  writeFileSync(resolve(dest,`${name}.mjs`),stripTypeScriptTypes(imports));
}
writeFileSync(resolve(here,'candidate.ts'),replacement+'\n');
const b=await import(pathToFileURL(resolve(dest,'baseline.mjs'))),c=await import(pathToFileURL(resolve(dest,'candidate.mjs')));
const metadata={source,sha256:createHash('sha256').update(original).digest('hex'),node:process.version,v8:process.versions.v8};
const inputs=['0','-0','1','-2','3.14159','  123.5  ','1e-100','1e309','Infinity','-Infinity','NaN','',' ','\0','0xff','0b101','1_000','1_2_3','1,000','1.2.3',null,undefined];
const mode=process.argv[2]??'check';
if(mode==='check') {
  let seed=74253;const next=()=>{seed=(Math.imul(seed,1664525)+1013904223)|0;return seed>>>0;};
  const alphabet='0123456789xXaAbBeE+-. _\t\nInfinityNaN';
  for(let i=0;i<4000;i++)inputs.push(Array.from({length:next()%32},()=>alphabet[next()%alphabet.length]).join(''));
  const run=(fn,input)=>{try{return {value:fn(input)};}catch(e){return {error:[e.constructor.name,e.message]};}};
  let checks=0;for(const input of inputs){assert.deepEqual(run(c.parse,input),run(b.parse,input));checks++;}
  const {FSharpRef}=await import(pathToFileURL(resolve(main,'temp/fable-library-js/Types.js')));
  for(const input of inputs){const br=new FSharpRef(987),cr=new FSharpRef(987);assert.equal(c.tryParse(input,cr),b.tryParse(input,br));assert.ok(Object.is(cr.contents,br.contents));checks+=2;}
  // Interop strings can observe coercion, replace calls and throws. Preserve those traces too.
  const traces=[];
  for(const throws of [false,true]) {
    const run=fn=>{const trace=[];const input={toString(){trace.push('toString');return '12';},replace(a,z){trace.push(['replace',a,z]);if(throws)throw Error('replace failed');return '12';}};return {outcome:(()=>{try{return fn(input);}catch(e){return [e.constructor.name,e.message];}})(),trace};};
    const br=run(b.parse),cr=run(c.parse);assert.deepEqual(cr,br);traces.push(cr);checks++;
  }
  writeFileSync(resolve(here,'correctness.json'),JSON.stringify({...metadata,checks,status:'passed',interopTraces:traces},null,2));
  console.log(`PASS: ${checks} numeric parse differential checks`);
} else if(mode==='bench') {
  let sink=0;const median=xs=>[...xs].sort((a,b)=>a-b)[xs.length>>>1];
  const cases=[['control-tryParse',['12345','-12345','0','2147483647']],['integer',['12345','-12345','0','2147483647']],['decimal',['3.14159','-0.125','12345.6789','-0']],['exponent',['1e30','1e-30','2.71e12','-9e-8']],['whitespace',[' 123 ','\t-5.5\n',' 0.123 ','  7e8 ']],['mixed-valid',['123','-3.125','1_000','0xff']],['invalid',['bad','1.2.3','1_2_3','NaN']]];
  const batch=(fn,pool,n)=>{let sum=0;for(let i=0;i<n;i++){try{const value=fn(pool[i&3]);sum=(sum+(value|0))|0;}catch(e){sum=(sum+e.message.length)|0;}}sink^=sum;return sum;};
  const results=[];
  const {FSharpRef}=await import(pathToFileURL(resolve(main,'temp/fable-library-js/Types.js')));
  const bref=new FSharpRef(0),cref=new FSharpRef(0);
  for(const [name,pool] of cases){
    const bf=name==='control-tryParse'?s=>b.tryParse(s,bref):b.parse,cf=name==='control-tryParse'?s=>c.tryParse(s,cref):c.parse;
    for(const fn of [bf,cf]){const until=performance.now()+40;do{batch(fn,pool,128);}while(performance.now()<until);}
    let n=128,elapsed;do{const start=performance.now();batch(bf,pool,n);elapsed=performance.now()-start;if(elapsed<15)n*=2;}while(elapsed<15);n=Math.max(1,Math.round(n*25/elapsed));
    const rounds=[];for(let r=0;r<11;r++){
      globalThis.gc?.();const samples={baseline:[],candidate:[]};let expected;
      for(const label of r%2?['candidate','baseline','baseline','candidate']:['baseline','candidate','candidate','baseline']){const start=performance.now(),sum=batch(label==='baseline'?bf:cf,pool,n),ns=(performance.now()-start)*1e6/n;if(expected!==undefined)assert.equal(sum,expected);expected=sum;samples[label].push(ns);}
      const baseline_ns=(samples.baseline[0]+samples.baseline[1])/2,candidate_ns=(samples.candidate[0]+samples.candidate[1])/2;rounds.push({baseline_ns,candidate_ns,ratio:candidate_ns/baseline_ns});
    }
    results.push({name,iterations:n,baseline_ns:median(rounds.map(r=>r.baseline_ns)),candidate_ns:median(rounds.map(r=>r.candidate_ns)),paired_change_percent:100*(median(rounds.map(r=>r.ratio))-1),wins:rounds.filter(r=>r.ratio<1).length,rounds});
  }
  writeFileSync(resolve(here,process.argv[3]??'timings.json'),JSON.stringify({...metadata,method:'11 ABBA/BAAB blocks; checksummed parsed values/errors',results,sink},null,2));
  console.log(JSON.stringify(results.map(({name,paired_change_percent,wins})=>({name,paired_change_percent,wins})),null,2));
} else throw Error('Expected check or bench');
