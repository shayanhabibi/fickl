// fickl: conversions between CancellationToken and the web AbortSignal
import { type Async, CancellationToken, OperationCanceledException } from "./AsyncBuilder.ts";
import { awaitPromise } from "./Async.ts";

// invariant: a token and the signal made from it, or made into it, always convert back to each other
const signals = new WeakMap<CancellationToken, AbortSignal>();
const tokens = new WeakMap<AbortSignal, CancellationToken>();

/** A signal that aborts, with a DOMException named AbortError, when the token is cancelled.
 * The AbortController is created on the first call and reused for the token afterwards. */
export function toAbortSignal(token: CancellationToken): AbortSignal {
  let signal = signals.get(token);
  if (signal === undefined) {
    const controller = new AbortController();
    signal = controller.signal;
    signals.set(token, signal);
    tokens.set(signal, token);
    token.addListener(() => controller.abort());
  }
  return signal;
}

/** A token that is cancelled when the signal aborts, or already cancelled if it has. */
export function fromAbortSignal(signal: AbortSignal): CancellationToken {
  let token = tokens.get(signal);
  if (token === undefined) {
    const created = new CancellationToken(signal.aborted);
    tokens.set(signal, created);
    signals.set(created, signal);
    if (!signal.aborted) {
      signal.addEventListener("abort", () => created.cancel(), { once: true });
    }
    token = created;
  }
  return token;
}

/** Awaits a promise until the token is cancelled, when the computation is cancelled without waiting for the promise. */
export function awaitPromiseWithToken<T>(promise: Promise<T>, token: CancellationToken): Async<T> {
  return awaitPromise(new Promise<T>((resolve, reject) => {
    const id = token.addListener(() => reject(new OperationCanceledException()));
    promise.then(
      (value) => { token.removeListener(id); resolve(value); },
      (error: unknown) => { token.removeListener(id); reject(error); });
  }));
}
