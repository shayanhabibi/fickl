import { FSharpRef } from "./Types.ts";
import { Exception } from "./Util.ts";

// JS's unary + also reads 0x, 0b and 0o literals, which .NET does not parse as a Double
function hasRadixPrefix(str: string) {
  return /^\s*[+-]?0[xXbBoO]/.test(str);
}

export function tryParse(str: string, defValue: FSharpRef<number>): boolean {
  // TODO: test if value is valid and in range
  if (str != null && /\S/.test(str) && !hasRadixPrefix(str)) {
    const v = +str.replace("_", "");
    if (!Number.isNaN(v)) {
      defValue.contents = v;
      return true;
    }
  }
  return false;
}

export function parse(str: string): number {
  if (str != null && /\S/.test(str) && !hasRadixPrefix(str)) {
    const v = +str.replace("_", "");
    if (!Number.isNaN(v)) {
      return v;
    }
  }
  throw new Exception(`The input string ${str} was not in a correct format.`);
}

// Double.Equals and Single.Equals: unlike ==, NaN equals itself
export function equals(x: number, y: number): boolean {
  return x === y || (x !== x && y !== y);
}

// JS Number.isFinite function evals false for NaN
export function isPositiveInfinity(x: number) {
  return x === Number.POSITIVE_INFINITY;
}

export function isNegativeInfinity(x: number) {
  return x === Number.NEGATIVE_INFINITY;
}

export function isInfinity(x: number) {
  return x === Number.POSITIVE_INFINITY || x === Number.NEGATIVE_INFINITY;
}

export function max(x: number, y: number): number {
  return Math.max(x, y);
}

export function min(x: number, y: number): number {
  return Math.min(x, y);
}

export function maxMagnitude(x: number, y: number): number {
  if (Number.isNaN(x) || Number.isNaN(y)) { return NaN; }
  return Math.abs(x) > Math.abs(y) ? x : y;
}

export function minMagnitude(x: number, y: number): number {
  if (Number.isNaN(x) || Number.isNaN(y)) { return NaN; }
  return Math.abs(x) < Math.abs(y) ? x : y;
}

export function clamp(x: number, min: number, max: number): number {
  return x < min ? min : x > max ? max : x;
}
