// Type-checked with the compiled snapshot: each F# type abbreviation is a type alias of what it stands for
import { User, Shape_Circle, apply, userName } from "./TypeAlias.ts";
import type {
    AccountId, Anything, Callback, Distance$1, FSharpList, Greeter, Greeters$1, Handler, Nested_Id, Nested_Deeper_Ids, Other_Id, Json, Names,
    Lookup$2, MaybeUser, Meters, Nothing, Pair$1, Pairs, Point, Reducer, Settings, Shapes, StructPoint, Token,
    UserId, UserMap,
} from "./TypeAlias.ts";
import { Handler as runHandler, numbers } from "./TypeAlias.ts";

type Equal<A, B> = (<T>() => T extends A ? 1 : 2) extends (<T>() => T extends B ? 1 : 2) ? true : false;
const same = <A, B>(_: Equal<A, B>) => {};

same<UserId, number>(true);
same<AccountId, UserId>(true);
same<Meters, number>(true);
same<Distance$1, number>(true);
same<Token, string>(true);
same<Nested_Id, string>(true);
same<Nested_Deeper_Ids[number], string>(true);
same<Other_Id, number>(true);
same<Point, [number, number]>(true);
same<StructPoint, [number, number]>(true);
same<Pair$1<string>, [string, string]>(true);
same<Pairs extends Iterable<infer T> ? T : never, [number, number]>(true);
same<FSharpList, typeof numbers>(true);
same<UserMap, Parameters<typeof userName>[0]>(true);
same<Lookup$2<string, number> extends Iterable<[infer K, infer V]> ? [K, V] : never, [string, typeof numbers]>(true);
same<Handler, (arg0: string) => void>(true);
same<Reducer, (arg0: number) => (arg0: string) => number>(true);
same<Callback, (arg0: string, arg1: number) => boolean>(true);
same<Greeter, { Greet(name: string): string }>(true);
same<Names, Iterable<string>>(true);
same<Nothing, void>(true);
same<Json, unknown>(true);
same<Settings, { Depth: number, Verbose: boolean }>(true);

// `obj` is `any`, and `unknown` is not
const anything: Anything = 1;
export const anyLength: number = anything.length;
declare const json: Json;
// @ts-expect-error an unknown value must be narrowed
export const jsonLength: number = json.length;

// The aliases name the types of the compiled functions
const users: UserMap = null as any;
const id: UserId = 1;
export const user: MaybeUser = userName(users, id);
export const shapes: Shapes = [Shape_Circle(1)];
export const first: number | undefined = (numbers satisfies FSharpList).head;

// A value named like an abbreviation is still the value
runHandler((text: string) => { text.trim(); });

// Function parameters are uncurried, so the curried alias is not the type of the parameter
const reducer: Reducer = (state) => (text) => state + text.length;
// @ts-expect-error
apply(reducer, 1);

export const greeters: Greeters$1<Greeter> = [{ Greet: (name) => name }];
// @ts-expect-error the type argument must be an IGreeter
export type NotGreeters = Greeters$1<string>;

// @ts-expect-error a private abbreviation is not exported
import type { Secret } from "./TypeAlias.ts";
// @ts-expect-error a number is not a pair
export const pair: Pair$1<number> = 1;
