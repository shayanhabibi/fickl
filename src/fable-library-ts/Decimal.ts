import { type Numeric, symbol } from "./Numeric.ts";
import { FSharpRef } from "./Types.ts";
import { Exception, MidpointRounding, combineHashCodes } from "./Util.ts";
import { NumberStyles, splitNumber } from "./NumberStyles.ts";
import { ArgumentNullException_$ctor_Z721C83C5, ArgumentOutOfRangeException_$ctor_Z721C83C5, DivideByZeroException_$ctor_Z721C83C5, FormatException_$ctor_Z721C83C5, OverflowException_$ctor_Z721C83C5 } from "./System.ts";
import { type int8, type uint8, type int16, type uint16, type int32, type uint32, type float16, type float32, type float64 } from "./Int32.ts";
import { type int64, type uint64, type int128, type uint128, type nativeint, type unativeint } from "./BigInt.ts";
import * as bigInt from "./BigInt.ts";

const MAX = (1n << 96n) - 1n;
const MAX_SCALE = 28;
const UINT32 = 1n << 32n;

const POW10: bigint[] = [];
for (let i = 0, p = 1n; i <= 64; i++, p *= 10n) {
  POW10.push(p);
}

function pow10(n: number): bigint {
  return n <= 64 ? POW10[n]! : 10n ** BigInt(n);
}

function overflow(): never {
  throw OverflowException_$ctor_Z721C83C5("Value was either too large or too small for a Decimal.");
}

function divideByZero(): never {
  throw DivideByZeroException_$ctor_Z721C83C5("Attempted to divide by zero.");
}

// n / d rounded half to even; d > 0
function divHalfEven(n: bigint, d: bigint): bigint {
  const q = n / d;
  const r = n % d;
  if (r === 0n) {
    return q;
  }
  const r2 = (r < 0n ? -r : r) * 2n;
  if (r2 > d || (r2 === d && (q & 1n) === 1n)) {
    return n < 0n ? q - 1n : q + 1n;
  }
  return q;
}

// n / d rounded half away from zero; d > 0
function divHalfUp(n: bigint, d: bigint): bigint {
  const q = n / d;
  const r2 = (n % d) * 2n;
  return r2 >= d ? q + 1n : r2 <= -d ? q - 1n : q;
}

// System.Decimal: the value is m / 10^s, with |m| < 2^96 and s from 0 to 28.
// n is the sign bit of a zero, which .NET keeps (-0.0m and 0.0m differ in GetBits); it is false for any other value.
export default class Decimal {
  readonly m: bigint;
  readonly s: number;
  readonly n: boolean;

  // A string is what the compiler emits for a literal; `scale` is given only by this module,
  // with the mantissa as the first argument
  constructor(x: string | number | bigint | Decimal, scale?: number, negativeZero: boolean = false) {
    if (scale !== undefined) {
      this.m = x as bigint;
      this.s = scale;
      this.n = negativeZero;
    } else {
      const d = typeof x === "string" ? ofString(x, false)
        : typeof x === "bigint" ? fit(x, 0)
          : typeof x === "number" ? (Number.isSafeInteger(x) ? make(BigInt(x), 0) : fromFloat64(x))
            : x;
      this.m = d.m;
      this.s = d.s;
      this.n = d.n;
    }
  }

  toString(): string {
    return toString(this);
  }

  toJSON(): string {
    return toString(this);
  }

  valueOf(): number {
    return toNumber(this);
  }

  // 1.0m and 1.00m are equal, so trailing zeros do not take part
  GetHashCode(): number {
    if (this.m === 0n) {
      return 0;
    }
    const { m, s } = trim(this.m, this.s, 0);
    return combineHashCodes([s, Number(BigInt.asIntN(32, m)), Number(BigInt.asIntN(32, m >> 32n)), Number(BigInt.asIntN(32, m >> 64n))]);
  }

  Equals(x: Decimal): boolean {
    return x instanceof Decimal && compare(this, x) === 0;
  }

  CompareTo(x: Decimal | number): number {
    return compare(this, typeof x === "number" ? new Decimal(x) : x);
  }

