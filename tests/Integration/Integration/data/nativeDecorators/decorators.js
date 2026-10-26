export const events = [];
globalThis["$0"] = function(value) { events.push("global-dollar"); return value; };

/** @param {any} value @param {any} context @param {any} [descriptor] @returns {any} */
export function identityDecorator(value, context, descriptor) {
  return isStandard(context) ? value : descriptor;
}

function factory(label) { events.push(`eval:${label}`); }
function applied(label) { events.push(`apply:${label}`); }
function isStandard(context) { return context != null && typeof context === "object" && typeof context.kind === "string"; }

/** @returns {any} */
export function classFactory(label) {
  factory(label);
  /** @param {any} value @param {any} [context] @returns {any} */
  return function(value, context) {
    applied(label);
    return class extends value { static decorated = true; };
  };
}

/** @returns {any} */
export function methodFactory(label) {
  factory(label);
  /** @param {any} value @param {any} context @param {any} [descriptor] @returns {any} */
  return function(value, context, descriptor) {
    applied(label);
    const original = isStandard(context) ? value : descriptor.value;
    function replacement(...args) { return original.apply(this, args) + label; }
    if (isStandard(context)) return replacement;
    descriptor.value = replacement;
    return descriptor;
  };
}

/** @returns {any} */
export function getterFactory(label) {
  factory(label);
  /** @param {any} value @param {any} context @param {any} [descriptor] @returns {any} */
  return function(value, context, descriptor) {
    applied(label);
    const original = isStandard(context) ? value : descriptor.get;
    function replacement() { return original.call(this) + ":" + label; }
    if (isStandard(context)) return replacement;
    descriptor.get = replacement;
    return descriptor;
  };
}

/** @returns {any} */
export function accessorFactory(label) {
  factory(label);
  /** @param {any} value @param {any} context @param {any} [descriptor] @returns {any} */
  return function(value, context, descriptor) {
    applied(label);
    if (isStandard(context)) {
      return { get: value.get, set: value.set, init(initial) { events.push(`init:${label}`); return initial + 10; } };
    }
    return descriptor;
  };
}

/** @returns {any} */
export function fieldFactory(label) {
  factory(label);
  /** @param {any} value @param {any} context @returns {any} */
  return function(value, context) {
    applied(label);
    if (isStandard(context)) return function(initial) { events.push(`init:${label}`); return initial + 100; };
  };
}

/** @returns {any} */
export function setterFactory(label) {
  factory(label);
  /** @param {any} value @param {any} context @param {any} [descriptor] @returns {any} */
  return function(value, context, descriptor) {
    applied(label);
    const original = isStandard(context) ? value : descriptor.set;
    function replacement(next) { events.push(`set:${label}`); return original.call(this, next); }
    if (isStandard(context)) return replacement;
    descriptor.set = replacement;
    return descriptor;
  };
}
