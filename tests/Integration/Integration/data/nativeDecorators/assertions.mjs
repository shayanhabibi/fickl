import assert from "node:assert/strict";
import { pathToFileURL } from "node:url";

const [modulePath, helperPath, mode] = process.argv.slice(2);
const output = await import(pathToFileURL(modulePath));
const { events, setterEvents } = await import(pathToFileURL(helperPath));
const value = output.create();
const standard = mode === "standard";

assert.deepEqual(output.read(value), ["helloinnerouter", "label:label", standard ? 10 : 0, standard ? 101 : 1]);
assert.equal(output.staticRead(), "hellostatic");
assert.equal(String(value), "primitivesymbol");
assert.equal(value.DollarLiteral("hello"), "hello$0");
assert.ok(events.includes("global-dollar"));
assert.equal(value.ReadOnly, "readonly:readonly");
assert.equal(value.constructor, output.Counter);
assert.equal(output.Counter.decorated, true);
assert.equal(output.Counter_$reflection().construct, output.Counter);
assert.equal(Object.isFrozen(output.Frozen), true);
assert.deepEqual(output.manualRead("start"), ["start:manual", "sink:manual"]);
assert.deepEqual(output.overrideRead(), ["hellooverride", "hellooverride"]);
assert.deepEqual(output.optionalRead(), [true, "optional"]);
assert.deepEqual(output.hiddenRead(), [[1, 2], [11, 2], [1, 2], [1, 2]]);
assert.equal(output.hiddenCallback(), 7);
assert.deepEqual(output.hiddenReceiver(), [3, 8, 2]);
assert.deepEqual(output.hiddenNull(), [true, 0]);
assert.deepEqual(output.hiddenNullSetter(), [true, 1]);
assert.deepEqual(output.frozenHidden(), [9, 2]);
assert.deepEqual(output.distinctHiddenFields(), [8, 2]);
assert.equal(output.opaqueRead(), 5);
assert.deepEqual(output.argumentShapes(), [5, 9, 8, 9]);
const opaqueDescriptor = Object.getOwnPropertyDescriptor(output.HiddenBase.prototype, 'Opaque');
Object.defineProperty(output.HiddenBase.prototype, 'Opaque', { configurable: true, value: 1 });
assert.deepEqual(output.nonCallableRead(), [true, 1]);
if (opaqueDescriptor) Object.defineProperty(output.HiddenBase.prototype, 'Opaque', opaqueDescriptor);
else delete output.HiddenBase.prototype.Opaque;
assert.deepEqual(output.explicitBaseRead(), [3, 1, 1]);
assert.equal(output.explicitBaseFieldRead(), 9);
setterEvents.length = 0;
output.orderedSetter();
assert.deepEqual(setterEvents, ['key', 'rhs']);
assert.equal(output.explicitState(), 8);
assert.ok(events.includes("set:sink"));

const memberFactories = ["outer", "inner", "count", "label", "field", "readonly", "static", "symbol", "$0"];
assert.deepEqual(events.filter(x => x.startsWith("eval:")).map(x => x.slice(5)),
  standard ? ["class", ...memberFactories, "manual", "sink", "override"]
    : [...memberFactories.filter(x => x !== "static"), "static", "class", "manual", "sink", "override"]);
assert.ok(events.indexOf("apply:inner") < events.indexOf("apply:outer"));
assert.deepEqual(events.filter(x => x.startsWith("init:")), standard ? ["init:count", "init:field"] : []);

function descriptor(object, name) {
  while (object != null) {
    const result = Object.getOwnPropertyDescriptor(object, name);
    if (result) return result;
    object = Object.getPrototypeOf(object);
  }
  throw new Error(`Missing property ${name}`);
}
assert.equal(descriptor(value, "ReadOnly").set, undefined);
assert.equal(descriptor(value, "Label").set, undefined);
assert.equal(Object.hasOwn(value, "Count"), false);
assert.equal(Object.hasOwn(value, "Field"), true);
value.Count = 5;
value.Field = 6;
assert.deepEqual(output.read(value).slice(2), [5, 6]);
console.log(`${mode} native decorator assertions passed`);