  [symbol]() {
    const _this = this;
    return {
      // Only used to format, so the product is not fitted to 96 bits: `P` of MaxValue does not overflow
      multiply: (y: Numeric): Numeric => {
        const d = y instanceof Decimal ? y : new Decimal(y as number);
        return new Decimal(_this.m * d.m, _this.s + d.s);
      },
      toPrecision: (sd?: number) => toPrecision(_this, sd),
      toExponential: (dp?: number) => toExponential(_this, dp),
      toFixed: (dp?: number) => toFixed(_this, dp),
      toHex: () => (Number(_this) >>> 0).toString(16),
    }
  }
}

export type decimal = Decimal;

function make(m: bigint, s: number): Decimal {
  return new Decimal(m, s);
}

// d, or the negative zero of its scale when d is a zero that .NET gives the sign bit
function zeroSign(d: Decimal, negative: boolean): Decimal {
  return negative && d.m === 0n ? new Decimal(0n, d.s, true) : d;
}

// The exact value m / 10^s as a decimal: rounded half to even to the largest scale up to 28 whose
// mantissa fits 96 bits. Throws when the integral part alone does not fit.
function fit(m: bigint, s: number): Decimal {
  if (s <= MAX_SCALE && m <= MAX && m >= -MAX) {
    return make(m, s);
  }
  let k = s > MAX_SCALE ? s - MAX_SCALE : 0;
  const a = m < 0n ? -m : m;
  while (a >= pow10(29 + k)) {
    k++;
  }
  for (; ; k++) {
    if (k > s) {
      overflow();
    }
    const r = k === 0 ? m : divHalfEven(m, pow10(k));
    if (r <= MAX && r >= -MAX) {
      return make(r, s - k);
    }
  }
}

const TRIM_STEPS = [16, 8, 4, 2, 1];

// Drops trailing zeros while the scale stays at or above minScale
function trim(m: bigint, s: number, minScale: number): Decimal {
  if (m === 0n) {
    return make(0n, minScale);
  }
  for (const step of TRIM_STEPS) {
    const p = POW10[step]!;
    while (s - minScale >= step && m % p === 0n) {
      m /= p;
      s -= step;
    }
  }
  return make(m, s);
}

export const get_Zero = make(0n, 0);
export const get_One = make(1n, 0);
export const get_MinusOne = make(-1n, 0);
export const get_MaxValue = make(MAX, 0);
export const get_MinValue = make(-MAX, 0);

export function compare(x: Decimal, y: Decimal): number {
  let a = x.m, b = y.m;
  const d = x.s - y.s;
  if (d < 0) {
    a *= pow10(-d);
  } else if (d > 0) {
    b *= pow10(d);
  }
  return a < b ? -1 : a > b ? 1 : 0;
}

export function equals(x: Decimal, y: Decimal) {
  return compare(x, y) === 0;
}

export function scale(x: Decimal): number { return x.s; }
export function isNegative(x: Decimal): boolean { return x.m < 0n || x.n; }
export function isInteger(x: Decimal): boolean { return x.m % pow10(x.s) === 0n; }

export function abs(x: Decimal) { return x.m < 0n ? make(-x.m, x.s) : x.n ? make(0n, x.s) : x; }
export function sign(x: Decimal): number { return x.m < 0n ? -1 : x.m > 0n ? 1 : 0; }
export function copySign(x: Decimal, sign: Decimal) { return isNegative(x) === isNegative(sign) ? x : op_UnaryNegation(x); }

export function max(x: Decimal, y: Decimal): Decimal { return compare(x, y) < 0 ? y : x; }
export function min(x: Decimal, y: Decimal): Decimal { return compare(x, y) < 0 ? x : y; }

export function maxMagnitude(x: Decimal, y: Decimal): Decimal { return compare(abs(x), abs(y)) > 0 ? x : y; }
export function minMagnitude(x: Decimal, y: Decimal): Decimal { return compare(abs(x), abs(y)) < 0 ? x : y; }

export function clamp(x: Decimal, min: Decimal, max: Decimal): Decimal {
  return compare(x, min) < 0 ? min : compare(x, max) > 0 ? max : x;
}

function checkDigits(digits: number) {
  if (!(digits >= 0 && digits <= MAX_SCALE)) {
    throw ArgumentOutOfRangeException_$ctor_Z721C83C5("decimals");
  }
}

