import { codePointCategory, UnicodeCategory } from "./Char.ts";
import { ArgumentException_$ctor_Z384F8060, ArgumentNullException_$ctor_Z721C83C5, ArgumentOutOfRangeException_$ctor_Z721C83C5 } from "./System.ts";
import type { FSharpRef } from "./Types.ts";
import { type IEnumerable, type IEnumerator, toIterator } from "./Util.ts";

// System.Text.Rune is a Unicode scalar value: its code point as a plain number

export type Rune = number;

const replacementChar = 0xFFFD;

function isSurrogateCodePoint(value: number) {
  return value >= 0xD800 && value <= 0xDFFF;
}

export function isValid(value: number): boolean {
  return value >= 0 && value <= 0x10FFFF && !isSurrogateCodePoint(value);
}

export function create(value: number): Rune {
  if (!isValid(value)) {
    throw ArgumentOutOfRangeException_$ctor_Z721C83C5("value");
  }
  return value;
}

export function createFromChar(ch: string): Rune {
  const value = ch.charCodeAt(0);
  if (isSurrogateCodePoint(value)) {
    throw ArgumentOutOfRangeException_$ctor_Z721C83C5("ch");
  }
  return value;
}

function isSurrogatePair(highSurrogate: number, lowSurrogate: number) {
  return highSurrogate >= 0xD800 && highSurrogate <= 0xDBFF && lowSurrogate >= 0xDC00 && lowSurrogate <= 0xDFFF;
}

function fromSurrogatePair(highSurrogate: number, lowSurrogate: number) {
  return (highSurrogate - 0xD800) * 0x400 + (lowSurrogate - 0xDC00) + 0x10000;
}

export function createFromSurrogatePair(highSurrogate: string, lowSurrogate: string): Rune {
  const high = highSurrogate.charCodeAt(0);
  const low = lowSurrogate.charCodeAt(0);
  if (!isSurrogatePair(high, low)) {
    throw ArgumentOutOfRangeException_$ctor_Z721C83C5(high >= 0xD800 && high <= 0xDBFF ? "lowSurrogate" : "highSurrogate");
  }
  return fromSurrogatePair(high, low);
}

export function tryCreate(value: number, result: FSharpRef<Rune>): boolean {
  const valid = isValid(value);
  result.contents = valid ? value : 0;
  return valid;
}

export function tryCreateFromChar(ch: string, result: FSharpRef<Rune>): boolean {
  return tryCreate(ch.charCodeAt(0), result);
}

export function tryCreateFromSurrogatePair(highSurrogate: string, lowSurrogate: string, result: FSharpRef<Rune>): boolean {
  const high = highSurrogate.charCodeAt(0);
  const low = lowSurrogate.charCodeAt(0);
  const valid = isSurrogatePair(high, low);
  result.contents = valid ? fromSurrogatePair(high, low) : 0;
  return valid;
}

// The scalar value that starts at a UTF-16 index, or -1 for a surrogate that is not half of a pair starting there
function readRuneAt(input: string, index: number): number {
  if (input == null) {
    throw ArgumentNullException_$ctor_Z721C83C5("input");
  }
  if (index < 0 || index >= input.length) {
    throw ArgumentOutOfRangeException_$ctor_Z721C83C5("index");
  }
  const value = input.codePointAt(index)!;
  return isSurrogateCodePoint(value) ? -1 : value;
}

export function getRuneAt(input: string, index: number): Rune {
  const value = readRuneAt(input, index);
  if (value < 0) {
    throw ArgumentException_$ctor_Z384F8060("Cannot extract a Unicode scalar value from the specified index in the input.", "index");
  }
  return value;
}

export function tryGetRuneAt(input: string, index: number, result: FSharpRef<Rune>): boolean {
  const value = readRuneAt(input, index);
  result.contents = value < 0 ? 0 : value;
  return value >= 0;
}

export function utf16SequenceLength(rune: Rune): number {
  return rune > 0xFFFF ? 2 : 1;
}

export function utf8SequenceLength(rune: Rune): number {
  return rune < 0x80 ? 1 : rune < 0x800 ? 2 : rune < 0x10000 ? 3 : 4;
}

export function isAscii(rune: Rune): boolean {
  return rune < 0x80;
}

export function isBmp(rune: Rune): boolean {
  return rune <= 0xFFFF;
}

export function plane(rune: Rune): number {
  return rune >> 16;
}

export function toString(rune: Rune): string {
  return String.fromCodePoint(rune);
}

