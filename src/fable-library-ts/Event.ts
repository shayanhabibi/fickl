import { type IObservable, subscribe } from "./Observable.ts";
import { type Option, some, value } from "./Option.ts";
import { createDisposable } from "./Util.ts";
import { type FSharpChoice$2_$union, Choice_tryValueIfChoice1Of2, Choice_tryValueIfChoice2Of2 } from "./Choice.ts";

export type Handler<T> = (sender: any, x: T) => void;

export interface IDelegateEvent<Delegate extends Function> {
  AddHandler(d: Delegate): void;
  RemoveHandler(d: Delegate): void;
}

export interface IEvent$2<Delegate extends Function, Args> extends IDelegateEvent<Delegate>, IObservable<Args> {
}

export type IEvent<T> = IEvent$2<Handler<T>, T>

export class Event$2<Delegate extends Function, Args> {
  private delegates: Delegate[] = [];

  private _add(d: Delegate) {
    this.delegates.push(d);
  }

  private _remove(d: Delegate) {
    const index = this.delegates.indexOf(d);
    if (index > -1) {
      this.delegates.splice(index, 1);
    }
  }

  get Publish(): IEvent$2<Delegate, Args> {
    return createEvent(h => { this._add(h) }, h => { this._remove(h) });
  }

  public Trigger(value: Args): void;
  public Trigger(sender: any, value: Args): void
  public Trigger(senderOrValue: any, valueOrUndefined?: Args): void {
    // Disambiguate the overloads by arity, not by value: a two-argument call may
    // legitimately pass `undefined` as the event value (e.g. unit events).
    let sender: any = null;
    let value: Args;
    if (arguments.length <= 1) {
      value = senderOrValue as Args;
    } else {
      sender = senderOrValue;
      value = valueOrUndefined as Args;
    }
    this.delegates.forEach(f => { f(sender, value) });
  }
}

export class Event<T> extends Event$2<Handler<T>, T> {
}

export function add<Del extends Function, T>(callback: (x: T) => void, sourceEvent: IEvent$2<Del, T>): void {
  subscribe(callback, sourceEvent);
}

export function choose<Del extends Function, T, U>(chooser: (x: T) => Option<U>, sourceEvent: IEvent$2<Del, T>): IEvent<U> {
  const ev = new Event<U>();
  add((t) => {
    const u = chooser(t);
    if (u != null) { ev.Trigger(value(u)); }
  }, sourceEvent);
  return ev.Publish;
}

export function filter<Del extends Function, T>(predicate: (x: T) => boolean, sourceEvent: IEvent$2<Del, T>): IEvent<T> {
  return choose((x) => predicate(x) ? some(x) : undefined, sourceEvent);
}

export function map<Del extends Function, T, U>(mapping: (x: T) => U, sourceEvent: IEvent$2<Del, T>): IEvent<U> {
  const ev = new Event<U>();
  add((t) => ev.Trigger(mapping(t)), sourceEvent);
  return ev.Publish;
}

export function merge<Del1 extends Function, Del2 extends Function, T>(event1: IEvent$2<Del1, T>, event2: IEvent$2<Del2, T>): IEvent<T> {
  const ev = new Event<T>();
  const fn = (x: T) => ev.Trigger(x);
  add(fn, event1);
  add(fn, event2);
  return ev.Publish;
}

export function pairwise<Del extends Function, T>(sourceEvent: IEvent$2<Del, T>): IEvent<[T, T]> {
  const ev = new Event<[T, T]>();
  let last: T;
  let haveLast = false;
  add((next) => {
    if (haveLast) {
      ev.Trigger([last, next]);
    }
    last = next;
    haveLast = true;
  }, sourceEvent);
  return ev.Publish;
}

export function partition<Del extends Function, T>(predicate: (x: T) => boolean, sourceEvent: IEvent$2<Del, T>): [IEvent<T>, IEvent<T>] {
  return [filter(predicate, sourceEvent), filter((x) => !predicate(x), sourceEvent)];
}

