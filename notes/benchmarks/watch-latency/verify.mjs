import { pathToFileURL } from 'node:url';
const [file, expectedJson] = process.argv.slice(2);
const expected = JSON.parse(expectedJson);
const actual = await import(pathToFileURL(file).href);
const observed = Object.fromEntries(Object.keys(expected).map(key => [key, actual[key]]));
const wrong = Object.keys(expected).filter(key => observed[key] !== expected[key]);
if (wrong.length > 0) throw new Error(`Expected ${JSON.stringify(expected)}, received ${JSON.stringify(observed)}`);
console.log(JSON.stringify(observed));