function roundTo(x: Decimal, digits: number, mode: number): Decimal {
  if (digits >= x.s) {
    return x;
  }
  const d = pow10(x.s - digits);
  let q = x.m / d;
  const r = x.m % d;
  if (r !== 0n) {
    switch (mode) {
      case MidpointRounding.ToEven: q = divHalfEven(x.m, d); break;
      case MidpointRounding.AwayFromZero: q = divHalfUp(x.m, d); break;
      case MidpointRounding.ToNegativeInfinity: if (r < 0n) { q -= 1n; } break;
      case MidpointRounding.ToPositiveInfinity: if (r > 0n) { q += 1n; } break;
    }
  }
  return zeroSign(make(q, digits), isNegative(x));
}

export function round(x: Decimal, digits: number = 0) {
  checkDigits(digits);
  return roundTo(x, digits, MidpointRounding.ToEven);
}

export function roundToMode(x: Decimal, digits: number, mode: number) {
  checkDigits(digits);
  if (!(mode >= 0 && mode <= 4)) {
    throw new Exception(`The value '${mode}' is not valid for this usage of the type MidpointRounding. (Parameter 'mode')`);
  }
  return roundTo(x, digits, mode);
}

export function roundMode(x: Decimal, mode: number) {
  return roundToMode(x, 0, mode);
}

// --checkedDivision: the divisor of a decimal division or remainder, which .NET rejects when it is zero
export function nonZero(divisor: Decimal): Decimal {
  return divisor.m === 0n ? divideByZero() : divisor;
}

export function truncate(x: Decimal) { return roundTo(x, 0, MidpointRounding.ToZero); }
export function ceiling(x: Decimal) { return roundTo(x, 0, MidpointRounding.ToPositiveInfinity); }
export function floor(x: Decimal) { return roundTo(x, 0, MidpointRounding.ToNegativeInfinity); }

const ONE_POINT_ZERO = make(10n, 1);

// As FSharp.Core's PowDecimal, whose multiplications start from the literal 1.0M and so decide the scale
function powPositive(x: Decimal, n: number): Decimal {
  switch (n) {
    case 0: return ONE_POINT_ZERO;
    case 1: return x;
    case 2: return op_Multiply(x, x);
    case 3: return op_Multiply(op_Multiply(x, x), x);
    case 4: { const v = op_Multiply(x, x); return op_Multiply(v, v); }
    default: {
      const h = powPositive(x, Math.floor(n / 2));
      const v = op_Multiply(h, h);
      return n % 2 === 0 ? v : op_Multiply(v, x);
    }
  }
}

export function pow(x: Decimal, n: number) {
  return n >= 0 ? powPositive(x, n) : op_Division(ONE_POINT_ZERO, powPositive(x, -n));
}

export function sqrt(x: Decimal) {
  if (x.m < 0n) {
    throw new Exception("No square root of a negative decimal");
  }
  // The integer square root of the value scaled to 56 decimals has 28 decimals
  const n = x.m * pow10(2 * MAX_SCALE - x.s);
  if (n === 0n) {
    return get_Zero;
  }
  // Newton's iteration from above the root decreases until it reaches the floor of the root
  let r = 1n << BigInt((n.toString(2).length >> 1) + 1);
  for (let next = (r + n / r) >> 1n; next < r; next = (r + n / r) >> 1n) {
    r = next;
  }
  const f = fit(r, MAX_SCALE);
  return trim(f.m, f.s, 0);
}

// .NET's DecAddSub: the sign bit of a zero sum comes from the operand that was not rescaled
function addSub(x: Decimal, y: Decimal, ym: bigint, negY: boolean) {
  const d = x.s - y.s;
  const r = d === 0 ? fit(x.m + ym, x.s)
    : d < 0 ? fit(x.m * pow10(-d) + ym, y.s)
      : fit(x.m + ym * pow10(d), x.s);
  if (r.m !== 0n) {
    return r;
  }
  const negX = isNegative(x);
  return zeroSign(r, d === 0 ? negX : d < 0 ? (x.m === 0n ? negY : negX) : (ym === 0n ? negX : negY));
}

