import { FSharpRef, Record, Union, recordFieldsSymbol } from "./Types.ts";
import { Exception, type MutableArray, combineHashCodes, equalArraysWith, fieldKey, type IEquatable, stringHash } from "./Util.ts";
import Decimal from "./Decimal.ts";
import { Some, some } from "./Option.ts";

export type FieldInfo = [string, TypeInfo, PropertyKey?];
export type PropertyInfo = FieldInfo;
// The third element is the position, set by the MethodInfo or ConstructorInfo that owns the parameter
export type ParameterInfo = [string, TypeInfo, number?];
// The parameters of a constructor and the function to call, when it is not the class constructor
export type ConstructorData = [ParameterInfo[], ((...args: any[]) => any)?];

export type Constructor = new (...args: any[]) => any;

export class CaseInfo {
  public declaringType: TypeInfo;
  public tag: number;
  public name: string;
  public fields?: FieldInfo[] | undefined;
  // Builds the custom attributes of the case, only set when the case has some
  public attributes?: (() => any[]) | undefined;

  constructor(
    declaringType: TypeInfo,
    tag: number,
    name: string,
    fields?: FieldInfo[],
    attributes?: () => any[]
  ) {
    this.declaringType = declaringType;
    this.tag = tag;
    this.name = name;
    this.fields = fields;
    this.attributes = attributes;
  }
}

export type EnumCase = [string, number];

function withPositions(parameters: ParameterInfo[]): ParameterInfo[] {
  parameters.forEach((p, i) => p[2] = i);
  return parameters;
}

export class MethodInfo {
  public name: string;
  public parameters: ParameterInfo[];
  public returnType: TypeInfo;
  // Only the methods found by Type.GetMethod, which the compiler resolves, have these
  public isStatic?: boolean | undefined;
  public invoke?: ((target: any, args: any[]) => any) | undefined;

  constructor(
    name: string,
    parameters: ParameterInfo[],
    returnType: TypeInfo,
    isStatic?: boolean,
    invoke?: (target: any, args: any[]) => any,
  ) {
    this.name = name;
    this.parameters = withPositions(parameters);
    this.returnType = returnType;
    this.isStatic = isStatic;
    this.invoke = invoke;
  }
}

export class ConstructorInfo {
  public name: string = ".ctor";
  public declaringType: TypeInfo;
  public parameters: ParameterInfo[];
  private factory?: ((...args: any[]) => any) | undefined;

  constructor(
    declaringType: TypeInfo,
    parameters: ParameterInfo[],
    factory?: (...args: any[]) => any,
  ) {
    this.declaringType = declaringType;
    this.parameters = withPositions(parameters);
    this.factory = factory;
  }

  public invoke(args?: any[] | null): any {
    args = args ?? [];
    if (args.length !== this.parameters.length) {
      throw new Exception("Parameter count mismatch.");
    }
    if (this.factory != null) {
      return this.factory(...args);
    }
    const construct = this.declaringType.construct;
    if (construct == null) {
      throw new Exception(`Cannot create an instance of ${this.declaringType.fullname}`);
    }
    return new construct(...args);
  }
}

export class TypeInfo implements IEquatable<TypeInfo> {
  public fullname: string;
  public generics?: TypeInfo[] | undefined;
  public construct?: Constructor | undefined;
  // A thunk when the base type's generic arguments can refer back to this type, see #3607
  private lazyParent?: TypeInfo | (() => TypeInfo) | undefined;
  public fields?: (() => FieldInfo[]) | undefined;
  public cases?: (() => CaseInfo[]) | undefined;
  public enumCases?: EnumCase[] | undefined;
  // Only for classes whose public constructors are not a single parameterless one
  public constructors?: (() => ConstructorData[]) | undefined;
  public isInterface?: boolean;
  // Struct records and unions; a struct class has System.ValueType as parent
  public isStruct?: boolean;

