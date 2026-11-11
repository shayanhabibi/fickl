// Type-checked with the compiled snapshot: each line below must be an error, which it is not
// when the static field of a case is `any`
import { Box$1, IOType, IOType_Sample, Only, Phantom$1 } from "./UnionCaseSingleton.ts";

// @ts-expect-error the case is an IOType, not a number
export const source: number = IOType.Source;

// @ts-expect-error the case has the type of the union, which is wider than one case
export const sample: IOType<1> = IOType.Source;

// @ts-expect-error the case constructor function returns the case too
export const fromFunction: number = IOType_Sample();

// @ts-expect-error
export const empty: number = Box$1.Empty;

// @ts-expect-error
export const only: number = Only.Only;

// @ts-expect-error
export const phantom: number = Phantom$1.Phantom;

// Two cases have the same type, so they can be compared and narrowed by tag
export const sameCase: boolean = IOType.Source === IOType.Sample;
export const isSample: boolean = IOType.Source.tag === 1;
