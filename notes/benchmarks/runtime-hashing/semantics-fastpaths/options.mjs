import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL, fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
import assert from 'node:assert/strict';
const here=path.dirname(fileURLToPath(import.meta.url));
const root=path.resolve(here,'../../../..');
const original=path.join(root,'temp/runtime-hashing/fixture-js/Fixture.js');
const runtime=path.join(root,'temp/runtime-hashing/full-runtime');
const {some,value}=await import(pathToFileURL(path.join(runtime,'Option.js')));
const {equals,structuralHash}=await import(pathToFileURL(path.join(runtime,'Util.js')));
const forms=[undefined,some(undefined),some(some(null)),some(some('')),some(some('value'))];
let checks=0;
for(let i=0;i<5;i++)for(let j=0;j<5;j++){assert.equal(equals(forms[i],forms[j]),i===j);checks++;}
assert.equal(value(value(forms[2])),null);checks++;
assert.equal(equals(some(undefined),some(null)),true);checks++;
const source=fs.readFileSync(original,'utf8');
assert.equal(source.split('some(defaultOf())').length,2);
const dest=path.join(root,'temp/semantics-fastpaths');fs.mkdirSync(dest,{recursive:true});
// Absolute imports preserve the exact runtime used by the original fixture.
const isolated=source.replaceAll(/from "([^"]+)"/g,(_,p)=>`from "${pathToFileURL(path.resolve(path.dirname(original),p)).href}"`);
const runs={};
for(const [name,code] of Object.entries({baseline:isolated,candidate:isolated.replace('some(defaultOf())','some(some(defaultOf()))')})){
 const file=path.join(dest,`${name}.mjs`);fs.writeFileSync(file,code);
 const r=spawnSync('node',[file],{encoding:'utf8'});runs[name]={status:r.status,stdout:r.stdout,stderr:r.stderr};
}
assert.equal(runs.baseline.status,1);assert.equal(runs.candidate.status,0);
const result={checks,hashes:forms.map(structuralHash),runs};
fs.writeFileSync(path.join(here,'options-results.json'),JSON.stringify(result,null,2));
console.log(JSON.stringify(result,null,2));