  constructor(
    fullname: string,
    generics?: TypeInfo[],
    construct?: Constructor,
    parent?: TypeInfo | (() => TypeInfo),
    fields?: () => FieldInfo[],
    cases?: () => CaseInfo[],
    enumCases?: EnumCase[],
    constructors?: () => ConstructorData[]
  ) {
    this.fullname = fullname;
    this.generics = generics;
    this.construct = construct;
    this.lazyParent = parent;
    this.fields = fields;
    this.cases = cases;
    this.enumCases = enumCases;
    this.constructors = constructors;
  }
  public get parent(): TypeInfo | undefined {
    if (typeof this.lazyParent === "function") {
      this.lazyParent = this.lazyParent();
    }
    return this.lazyParent;
  }
  public toString() {
    return fullName(this);
  }
  public GetHashCode() {
    return getHashCode(this);
  }
  public Equals(other: TypeInfo) {
    return equals(this, other);
  }
}

export class GenericParameter extends TypeInfo {
  constructor(name: string) {
    super(name);
  }
}

export function getGenerics(t: TypeInfo): TypeInfo[] {
  return t.generics != null ? t.generics : [];
}

// Anonymous records keep an empty `fullname`, their identity is their fields.
function isAnonRecord(t: TypeInfo): boolean {
  return t.fullname === "" && t.fields != null;
}

// .NET names them `<>f__AnonymousType{hash of the field names}`{field count}`
// and puts the field types in the generic arguments, so the name depends
// only on the field names and the full name also on the field types.
function anonRecordName(t: TypeInfo): string {
  const fields = getRecordElements(t);
  const hash = stringHash(fields.map(([k]) => k).join("\0")) >>> 0;
  return "<>f__AnonymousType" + hash + "`" + fields.length;
}

function anonRecordGenerics(t: TypeInfo): TypeInfo[] {
  return getRecordElements(t).map(([, fieldType]) => fieldType);
}

export function getHashCode(t: TypeInfo): number {
  if (isAnonRecord(t)) {
    return combineHashCodes([stringHash(anonRecordName(t)), ...anonRecordGenerics(t).map(getHashCode)]);
  }
  const fullnameHash = stringHash(t.fullname);
  const genHashes: number[] = getGenerics(t).map(getHashCode);
  return combineHashCodes([fullnameHash, ...genHashes]);
}

export function equals(t1: TypeInfo, t2: TypeInfo): boolean {
  if (t1.fullname === "") { // Anonymous records
    return t2.fullname === ""
      && equalArraysWith(getRecordElements(t1),
        getRecordElements(t2),
        ([k1, v1], [k2, v2]) => k1 === k2 && equals(v1, v2));
  } else {
    return t1.fullname === t2.fullname
      && equalArraysWith(getGenerics(t1), getGenerics(t2), equals);
  }
}

export function class_type(
  fullname: string,
  generics?: TypeInfo[],
  construct?: Constructor,
  parent?: TypeInfo | (() => TypeInfo),
  constructors?: () => ConstructorData[]): TypeInfo {
  return new TypeInfo(fullname, generics, construct, parent, undefined, undefined, undefined, constructors);
}

export function interface_type(fullname: string, generics?: TypeInfo[]): TypeInfo {
  const t = new TypeInfo(fullname, generics);
  t.isInterface = true;
  return t;
}

export function record_type(
  fullname: string,
  generics: TypeInfo[],
  construct: Constructor,
  fields: () => FieldInfo[],
  isStruct?: boolean): TypeInfo {
  const t = new TypeInfo(fullname, generics, construct, undefined, fields);
  if (isStruct) { t.isStruct = true; }
  return t;
}

export function anonRecord_type(...fields: FieldInfo[]): TypeInfo {
  return new TypeInfo("", undefined, undefined, undefined, () => fields);
}