export function op_Addition(x: Decimal, y: Decimal) {
  return addSub(x, y, y.m, isNegative(y));
}

export function op_Subtraction(x: Decimal, y: Decimal) {
  return addSub(x, y, -y.m, !isNegative(y));
}

export function op_Multiply(x: Decimal, y: Decimal) {
  const p = x.m * y.m;
  const s = x.s + y.s;
  if (x.m < UINT32 && x.m > -UINT32 && y.m < UINT32 && y.m > -UINT32) {
    // .NET's 32x32 fast path: a product below 10^-47 is a plain zero
    return s > MAX_SCALE + 19 ? get_Zero : zeroSign(fit(p, s), isNegative(x) !== isNegative(y));
  }
  // .NET's general path gives a zero product scale 0
  return p === 0n ? get_Zero : zeroSign(fit(p, s), p < 0n);
}

export function op_Division(x: Decimal, y: Decimal) {
  let den = y.m;
  if (den === 0n) {
    divideByZero();
  }
  const preferred = Math.max(x.s - y.s, 0);
  if (x.m === 0n) {
    return zeroSign(make(0n, preferred), isNegative(x) !== isNegative(y));
  }
  let num = x.s < y.s ? x.m * pow10(y.s - x.s) : x.m;
  if (den < 0n) {
    den = -den;
    num = -num;
  }
  // An exact quotient keeps the preferred scale, trailing zeros included
  if (num % den === 0n) {
    return fit(num / den, preferred);
  }
  // Otherwise as many digits as fit 96 bits and 28 decimals, rounded half to even
  num *= pow10(MAX_SCALE - preferred);
  let q = num / den;
  const a = q < 0n ? -q : q;
  let k = 0;
  while (a >= pow10(29 + k)) {
    k++;
  }
  for (; ; k++) {
    if (k > MAX_SCALE) {
      overflow();
    }
    q = divHalfEven(num, den * pow10(k));
    if (q <= MAX && q >= -MAX) {
      break;
    }
  }
  return q === 0n ? zeroSign(get_Zero, isNegative(x) !== isNegative(y)) : trim(q, MAX_SCALE - k, 0);
}

export function op_Modulus(x: Decimal, y: Decimal) {
  if (y.m === 0n) {
    divideByZero();
  }
  if (x.m === 0n) {
    return x;
  }
  // Deliberately not as .NET in one case: when aligning the scales needs more than 96 bits
  // (Decimal.MaxValue % 1.0000000000000000000000000001m) .NET throws an OverflowException,
  // and this returns the exact remainder, which always fits
  let a = x.m, b = y.m;
  const d = x.s - y.s;
  if (d < 0) {
    a *= pow10(-d);
  } else if (d > 0) {
    b *= pow10(d);
  }
  const absA = a < 0n ? -a : a, absB = b < 0n ? -b : b;
  // .NET returns the dividend itself, scale included, when it is the smaller one
  return absA < absB ? x : zeroSign(make(a % b, Math.max(x.s, y.s)), a < 0n);
}

export function op_UnaryNegation(x: Decimal) {
  return x.m === 0n ? new Decimal(0n, x.s, !x.n) : make(-x.m, x.s);
}

export function op_UnaryPlus(x: Decimal) {
  return x;
}

export const add = op_Addition;
export const subtract = op_Subtraction;
export const multiply = op_Multiply;
export const divide = op_Division;
export const remainder = op_Modulus;
export const negate = op_UnaryNegation;

export function toString(x: Decimal): string {
  if (x.s === 0) {
    return x.m.toString();
  }
  const negative = x.m < 0n;
  const digits = (negative ? -x.m : x.m).toString().padStart(x.s + 1, "0");
  const i = digits.length - x.s;
  return (negative ? "-" : "") + digits.slice(0, i) + "." + digits.slice(i);
}

