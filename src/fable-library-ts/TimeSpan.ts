import { FSharpRef } from "./Types.ts";
import { Exception, comparePrimitives, padWithZeros } from "./Util.ts";
import { fromFloat64, type int64 } from "./BigInt.ts";
import { ArgumentOutOfRangeException_$ctor_Z721C83C5, OverflowException_$ctor_Z721C83C5 } from "./System.ts";

// TimeSpan in runtime just becomes a number representing milliseconds
export type TimeSpan = number;

/**
 * Calls:
 * - `Math.ceil` if the `value` is **negative**
 * - `Math.floor` if the `value` is **positive**
 * @param value Value to round
 */
function signedRound(value: number) {
  return value < 0 ? Math.ceil(value) : Math.floor(value);
}

const maxTicks = 9223372036854775807n;
const minTicks = -9223372036854775808n;

// Int64.MaxValue ticks are 922337203685477.5807ms, which a double cannot hold:
// the closest one is .625. `ticks` maps these two values back to the Int64 limits.
export const maxValue: TimeSpan = 922337203685477.625;
export const minValue: TimeSpan = -922337203685477.625;

function checkOverflow(ms: number): TimeSpan {
  if (ms > maxValue || ms < minValue) {
    throw OverflowException_$ctor_Z721C83C5("TimeSpan overflowed because the duration is too long.");
  }
  return ms;
}

function fromParts(d: number, h: number, m: number, s: number, ms: number): TimeSpan {
  return d * 86400000 + h * 3600000 + m * 60_000 + s * 1000 + ms;
}

export function create(d: number = 0, h: number = 0, m: number = 0, s: number = 0, ms: number = 0) {
  switch (arguments.length) {
    case 1:
      // ticks
      return fromTicks(arguments[0]);
    case 3:
      // h,m,s
      d = 0, h = arguments[0], m = arguments[1], s = arguments[2], ms = 0;
      break;
    default:
      // d,h,m,s,ms
      break;
  }
  const total = fromParts(d, h, m, s, ms);
  if (total > maxValue || total < minValue) {
    throw ArgumentOutOfRangeException_$ctor_Z721C83C5("TimeSpan overflowed because the duration is too long.");
  }
  return total;
}

export function fromTicks(ticks: number | bigint): TimeSpan {
  const n = Number(ticks);
  if (!Number.isSafeInteger(n)) {
    // Beyond 2^53 ticks: divide in BigInt so whole milliseconds stay exact,
    // the sub-millisecond rest is rounded to the closest double
    const t = BigInt(ticks);
    return Number(t / 10000n) + Number(t % 10000n) / 10000;
  }
  const ms = n / 10000;
  // `ticks` truncates ms * 10000 like .NET does. When the division rounded down,
  // take the next double so that the same tick count is read back.
  return Math.abs(ms * 10000) < Math.abs(n) ? ms * (1 + Number.EPSILON) : ms;
}

export function fromDays(d: number, h: number = 0, m: bigint = 0n, s: bigint = 0n, ms: bigint = 0n) {
  return checkOverflow(fromParts(d, h, Number(m), Number(s), Number(ms)));
}

export function fromHours(h: number, m: bigint = 0n, s: bigint = 0n, ms: bigint = 0n) {
  return checkOverflow(fromParts(0, h, Number(m), Number(s), Number(ms)));
}

export function fromMinutes(m: number | bigint, s: bigint = 0n, ms: bigint = 0n) {
  return checkOverflow(fromParts(0, 0, Number(m), Number(s), Number(ms)));
}

export function fromSeconds(s: number | bigint, ms: bigint = 0n) {
  return checkOverflow(fromParts(0, 0, 0, Number(s), Number(ms)));
}

export function fromMilliseconds(ms: number | bigint) {
  return checkOverflow(Number(ms));
}

export function days(ts: TimeSpan) {
  return signedRound(ts / 86400000);
}

export function hours(ts: TimeSpan) {
  return signedRound(ts % 86400000 / 3600000);
}

export function minutes(ts: TimeSpan) {
  return signedRound(ts % 3600000 / 60_000);
}

export function seconds(ts: TimeSpan) {
  return signedRound(ts % 60_000 / 1000);
}

export function milliseconds(ts: TimeSpan) {
  return signedRound(ts % 1000);
}

export function ticks(ts: TimeSpan): int64 {
  checkOverflow(ts);
  if (Number.isInteger(ts)) {
    return BigInt(ts) * 10000n;
  }
  const t = fromFloat64(ts * 10000);
  return t > maxTicks ? maxTicks : t < minTicks ? minTicks : t;
}