export function getUnicodeCategory(rune: Rune): UnicodeCategory {
  return codePointCategory(rune) as UnicodeCategory;
}

export function isControl(rune: Rune): boolean {
  return codePointCategory(rune) === UnicodeCategory.Control;
}

export function isDigit(rune: Rune): boolean {
  return codePointCategory(rune) === UnicodeCategory.DecimalDigitNumber;
}

export function isLetter(rune: Rune): boolean {
  return codePointCategory(rune) <= UnicodeCategory.OtherLetter;
}

export function isLetterOrDigit(rune: Rune): boolean {
  const category = codePointCategory(rune);
  return category <= UnicodeCategory.OtherLetter || category === UnicodeCategory.DecimalDigitNumber;
}

export function isLower(rune: Rune): boolean {
  return codePointCategory(rune) === UnicodeCategory.LowercaseLetter;
}

export function isUpper(rune: Rune): boolean {
  return codePointCategory(rune) === UnicodeCategory.UppercaseLetter;
}

export function isNumber(rune: Rune): boolean {
  const category = codePointCategory(rune);
  return category >= UnicodeCategory.DecimalDigitNumber && category <= UnicodeCategory.OtherNumber;
}

export function isPunctuation(rune: Rune): boolean {
  const category = codePointCategory(rune);
  return category >= UnicodeCategory.ConnectorPunctuation && category <= UnicodeCategory.OtherPunctuation;
}

export function isSeparator(rune: Rune): boolean {
  const category = codePointCategory(rune);
  return category >= UnicodeCategory.SpaceSeparator && category <= UnicodeCategory.ParagraphSeparator;
}

export function isSymbol(rune: Rune): boolean {
  const category = codePointCategory(rune);
  return category >= UnicodeCategory.MathSymbol && category <= UnicodeCategory.OtherSymbol;
}

export function isWhiteSpace(rune: Rune): boolean {
  return isSeparator(rune) || (rune >= 0x09 && rune <= 0x0D) || rune === 0x85;
}

// Simple case mapping keeps one scalar value: JS's full mapping can expand ('ß' to "SS"), and .NET leaves such a rune unchanged
function oneRune(rune: Rune, mapped: string): Rune {
  const value = mapped.codePointAt(0)!;
  return mapped.length === utf16SequenceLength(value) ? value : rune;
}

export function toUpperInvariant(rune: Rune): Rune {
  // .NET leaves dotless i alone and sends a Greek letter with iota subscript to its one-letter titlecase form
  if (rune === 0x131) {
    return rune;
  }
  if (rune >= 0x1F80 && rune <= 0x1FAF) {
    return rune | 8;
  }
  if (rune === 0x1FB3 || rune === 0x1FC3 || rune === 0x1FF3) {
    return rune + 9;
  }
  return oneRune(rune, String.fromCodePoint(rune).toUpperCase());
}

export function toLowerInvariant(rune: Rune): Rune {
  return oneRune(rune, String.fromCodePoint(rune).toLowerCase());
}

// System.Text.StringRuneEnumerator, which is both the sequence and its enumerator. An unpaired surrogate is read as U+FFFD
export class StringRuneEnumerator implements IEnumerator<Rune>, IEnumerable<Rune> {
  private readonly input: string;
  private index = 0;
  private current: Rune = 0;
  constructor(input: string) { this.input = input; }
  public GetEnumerator(): IEnumerator<Rune> {
    return new StringRuneEnumerator(this.input);
  }
  public "System.Collections.IEnumerable.GetEnumerator"(): IEnumerator<any> {
    return this.GetEnumerator();
  }
  public [Symbol.iterator](): Iterator<Rune> {
    return toIterator(this.GetEnumerator());
  }
  public ["System.Collections.Generic.IEnumerator`1.get_Current"]() {
    return this.current;
  }
  public ["System.Collections.IEnumerator.get_Current"]() {
    return this.current;
  }
  public ["System.Collections.IEnumerator.MoveNext"]() {
    if (this.index >= this.input.length) {
      this.current = 0;
      return false;
    }
    const value = this.input.codePointAt(this.index)!;
    this.index += utf16SequenceLength(value);
    this.current = isSurrogateCodePoint(value) ? replacementChar : value;
    return true;
  }
  public ["System.Collections.IEnumerator.Reset"]() {
    this.index = 0;
    this.current = 0;
  }
  public Dispose() {
    return;
  }
}

export function enumerateRunes(input: string): StringRuneEnumerator {
  return new StringRuneEnumerator(input);
}