export function union_type(
  fullname: string,
  generics: TypeInfo[],
  construct: Constructor,
  cases: () => FieldInfo[][],
  // The custom attributes of each case, by tag
  attributes?: () => any[][],
  isStruct?: boolean): TypeInfo {
  const t: TypeInfo = new TypeInfo(fullname, generics, construct, undefined, undefined, () => {
    const caseNames = construct.prototype.cases() as string[];
    return cases().map((fields, i) =>
      new CaseInfo(t, i, caseNames[i]!, fields, attributes && (() => attributes()[i] ?? [])))
  });
  if (isStruct) { t.isStruct = true; }
  return t;
}

export function tuple_type(...generics: TypeInfo[]): TypeInfo {
  return new TypeInfo("System.Tuple`" + generics.length, generics);
}

export function delegate_type(...generics: TypeInfo[]): TypeInfo {
  return new TypeInfo("System.Func`" + generics.length, generics);
}

export function lambda_type(argType: TypeInfo, returnType: TypeInfo): TypeInfo {
  return new TypeInfo("Microsoft.FSharp.Core.FSharpFunc`2", [argType, returnType]);
}

export function option_type(generic: TypeInfo): TypeInfo {
  const t: TypeInfo = new TypeInfo(
    "Microsoft.FSharp.Core.FSharpOption`1",
    [generic],
    undefined,
    undefined,
    undefined,
    () => [
      new CaseInfo(t, 0, "None"),
      new CaseInfo(t, 1, "Some", [["value", generic]])
    ]
  );
  return t;
}

export function list_type(generic: TypeInfo): TypeInfo {
  const t: TypeInfo = new TypeInfo(
    "Microsoft.FSharp.Collections.FSharpList`1",
    [generic],
    undefined,
    undefined,
    undefined,
    () => [
      new CaseInfo(t, 0, "Empty"),
      new CaseInfo(t, 1, "Cons", [["Head", generic], ["Tail", t]])
    ]
  );
  return t;
}

export function array_type(generic: TypeInfo): TypeInfo {
  return new TypeInfo("[]", [generic]);
}

export function enum_type(fullname: string, underlyingType: TypeInfo, enumCases: EnumCase[]): TypeInfo {
  return new TypeInfo(fullname, [underlyingType], undefined, undefined, undefined, undefined, enumCases);
}

export function measure_type(fullname: string): TypeInfo {
  return new TypeInfo(fullname);
}

export function generic_type(name: string): TypeInfo {
  return new GenericParameter(name);
}

export const obj_type: TypeInfo = new TypeInfo("System.Object");
export const unit_type: TypeInfo = new TypeInfo("Microsoft.FSharp.Core.Unit");
export const char_type: TypeInfo = new TypeInfo("System.Char");
export const string_type: TypeInfo = new TypeInfo("System.String");
export const bool_type: TypeInfo = new TypeInfo("System.Boolean");
export const int8_type: TypeInfo = new TypeInfo("System.SByte");
export const uint8_type: TypeInfo = new TypeInfo("System.Byte");
export const int16_type: TypeInfo = new TypeInfo("System.Int16");
export const uint16_type: TypeInfo = new TypeInfo("System.UInt16");
export const int32_type: TypeInfo = new TypeInfo("System.Int32");
export const uint32_type: TypeInfo = new TypeInfo("System.UInt32");
export const int64_type: TypeInfo = new TypeInfo("System.Int64");
export const uint64_type: TypeInfo = new TypeInfo("System.UInt64");
export const int128_type: TypeInfo = new TypeInfo("System.Int128");
export const uint128_type: TypeInfo = new TypeInfo("System.UInt128");
export const nativeint_type: TypeInfo = new TypeInfo("System.IntPtr");
export const unativeint_type: TypeInfo = new TypeInfo("System.UIntPtr");
export const float16_type: TypeInfo = new TypeInfo("System.Half");
export const float32_type: TypeInfo = new TypeInfo("System.Single");
export const float64_type: TypeInfo = new TypeInfo("System.Double");
export const decimal_type: TypeInfo = new TypeInfo("System.Decimal");
export const bigint_type: TypeInfo = new TypeInfo("System.Numerics.BigInteger");

