const payloads = new WeakMap<object, object>();

// The tagged view owns its discriminator and forwards the interface to its payload.
// A separate target keeps proxy invariants independent of sealed/frozen payloads.
export function withInlineInterfaceTag<T extends object, K extends string, const V>(value: T, tagName: K, tag: V): T & Record<K, V> {
  const target = Object.create(Object.getPrototypeOf(value)) as T;
  Object.defineProperty(target, tagName, { value: tag, enumerable: true, configurable: true, writable: true });
  const methods = new Map<PropertyKey, { original: unknown; bound: unknown }>();
  const view = new Proxy(target, {
    get(_, key) {
      if (key === tagName) {
        return Reflect.get(target, key);
      }
      const fixed = Reflect.getOwnPropertyDescriptor(target, key);
      if (fixed?.configurable === false && "value" in fixed && fixed.writable === false) {
        // Explicit fixed descriptors expose the exact stored value, including a function's receiver semantics.
        return fixed.value;
      }
      const result: unknown = Reflect.get(value, key, value);
      if (typeof result !== "function" || key === "constructor") {
        return result;
      }
      const cached = methods.get(key);
      if (cached?.original === result) {
        return cached.bound;
      }
      const bound: unknown = result.bind(value);
      methods.set(key, { original: result, bound });
      return bound;
    },
    set(_, key, next) {
      return key === tagName ? Reflect.set(target, key, next) : Reflect.set(value, key, next, value);
    },
    has(_, key) {
      return key === tagName ? Reflect.has(target, key) : Reflect.has(value, key);
    },
    ownKeys() {
      return [...new Set([...Reflect.ownKeys(value).filter(key => key !== tagName), ...Reflect.ownKeys(target)])];
    },
    getOwnPropertyDescriptor(_, key) {
      if (key === tagName) {
        return Reflect.getOwnPropertyDescriptor(target, key);
      }
      const descriptor = Reflect.getOwnPropertyDescriptor(value, key);
      if (Reflect.getOwnPropertyDescriptor(target, key)?.configurable === false) {
        if (descriptor != null) {
          Reflect.defineProperty(target, key, descriptor);
        }
        return Reflect.getOwnPropertyDescriptor(target, key);
      }
      return descriptor == null ? undefined : { ...descriptor, configurable: true };
    },
    defineProperty(_, key, descriptor) {
      if (key === tagName) {
        return Reflect.defineProperty(target, key, descriptor);
      }
      if (!Reflect.defineProperty(value, key, descriptor)) {
        return false;
      }
      const stored = Reflect.getOwnPropertyDescriptor(value, key);
      return stored?.configurable === false ? Reflect.defineProperty(target, key, stored) : true;
    },
    deleteProperty(_, key) {
      return Reflect.deleteProperty(key === tagName ? target : value, key);
    },
    getPrototypeOf() {
      return Reflect.getPrototypeOf(value);
    },
    setPrototypeOf(_, prototype) {
      return Reflect.setPrototypeOf(value, prototype) && Reflect.setPrototypeOf(target, prototype);
    },
    preventExtensions() {
      // The view must remain extensible so forwarded keys can follow the payload.
      return false;
    },
  });
  payloads.set(view, value);
  return view as T & Record<K, V>;
}

export function getInlineInterfaceValue<T extends object>(value: T): T {
  return (payloads.get(value) as T | undefined) ?? value;
}
