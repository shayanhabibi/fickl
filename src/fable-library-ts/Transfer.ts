// fickl: F# values to and from plain data that structuredClone, postMessage and IndexedDB copy without loss
import {
  type TypeInfo, getElementType, getGenerics, getRecordElements, getRecordField, getTupleElements, getUnionCaseFields,
  getUnionCases, getUnionFields, isArray, isRecord, isTuple, isUnion, makeRecord, makeUnion,
} from "./Reflection.ts";
import { Some } from "./Option.ts";
import { compare } from "./Util.ts";
import Decimal from "./Decimal.ts";
import { ofArray as listOfArray, toArray as listToArray } from "./List.ts";
import { ofArray as mapOfArray, toArray as mapToArray } from "./Map.ts";
import { ofArray as setOfArray, toArray as setToArray } from "./Set.ts";

const optionName = "Microsoft.FSharp.Core.FSharpOption`1";
const listName = "Microsoft.FSharp.Collections.FSharpList`1";
const mapName = "Microsoft.FSharp.Collections.FSharpMap`2";
const setName = "Microsoft.FSharp.Collections.FSharpSet`1";
const decimalName = "System.Decimal";

const structuralComparer = { Compare: (x: any, y: any) => compare(x, y) };

/** A union case as plain data: the case name, and its fields in order. */
export interface PlainUnion {
  case: string;
  fields: unknown[];
}

/** Whether a Some is written wrapped, `{ value }`, so that it stays apart from None. */
function wrapsSome(t: TypeInfo): boolean {
  const inner = getGenerics(t)[0];
  return inner == null || inner.fullname === optionName || inner.fullname === "Microsoft.FSharp.Core.Unit" || inner.fullname === "System.Object";
}

/** Copies an F# value into plain objects and arrays, by its type: records become objects with their field names,
 * union cases `{ case, fields }`, lists and sets arrays, maps arrays of [key, value] pairs and decimals strings.
 * None is null. Other values are kept as they are. */
export function toPlain(value: any, t: TypeInfo): any {
  if (value == null && t.fullname !== optionName) { return value; }
  switch (t.fullname) {
    case optionName: {
      if (value == null) { return null; }
      const inner = toPlain(value instanceof Some ? value.value : value, getGenerics(t)[0]!);
      return wrapsSome(t) ? { value: inner } : inner;
    }
    case listName: {
      const elementType = getGenerics(t)[0]!;
      return Array.from(listToArray<any>(value) as ArrayLike<any>, (x) => toPlain(x, elementType));
    }
    case setName: {
      const elementType = getGenerics(t)[0]!;
      return Array.from(setToArray<any>(value) as ArrayLike<any>, (x) => toPlain(x, elementType));
    }
    case mapName: {
      const [keyType, valueType] = getGenerics(t) as [TypeInfo, TypeInfo];
      return Array.from(mapToArray<any, any>(value) as ArrayLike<[any, any]>, ([k, v]) => [toPlain(k, keyType), toPlain(v, valueType)]);
    }
    case decimalName:
      return value.toString();
  }
  if (isArray(t)) {
    const elementType = getElementType(t)!;
    return ArrayBuffer.isView(value) ? value : Array.from(value as ArrayLike<any>, (x) => toPlain(x, elementType));
  }
  if (isTuple(t)) {
    return getTupleElements(t).map((elementType, i) => toPlain(value[i], elementType));
  }
  if (isRecord(t)) {
    const plain: Record<string, any> = {};
    for (const field of getRecordElements(t)) {
      plain[field[0]] = toPlain(getRecordField(value, field), field[1]);
    }
    return plain;
  }
  if (isUnion(t)) {
    const [uci, fields] = getUnionFields(value, t);
    const fieldTypes = getUnionCaseFields(uci);
    const plain: PlainUnion = { case: uci.name, fields: fields.map((x, i) => toPlain(x, fieldTypes[i]![1])) };
    return plain;
  }
  return value;
}

/** Rebuilds an F# value of the type from the plain data `toPlain` gives, restoring prototypes, lists, maps and sets. */
export function ofPlain(data: any, t: TypeInfo): any {
  if (data == null && t.fullname !== optionName) { return data; }
  switch (t.fullname) {
    case optionName: {
      if (data == null) { return undefined; }
      const inner = ofPlain(wrapsSome(t) ? data.value : data, getGenerics(t)[0]!);
      return makeUnion(getUnionCases(t)[1]!, [inner]);
    }
    case listName: {
      const elementType = getGenerics(t)[0]!;
      return listOfArray((data as any[]).map((x) => ofPlain(x, elementType)));
    }
    case setName: {
      const elementType = getGenerics(t)[0]!;
      return setOfArray((data as any[]).map((x) => ofPlain(x, elementType)), structuralComparer);
    }
    case mapName: {
      const [keyType, valueType] = getGenerics(t) as [TypeInfo, TypeInfo];
      const entries = (data as [any, any][]).map(([k, v]): [any, any] => [ofPlain(k, keyType), ofPlain(v, valueType)]);
      return mapOfArray(entries, structuralComparer);
    }
    case decimalName:
      return new Decimal(data);
  }
  if (isArray(t)) {
    const elementType = getElementType(t)!;
    return ArrayBuffer.isView(data) ? data : (data as any[]).map((x) => ofPlain(x, elementType));
  }
  if (isTuple(t)) {
    return getTupleElements(t).map((elementType, i) => ofPlain(data[i], elementType));
  }
  if (isRecord(t)) {
    return makeRecord(t, getRecordElements(t).map(([name, fieldType]) => ofPlain(data[name], fieldType)));
  }
  if (isUnion(t)) {
    const plain = data as PlainUnion;
    const uci = getUnionCases(t).find((c) => c.name === plain.case);
    if (uci === undefined) {
      throw new Error(`${t.fullname} has no case ${plain.case}`);
    }
    const fieldTypes = getUnionCaseFields(uci);
    return makeUnion(uci, fieldTypes.map(([, fieldType], i) => ofPlain(plain.fields[i], fieldType)));
  }
  return data;
}

/** A deep copy of an F# value through structuredClone, with its prototypes restored. */
export function clone(value: any, t: TypeInfo): any {
  return ofPlain(structuredClone(toPlain(value, t)), t);
}