export function name(info: FieldInfo | ParameterInfo | TypeInfo | CaseInfo | MethodInfo | ConstructorInfo): string {
  if (Array.isArray(info)) {
    return info[0];
  } else if (info instanceof TypeInfo) {
    const elemType = getElementType(info);
    if (elemType != null) {
      return name(elemType) + "[]";
    } else if (isAnonRecord(info)) {
      return anonRecordName(info);
    } else {
      const i = info.fullname.lastIndexOf(".");
      return i === -1 ? info.fullname : info.fullname.slice(i + 1);
    }
  } else {
    return info.name;
  }
}

export function fullName(t: TypeInfo): string {
  const elemType = getElementType(t);
  if (elemType != null) {
    return fullName(elemType) + "[]";
  } else if (isAnonRecord(t)) {
    return anonRecordName(t) + "[" + anonRecordGenerics(t).map((x) => fullName(x)).join(",") + "]";
  } else if (t.generics == null || t.generics.length === 0) {
    return t.fullname
  } else {
    return t.fullname + "[" + t.generics.map((x) => fullName(x)).join(",") + "]";
  }
}

export function namespace(t: TypeInfo): string {
  const elemType = getElementType(t);
  if (elemType != null) {
    return namespace(elemType);
  } else {
    const i = t.fullname.lastIndexOf(".");
    return i === -1 ? "" : t.fullname.slice(0, i);
  }
}

export function isArray(t: TypeInfo): boolean {
  return getElementType(t) != null;
}

export function getElementType(t: TypeInfo): TypeInfo | undefined {
  return t.fullname === "[]" && t.generics?.length === 1 ? t.generics[0] : undefined;
}

export function isGenericType(t: TypeInfo) {
  return t.generics != null && t.generics.length > 0;
}

export function isGenericParameter(t: TypeInfo) {
  return t instanceof GenericParameter;
}

export function isEnum(t: TypeInfo) {
  return t.enumCases != null && t.enumCases.length > 0;
}

const valueTypeName = "System.ValueType";

// Struct tuples, value options and nullable value types have the type info of their reference counterparts
const valueTypeNames = new Set([
  "System.Char", "System.Boolean", "System.SByte", "System.Byte", "System.Int16", "System.UInt16",
  "System.Int32", "System.UInt32", "System.Int64", "System.UInt64", "System.Int128", "System.UInt128",
  "System.IntPtr", "System.UIntPtr", "System.Half", "System.Single", "System.Double", "System.Decimal",
  "System.Numerics.BigInteger", "System.Guid", "System.TimeSpan", "System.DateTime", "System.DateTimeOffset",
  "System.DateOnly", "System.TimeOnly", "System.Text.Rune", "System.Collections.Generic.KeyValuePair`2",
  valueTypeName, "System.Enum",
]);

export function isClass(t: TypeInfo): boolean {
  if (t.isInterface || t.isStruct || t.enumCases != null || valueTypeNames.has(t.fullname)) {
    return false;
  }
  for (let parent = t.parent; parent != null; parent = parent.parent) {
    if (parent.fullname === valueTypeName) {
      return false;
    }
  }
  return true;
}

export function isSubclassOf(t1: TypeInfo, t2: TypeInfo): boolean {
  return (t2.fullname === obj_type.fullname) || (t1.parent != null && (t1.parent.Equals(t2) || isSubclassOf(t1.parent, t2)));
}

function isErasedToNumber(t: TypeInfo) {
  return isEnum(t) || [
    int8_type.fullname,
    uint8_type.fullname,
    int16_type.fullname,
    uint16_type.fullname,
    int32_type.fullname,
    uint32_type.fullname,
    float16_type.fullname,
    float32_type.fullname,
    float64_type.fullname,
  ].includes(t.fullname);
}

function isErasedToBigInt(t: TypeInfo) {
  return isEnum(t) || [
    int64_type.fullname,
    uint64_type.fullname,
    int128_type.fullname,
    uint128_type.fullname,
    nativeint_type.fullname,
    unativeint_type.fullname,
    bigint_type.fullname,
  ].includes(t.fullname);
}

