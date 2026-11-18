/**
 * .NET custom TimeSpan format strings ("d\\.hh\\:mm\\:ss", "%h", "ss\\.fff"), shared by the
 * number-based and the Temporal-based TimeSpan representations.
 *
 * Both hand over the absolute components of the interval: a custom format never writes a sign.
 */

import { padWithZeros } from "./Util.ts";
import { FormatException_$ctor_Z721C83C5 } from "./System.ts";

function invalidFormat(): never {
  throw FormatException_$ctor_Z721C83C5("Input string was not in a correct format.");
}

/**
 * Formats the absolute components of an interval with a custom format string.
 * @param fraction The part below one second, in ticks (0 to 9,999,999).
 */
export function formatCustom(
  days: number,
  hours: number,
  minutes: number,
  seconds: number,
  fraction: number,
  format: string,
): string {
  let result = "";
  let i = 0;
  while (i < format.length) {
    const ch = format[i];
    let length = 1;
    switch (ch) {
      case "d":
      case "h":
      case "m":
      case "s":
      case "f":
      case "F": {
        while (i + length < format.length && format[i + length] === ch) {
          length++;
        }
        if (ch === "d") {
          if (length > 8) { invalidFormat(); }
          result += padWithZeros(days, length);
        } else if (ch === "f" || ch === "F") {
          if (length > 7) { invalidFormat(); }
          // The leading digits of the seven tick digits, cut off, not rounded
          let digits = padWithZeros(fraction, 7).substring(0, length);
          if (ch === "F") {
            digits = digits.replace(/0+$/, "");
          }
          result += digits;
        } else {
          if (length > 2) { invalidFormat(); }
          result += padWithZeros(ch === "h" ? hours : ch === "m" ? minutes : seconds, length);
        }
        break;
      }
      case "'":
      case "\"": {
        // A quoted literal, in which a backslash still takes the next character as it is
        let j = i + 1;
        let closed = false;
        while (j < format.length && !closed) {
          const c = format[j];
          if (c === ch) {
            closed = true;
          } else if (c === "\\") {
            if (j + 1 >= format.length) { invalidFormat(); }
            result += format[j + 1];
            j++;
          } else {
            result += c;
          }
          j++;
        }
        if (!closed) { invalidFormat(); }
        length = j - i;
        break;
      }
      case "%": {
        // "%d" is the custom specifier "d", where "d" alone would be read as a standard format
        const next = format[i + 1];
        if (next == null || next === "%") { invalidFormat(); }
        result += formatCustom(days, hours, minutes, seconds, fraction, next);
        length = 2;
        break;
      }
      case "\\": {
        const next = format[i + 1];
        if (next == null) { invalidFormat(); }
        result += next;
        length = 2;
        break;
      }
      default:
        // Any other character, a separator included, has to be escaped or quoted
        invalidFormat();
    }
    i += length;
  }
  return result;
}