// Digits with an optional sign, decimal point and exponent, as splitNumber and the compiler write them
function ofString(str: string, negative: boolean): Decimal {
  let exponent = 0;
  const e = str.search(/e/i);
  if (e >= 0) {
    exponent = Number(str.slice(e + 1));
    str = str.slice(0, e);
  }
  if (str.charCodeAt(0) === 45 /* - */) {
    negative = !negative;
    str = str.slice(1);
  }
  const point = str.indexOf(".");
  let scale = -exponent;
  if (point >= 0) {
    scale += str.length - point - 1;
    str = str.slice(0, point) + str.slice(point + 1);
  }
  let m = BigInt(str);
  if (scale < 0) {
    m *= pow10(-scale);
    scale = 0;
  }
  return zeroSign(fit(negative ? -m : m, scale), negative);
}

// Returns undefined when the string does not have the format allowed by the style
function parseWithStyle(str: string, style: number): Decimal | undefined {
  const parts = splitNumber(str, style);
  return parts === undefined ? undefined : ofString(parts.digits, parts.negative);
}

export function tryParse(str: string, style: number, defValue: FSharpRef<Decimal>): boolean {
  try {
    const d = parseWithStyle(str, style);
    if (d === undefined) {
      return false;
    }
    defValue.contents = d;
    return true;
  } catch {
    return false;
  }
}

export function parse(str: string, style: number = NumberStyles.Number): Decimal {
  if (str == null) {
    throw ArgumentNullException_$ctor_Z721C83C5("s");
  }
  const d = parseWithStyle(str, style);
  if (d === undefined) {
    throw FormatException_$ctor_Z721C83C5(`The input string '${str}' was not in a correct format.`);
  }
  return d;
}

const MAX_SAFE_INTEGER = BigInt(Number.MAX_SAFE_INTEGER);

export function toNumber(x: Decimal): number {
  if (x.s === 0 || x.m === 0n) {
    return x.n ? -0 : Number(x.m);
  }
  // Two doubles that are exact divide to the correctly rounded quotient
  if (x.s <= 22 && x.m <= MAX_SAFE_INTEGER && x.m >= -MAX_SAFE_INTEGER) {
    return Number(x.m) / 10 ** x.s;
  }
  return Number(toString(x));
}

export function toChar(x: Decimal): string {
  const n = toNumber(truncate(x));
  if (n < 0 || n > 65535 || isNaN(n)) {
    throw OverflowException_$ctor_Z721C83C5("Value was either too large or too small for a character.");
  }
  return String.fromCharCode(n);
}

export function toBigInt(x: Decimal): bigint {
  return x.s === 0 ? x.m : x.m / pow10(x.s);
}

export function toInt8(x: Decimal): int8 { return bigInt.toInt8(toBigInt(x)); }
export function toUInt8(x: Decimal): uint8 { return bigInt.toUInt8(toBigInt(x)); }
export function toInt16(x: Decimal): int16 { return bigInt.toInt16(toBigInt(x)); }
export function toUInt16(x: Decimal): uint16 { return bigInt.toUInt16(toBigInt(x)); }
export function toInt32(x: Decimal): int32 { return bigInt.toInt32(toBigInt(x)); }
export function toUInt32(x: Decimal): uint32 { return bigInt.toUInt32(toBigInt(x)); }
export function toInt64(x: Decimal): int64 { return bigInt.toInt64(toBigInt(x)); }
export function toUInt64(x: Decimal): uint64 { return bigInt.toUInt64(toBigInt(x)); }
export function toInt128(x: Decimal): int128 { return bigInt.toInt128(toBigInt(x)); }
export function toUInt128(x: Decimal): uint128 { return bigInt.toUInt128(toBigInt(x)); }
export function toNativeInt(x: Decimal): nativeint { return bigInt.toNativeInt(toBigInt(x)); }
export function toUNativeInt(x: Decimal): unativeint { return bigInt.toUNativeInt(toBigInt(x)); }

export function toFloat16(x: Decimal): float16 { return toNumber(x); }
export function toFloat32(x: Decimal): float32 { return toNumber(x); }
export function toFloat64(x: Decimal): float64 { return toNumber(x); }

// The first double outside the decimal range
const MAX_DOUBLE = 2 ** 96;