export function isInstanceOfType(t: TypeInfo, o: any) {
  if (t.fullname === obj_type.fullname)
    return true;
  switch (typeof o) {
    case "boolean":
      return t.fullname === bool_type.fullname;
    case "string":
      return t.fullname === string_type.fullname;
    case "function":
      return isFunction(t);
    case "number":
      return isErasedToNumber(t);
    case "bigint":
      return isErasedToBigInt(t);
    default:
      return t.construct != null && o instanceof t.construct;
  }
}

/**
 * This doesn't replace types for fields (records) or cases (unions)
 * but it should be enough for type comparison purposes
 */
export function getGenericTypeDefinition(t: TypeInfo) {
  return t.generics == null ? t : new TypeInfo(t.fullname, t.generics.map(() => obj_type));
}

export function getEnumUnderlyingType(t: TypeInfo): TypeInfo {
  const underlyingType = t.enumCases != null ? t.generics?.[0] : undefined;
  if (underlyingType == null) {
    throw new Exception(`${t.fullname} is not an enum type`);
  }
  return underlyingType;
}

export function getEnumValues(t: TypeInfo): number[] {
  if (isEnum(t) && t.enumCases != null) {
    return t.enumCases.map((kv) => kv[1]);
  } else {
    throw new Exception(`${t.fullname} is not an enum type`);
  }
}

export function getEnumNames(t: TypeInfo): string[] {
  if (isEnum(t) && t.enumCases != null) {
    return t.enumCases.map((kv) => kv[0]);
  } else {
    throw new Exception(`${t.fullname} is not an enum type`);
  }
}

function getEnumCase(t: TypeInfo, v: number | string): EnumCase {
  if (t.enumCases != null) {
    if (typeof v === "string") {
      for (const kv of t.enumCases) {
        if (kv[0] === v) {
          return kv;
        }
      }
      throw new Exception(`'${v}' was not found in ${t.fullname}`);
    } else {
      for (const kv of t.enumCases) {
        if (kv[1] === v) {
          return kv;
        }
      }
      // .NET returns the number even if it doesn't match any of the cases
      return ["", v];
    }
  } else {
    throw new Exception(`${t.fullname} is not an enum type`);
  }
}

export function parseEnum(t: TypeInfo, str: string): number {
  // TODO: better int parsing here, parseInt ceils floats: "4.8" -> 4
  const value = parseInt(str, 10);
  return getEnumCase(t, isNaN(value) ? str : value)[1];
}

export function tryParseEnum(t: TypeInfo, str: string, defValue: FSharpRef<number>): boolean {
  try {
    defValue.contents = parseEnum(t, str);
    return true;
  } catch {
    return false;
  }
}

export function getEnumName(t: TypeInfo, v: number): string {
  return getEnumCase(t, v)[0];
}

export function isEnumDefined(t: TypeInfo, v: any): boolean {
  try {
    const kv = getEnumCase(t, v);
    return kv[0] != null && kv[0] !== "";
  } catch {
    // supress error
  }
  return false;
}

// FSharpType

export function getUnionCases(t: TypeInfo): CaseInfo[] {
  if (t.cases != null) {
    return t.cases();
  } else {
    throw new Exception(`${t.fullname} is not an F# union type`);
  }
}

export function getRecordElements(t: TypeInfo): FieldInfo[] {
  if (t.fields != null) {
    return t.fields();
  } else {
    throw new Exception(`${t.fullname} is not an F# record type`);
  }
}

export function getTupleElements(t: TypeInfo): TypeInfo[] {
  if (isTuple(t) && t.generics != null) {
    return t.generics;
  } else {
    throw new Exception(`${t.fullname} is not a tuple type`);
  }
}

export function getFunctionElements(t: TypeInfo): [TypeInfo, TypeInfo] {
  if (isFunction(t) && t.generics != null) {
    const gen = t.generics;
    return [gen[0]!, gen[1]!];
  } else {
    throw new Exception(`${t.fullname} is not an F# function type`);
  }
}

