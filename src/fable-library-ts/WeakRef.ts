// fickl: WeakReference<'T> is a native WeakRef
import type { FSharpRef } from "./Types.ts";

// The library targets ES2020, whose declarations have no WeakRef
interface Dereferenceable<T> {
  deref(): T | undefined;
}

/** A WeakRef of a target typed by a type parameter, which TypeScript's WeakRef does not accept. */
export function create(target: unknown): any {
  const WeakRefConstructor = (globalThis as unknown as { WeakRef: new (target: unknown) => unknown }).WeakRef;
  return new WeakRefConstructor(target);
}

/** WeakReference.TryGetTarget: whether the target is still alive, writing it to the out argument if so. */
export function tryGetTarget<T>(ref: Dereferenceable<T>, target: FSharpRef<T>): boolean {
  const value = ref.deref();
  if (value === undefined) {
    return false;
  }
  target.contents = value;
  return true;
}