// As .NET up to 10: the double rounded half to even to 15 (float32: 7) significant digits
function fromFloat(n: number, significantDigits: number): Decimal {
  if (!isFinite(n) || Math.abs(n) >= MAX_DOUBLE) {
    overflow();
  }
  if (n === 0) {
    return get_Zero;
  }
  // toFixed(100) writes the exact binary value of anything a decimal can hold
  const exact = Math.abs(n) >= 1e21 ? BigInt(n).toString() : n.toFixed(100);
  const point = exact.indexOf(".");
  let m = BigInt(point < 0 ? exact : exact.slice(0, point) + exact.slice(point + 1));
  let s = point < 0 ? 0 : exact.length - point - 1;
  // .NET clamps the scale to 28 before it rounds to the significant digits
  if (s > MAX_SCALE) {
    m = divHalfEven(m, pow10(s - MAX_SCALE));
    s = MAX_SCALE;
  }
  const excess = (m < 0n ? -m : m).toString().length - significantDigits;
  if (excess > 0) {
    m = divHalfEven(m, pow10(excess));
    s -= excess;
  }
  if (s < 0) {
    m *= pow10(-s);
    s = 0;
  }
  return trim(m, s, 0);
}

export function fromFloat64(n: number): Decimal {
  return fromFloat(n, 15);
}

export function fromFloat32(n: number): Decimal {
  return fromFloat(n, 7);
}

export function fromIntArray(bits: ArrayLike<number>) {
  return fromInts(bits[0]!, bits[1]!, bits[2]!, bits[3]!);
}

export function fromInts(low: number, mid: number, high: number, signExp: number) {
  const isNegative = signExp < 0;
  const scale = (signExp >> 16) & 0x7F;
  return fromParts(low, mid, high, isNegative, scale);
}

export function fromParts(low: number, mid: number, high: number, isNegative: boolean, scale: number) {
  if (scale > MAX_SCALE) {
    throw ArgumentOutOfRangeException_$ctor_Z721C83C5("scale");
  }
  const m = (BigInt(high >>> 0) << 64n) | (BigInt(mid >>> 0) << 32n) | BigInt(low >>> 0);
  return zeroSign(make(isNegative ? -m : m, scale), isNegative);
}

export function getBits(d: Decimal) {
  const negative = isNegative(d);
  const m = negative ? -d.m : d.m;
  return [
    Number(BigInt.asIntN(32, m)),
    Number(BigInt.asIntN(32, m >> 32n)),
    Number(BigInt.asIntN(32, m >> 64n)),
    (d.s << 16) | (negative ? 0x80000000 : 0),
  ];
}

// The digits of |x| rounded half away from zero to sd significant digits, and the exponent of the first
function significant(x: Decimal, sd: number | undefined): [string, number] {
  let a = x.m < 0n ? -x.m : x.m;
  let digits = a.toString();
  let e = a === 0n ? 0 : digits.length - 1 - x.s;
  if (sd !== undefined) {
    if (digits.length > sd) {
      a = divHalfUp(a, pow10(digits.length - sd));
      digits = a.toString();
      if (digits.length > sd) {
        digits = digits.slice(0, sd);
        e++;
      }
    } else {
      digits = digits.padEnd(sd, "0");
    }
  }
  return [digits, e];
}

function exponential(x: Decimal, digits: string, e: number) {
  return (x.m < 0n ? "-" : "") + digits[0] + (digits.length > 1 ? "." + digits.slice(1) : "") + (e < 0 ? "e" : "e+") + e;
}

function toExponential(x: Decimal, dp?: number): string {
  const [digits, e] = significant(x, dp === undefined ? undefined : dp + 1);
  return exponential(x, digits, e);
}

function toFixed(x: Decimal, dp?: number): string {
  if (dp === undefined || dp === x.s) {
    return toString(x);
  }
  return dp > x.s
    ? toString(make(x.m * pow10(dp - x.s), dp))
    : toString(make(divHalfUp(x.m, pow10(x.s - dp)), dp));
}

function toPrecision(x: Decimal, sd?: number): string {
  if (sd === undefined) {
    return toString(x);
  }
  const [digits, e] = significant(x, sd);
  if (sd <= e) {
    return exponential(x, digits, e);
  }
  const body = e < 0
    ? "0." + "0".repeat(-e - 1) + digits
    : digits.slice(0, e + 1) + (sd > e + 1 ? "." + digits.slice(e + 1) : "");
  return (x.m < 0n && /[1-9]/.test(digits) ? "-" : "") + body;
}