export function isUnion(t: any): boolean {
  return t instanceof TypeInfo ? t.cases != null : t instanceof Union;
}

export function isRecord(t: any): boolean {
  return t instanceof TypeInfo ? t.fields != null : t instanceof Record;
}

export function isTuple(t: TypeInfo): boolean {
  return t.fullname.startsWith("System.Tuple");
}

// In .NET this is false for delegates
export function isFunction(t: TypeInfo): boolean {
  return t.fullname === "Microsoft.FSharp.Core.FSharpFunc`2";
}

// FSharpValue

export function getUnionFields(v: any, t: TypeInfo): [CaseInfo, any[]] {
  const cases = getUnionCases(t);
  // Special handling for option types (None is undefined, Some is the value or a Some wrapper)
  if (t.fullname === "Microsoft.FSharp.Core.FSharpOption`1") {
    if (v == null) {
      return [cases[0]!, []]; // None case
    } else {
      const innerValue = v instanceof Some ? v.value : v;
      return [cases[1]!, [innerValue]]; // Some case
    }
  }
  const case_ = cases[v.tag];
  if (case_ == null) {
    throw new Exception(`Cannot find case ${v.name} in union type`);
  }
  return [case_, v.fields];
}

// Like .NET, each call builds new attribute instances
export function getCaseCustomAttributes(uci: CaseInfo, attributeType?: TypeInfo): any[] {
  const attributes = uci.attributes?.() ?? [];
  return attributeType == null || attributeType.fullname === "System.Attribute"
    ? attributes
    : attributes.filter((attribute) => isInstanceOfType(attributeType, attribute));
}

export function getUnionCaseFields(uci: CaseInfo): FieldInfo[] {
  return uci.fields == null ? [] : uci.fields;
}

// This is used as replacement of `FSharpValue.GetRecordFields`
// For `FSharpTypes.GetRecordFields` see `getRecordElements`
// TypeInfo is used when available to enumerate fields by name: anonymous record None fields
// are omitted from the JS object, so Object.keys alone would miss them.
// Boxed anonymous record would are still not covered but this is the best we can do for now
// without adding a __fields__ to every anonymous record being created
export function getRecordFields(v: any, t: TypeInfo): MutableArray<any> {
  if (t.fields != null) {
    return t.fields().map(([key, _, backingKey]) => v[backingKey ?? fieldKey(key)]);
  }
  const fields: ReadonlyArray<readonly [string, PropertyKey]> | undefined = v.constructor?.[recordFieldsSymbol];
  if (fields != null) {
    return fields.map(([, key]) => v[key]);
  }
  return Object.keys(v).map((k) => v[k]);
}

export function getRecordField(v: any, field: FieldInfo): any {
  return v[field[2] ?? fieldKey(field[0])];
}

export function getTupleFields(v: any): MutableArray<any> {
  return v;
}

export function getTupleField(v: any, i: number): any {
  return v[i];
}

export function makeUnion(uci: CaseInfo, values: MutableArray<any>): any {
  const expectedLength = (uci.fields || []).length;
  if (values.length !== expectedLength) {
    throw new Exception(`Expected an array of length ${expectedLength} but got ${values.length}`);
  }
  // Special handling for option types
  if (uci.declaringType.fullname === "Microsoft.FSharp.Core.FSharpOption`1") {
    return uci.tag === 0 ? undefined : some(values[0]);
  }
  const construct = uci.declaringType.construct;
  if (construct == null) {
    return {};
  }
  const isSingleCase = uci.declaringType.cases ? uci.declaringType.cases().length == 1 : false;
  if (isSingleCase) {
    return new construct(...values);
  }
  else {
    return new construct(uci.tag, values);
  }
}

export function makeRecord(t: TypeInfo, values: MutableArray<any>): any {
  const fields = getRecordElements(t);
  if (fields.length !== values.length) {
    throw new Exception(`Expected an array of length ${fields.length} but got ${values.length}`);
  }
  return t.construct != null
    ? new t.construct(...values)
    : fields.reduce((obj, [key, _t, backingKey], i) => {
      obj[backingKey ?? fieldKey(key)] = values[i];
      return obj;
    }, {} as any);
}