export function totalDays(ts: TimeSpan) {
  return ts / 86400000;
}

export function totalHours(ts: TimeSpan) {
  return ts / 3600000;
}

export function totalMinutes(ts: TimeSpan) {
  return ts / 60_000;
}

export function totalSeconds(ts: TimeSpan) {
  return ts / 1000;
}

export function negate(ts: TimeSpan) {
  return ts * -1;
}

export function add(ts1: number, ts2: number) {
  return checkOverflow(ts1 + ts2);
}

export function subtract(ts1: number, ts2: number) {
  return checkOverflow(ts1 - ts2);
}

export function multiply(ts: TimeSpan, factor: number) {
  return checkOverflow(ts * factor);
}

export function divide(ts: TimeSpan, b: number) {
  return checkOverflow(ts / b);
}

export const op_Addition = add;
export const op_Subtraction = subtract;
export const op_Multiply = multiply;
export const op_Division = divide;

export const compare = comparePrimitives;
export const compareTo = comparePrimitives;

export function duration(x: number) {
  return Math.abs(x);
}

export function toString(ts: TimeSpan, format = "c", _provider?: any) {
  if (["c", "g", "G"].indexOf(format) === -1) {
    throw new Exception("Custom formats are not supported");
  }
  const d = Math.abs(days(ts));
  const h = Math.abs(hours(ts));
  const m = Math.abs(minutes(ts));
  const s = Math.abs(seconds(ts));
  const sign = ts < 0 ? "-" : "";
  // The seven digits after the seconds, read from the ticks so that sub-millisecond digits are kept
  let fraction = Math.abs(milliseconds(ts)) * 10000;
  if (ts >= minValue && ts <= maxValue) {
    const t = ticks(ts);
    fraction = Number((t < 0n ? -t : t) % 10000000n);
  }
  const digits = padWithZeros(fraction, 7);
  const dayPart = d === 0 && format !== "G" ? "" : format === "c" ? d + "." : d + ":";
  const fractionPart = format === "G"
    ? "." + digits
    : fraction === 0 ? "" : "." + (format === "g" ? digits.replace(/0+$/, "") : digits);
  return `${sign}${dayPart}${format === "g" ? h : padWithZeros(h, 2)}:${padWithZeros(m, 2)}:${padWithZeros(s, 2)}${fractionPart}`;
}

export function parse(str: string) {
  const firstDot = str.search("\\.");
  const firstColon = str.search("\\:");
  if (firstDot === -1 && firstColon === -1) { // There is only a day ex: 4
    const d = parseInt(str, 0);
    if (isNaN(d)) {
      throw new Exception(`String '${str}' was not recognized as a valid TimeSpan.`);
    } else {
      return checkOverflow(fromParts(d, 0, 0, 0, 0));
    }
  }
  if (firstColon > 0) { // process time part
    // WIP: (-?)(((\d+)\.)?([0-9]|0[0-9]|1[0-9]|2[0-3]):(\d+)(:\d+(\.\d{1,7})?)?|\d+(?:(?!\.)))
    const r = /^(-?)((\d+)\.)?(?:0*)([0-9]|0[0-9]|1[0-9]|2[0-3]):(?:0*)([0-5][0-9]|[0-9])(:(?:0*)([0-5][0-9]|[0-9]))?\.?(\d+)?$/.exec(str);
    if (r != null && r[4] != null && r[5] != null) {
      let d = 0;
      let ms = 0;
      let s = 0;
      const sign = r[1] != null && r[1] === "-" ? -1 : 1;
      const h = +r[4];
      const m = +r[5];
      if (r[3] != null) {
        d = +r[3];
      }
      if (r[7] != null) {
        s = +r[7];
      }
      if (r[8] != null) {
        // Depending on the number of decimals passed, we need to adapt the numbers
        switch (r[8].length) {
          case 1: ms = +r[8] * 100; break;
          case 2: ms = +r[8] * 10; break;
          case 3: ms = +r[8]; break;
          case 4: ms = +r[8] / 10; break;
          case 5: ms = +r[8] / 100; break;
          case 6: ms = +r[8] / 1000; break;
          case 7: ms = +r[8] / 10000; break;
          default:
            throw new Exception(`String '${str}' was not recognized as a valid TimeSpan.`);
        }
      }
      return checkOverflow(sign * fromParts(d, h, m, s, ms));
    }
  }
  throw new Exception(`String '${str}' was not recognized as a valid TimeSpan.`);
}

export function tryParse(v: string, defValue: FSharpRef<number>): boolean {
  try {
    defValue.contents = parse(v);
    return true;
  } catch {
    return false;
  }
}
