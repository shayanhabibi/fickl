// Type-checked with the snapshot: optional elements can be left out, and labels name the elements
import { type Entry, type FetchArgs, type Span, stop, url } from "./LabelledTuples.ts";

export const entry: Entry = ["key", 1];
export const get: FetchArgs = ["/"];
export const post: FetchArgs = ["/", { method: "POST" }];
export const span: Span = [0];

export const fromTs: string = url(["/"]);
export const stopped = stop([0, 5]);

// @ts-expect-error a required element cannot be left out
export const missing: Entry = ["key"];
