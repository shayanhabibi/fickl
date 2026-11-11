// Type-checked with the compiled snapshot, under the suite's exactOptionalPropertyTypes
import { read, type IMore, type IOpts } from "./OptionalProperty.ts";

// The option properties can be left out, set, or set to undefined
export const minimal: IOpts = { name: "a" };
export const full: IOpts = { name: "a", size: 1, vsize: 2, label: "b", nested: 3, nothing: undefined };
export const explicit: IOpts = { name: "a", size: undefined, label: undefined };
export const inherited: IMore = { name: "a" };
export const total: number = read(minimal);

// A read-only option is its value or undefined, with no wrapper to take off
export const size: number | undefined = full.size;

// A property with a setter is writable
full.label = "c";

// @ts-expect-error a property that is not an option is still required
export const noName: IOpts = { size: 1 };

// @ts-expect-error the optional property keeps its type
export const wrongType: IOpts = { name: "a", size: "1" };

// @ts-expect-error a property without a setter is read-only
full.size = 2;
