// Type-checked with the snapshot: TypeScript sees the members the F# module adds
import { properties } from "./plugin.js";
import { countAll } from "./Augmentations.ts";

export function counterValue(): number {
    return document.createElement("my-counter").count;
}

export function total(): number {
    return countAll(window) + window.counters.length;
}

export function hello(): string {
    return properties().translate("hello") + properties().version;
}