export function makeTuple(values: MutableArray<any>, _t: TypeInfo): any {
  return values;
}

export function makeGenericType(t: TypeInfo, generics: TypeInfo[]): TypeInfo {
  return new TypeInfo(
    t.fullname,
    generics,
    t.construct,
    t.parent,
    t.fields,
    t.cases,
    undefined,
    t.constructors);
}

// Public instance constructors, in declaration order. Only types compiled as classes or records have them:
// unions have none, as in .NET, and neither do the types the runtime represents natively (tuples, options...)
export function getConstructors(t: TypeInfo): ConstructorInfo[] {
  if (t.constructors != null) {
    return t.constructors().map(([parameters, factory]) => new ConstructorInfo(t, parameters, factory));
  } else if (t.fields != null) {
    // .NET names the parameters of a record after its fields, starting in lower case
    const parameters = getRecordElements(t).map(([name, fieldType]): ParameterInfo =>
      [isAnonRecord(t) ? name : name.charAt(0).toLowerCase() + name.slice(1), fieldType]);
    return [new ConstructorInfo(t, parameters, (...args) => makeRecord(t, args))];
  } else if (t.cases != null || t.construct == null) {
    return [];
  } else {
    return [new ConstructorInfo(t, [])];
  }
}

// Null when there is none, which the F# type does not say
export function getConstructor(t: TypeInfo, types: TypeInfo[]): ConstructorInfo {
  const found = getConstructors(t).find((c) =>
    c.parameters.length === types.length && c.parameters.every((p, i) => equals(p[1], types[i]!)));
  return (found ?? null) as ConstructorInfo;
}

export function createInstance(t: TypeInfo, consArgs?: any[]): any {
  // TODO: Check if consArgs length is same as t.construct?
  // (Arg types can still be different)
  if (typeof t.construct === "function") {
    return new t.construct(...(consArgs ?? []));
  } else if (isErasedToNumber(t)) {
    return 0;
  } else if (isErasedToBigInt(t)) {
    return 0n;
  } else {
    switch (t.fullname) {
      case obj_type.fullname:
        return {};
      case bool_type.fullname:
        return false;
      case decimal_type.fullname:
        return new Decimal(0);
      case char_type.fullname:
        return "\0";
      default:
        throw new Exception(`Cannot access constructor of ${t.fullname}`);
    }
  }
}

// MethodInfo.IsStatic, for a method found by Type.GetMethod
export function isStatic(method: MethodInfo): boolean {
  if (method.isStatic == null) {
    throw new Exception(`Cannot tell whether ${method.name} is static: only the methods found by Type.GetMethod know`);
  }
  return method.isStatic;
}

// MethodInfo.Invoke, for a method found by Type.GetMethod
export function invoke(method: MethodInfo, target: any, args?: any[] | null): any {
  if (method.invoke == null) {
    throw new Exception(`Cannot invoke ${method.name}: only the methods found by Type.GetMethod can be invoked`);
  }
  const args_ = args ?? [];
  if (!method.isStatic && target == null) {
    throw new Exception("Non-static method requires a target.");
  }
  if (args_.length !== method.parameters.length) {
    throw new Exception("Parameter count mismatch.");
  }
  return method.invoke(target, args_);
}

export function getValue(propertyInfo: PropertyInfo, v: any): any {
  return v[fieldKey(propertyInfo[0])];
}

// Fable.Core.Reflection

function assertUnion(x: any) {
  if (!(x instanceof Union)) {
    throw new Exception(`Value is not an F# union type`);
  }
}

export function getCaseTag(x: any): number {
  assertUnion(x);
  return x.tag;
}

export function getCaseName(x: any): string {
  assertUnion(x);
  return x.cases()[x.tag];
}

export function getCaseFields(x: any): any[] {
  assertUnion(x);
  return x.fields;
}