export function scan<Del extends Function, U, T>(collector: (u: U, t: T) => U, state: U, sourceEvent: IEvent$2<Del, T>): IEvent<U> {
  return map((t) => state = collector(state, t), sourceEvent);
}

export function split<Del extends Function, T, U1, U2>(splitter: (x: T) => FSharpChoice$2_$union<U1, U2>, sourceEvent: IEvent$2<Del, T>): [IEvent<U1>, IEvent<U2>] {
  return [
    choose((v) => Choice_tryValueIfChoice1Of2(splitter(v)), sourceEvent),
    choose((v) => Choice_tryValueIfChoice2Of2(splitter(v)), sourceEvent),
  ];
}

export function createEvent<Del extends Function, T>(addHandler: (h: Del) => void, removeHandler: (h: Del) => void): IEvent$2<Del, T> {
  return {
    AddHandler(h) { addHandler(h); },
    RemoveHandler(h) { removeHandler(h); },
    Subscribe(r) {
      const h = ((_: any, args: T) => r.OnNext(args)) as unknown as Del;
      addHandler(h);
      return createDisposable(() => removeHandler(h));
    }
  };
}

// fickl: web EventTarget and Node.js emitter events as F# events
type Listener<T> = (x: T) => void;

// invariant: per source object and event key, each handler maps to one listener per AddHandler call, removed
// last-in first-out. Shared across IEvent values, as a getter may build a new one for each access.
const addedListeners = new WeakMap<object, Map<string, Map<Function, Listener<any>[]>>>();

function listenersOf(source: object, key: string): Map<Function, Listener<any>[]> {
  let byKey = addedListeners.get(source);
  if (byKey === undefined) { byKey = new Map(); addedListeners.set(source, byKey); }
  let byHandler = byKey.get(key);
  if (byHandler === undefined) { byHandler = new Map(); byKey.set(key, byHandler); }
  return byHandler;
}

function listenerEvent<T>(source: object, key: string, listen: (l: Listener<T>) => void, unlisten: (l: Listener<T>) => void): IEvent<T> {
  return createEvent<Handler<T>, T>(
    (h) => {
      const listener: Listener<T> = (x) => h(source, x);
      const listeners = listenersOf(source, key);
      const added = listeners.get(h);
      if (added === undefined) { listeners.set(h, [listener]); } else { added.push(listener); }
      listen(listener);
    },
    (h) => {
      const listeners = listenersOf(source, key);
      const added = listeners.get(h);
      const listener = added?.pop();
      if (added === undefined || listener === undefined) { return; }
      if (added.length === 0) { listeners.delete(h); }
      unlisten(listener);
    });
}

/** The events of a type an EventTarget dispatches, through addEventListener and removeEventListener. */
export function fromEventTarget<T>(target: EventTarget, type: string, options?: AddEventListenerOptions): IEvent<T> {
  const capture = typeof options === "object" && options.capture === true;
  return listenerEvent<T>(target, (capture ? "capture:" : "bubble:") + type,
    (l) => target.addEventListener(type, l as unknown as EventListener, options),
    (l) => target.removeEventListener(type, l as unknown as EventListener, options));
}

/** An object with Node.js-style `on` and `off` or `removeListener` methods. */
export interface EventEmitter {
  on(eventName: string, listener: (...args: any[]) => void): unknown;
  off?(eventName: string, listener: (...args: any[]) => void): unknown;
  removeListener?(eventName: string, listener: (...args: any[]) => void): unknown;
}

/** The events of a name an emitter emits, with the first argument as the value. */
export function fromEmitter<T>(emitter: EventEmitter, eventName: string): IEvent<T> {
  return listenerEvent<T>(emitter, eventName,
    (l) => emitter.on(eventName, l),
    (l) => {
      if (emitter.off !== undefined) { emitter.off(eventName, l); } else { emitter.removeListener?.(eventName, l); }
    });
}

/** Dispatches a CustomEvent carrying a value, and returns false if a listener cancelled it. */
export function dispatchCustomEvent<T>(target: EventTarget, type: string, detail: T): boolean {
  return target.dispatchEvent(new CustomEvent<T>(type, { detail }));
}
