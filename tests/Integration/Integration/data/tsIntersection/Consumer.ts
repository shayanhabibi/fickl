// Type-checked with the compiled snapshot: an I2<Named, Aged> must be both a Named and an Aged
import { type Aged, type Named, asNamed, describe, inResizeArray, inUnion, ofPerson, Person } from "./Intersection.ts";

const person = new Person("Ada", 36, "t");
const both: Named & Aged = ofPerson(person);

export const name: string = both.name;
export const age: number = both.age;
export const text: string = describe(both);
export const named: Named = asNamed({ name: "Ada", age: 36 });
export const count: number = inResizeArray([both]);
export const either: any = inUnion("text");

// @ts-expect-error a Named alone is not Named & Aged, which it would be were the parameter a union
describe({ name: "Ada" });

// @ts-expect-error nor is an Aged alone
describe({ age: 36 });

// @ts-expect-error every element is Named & Aged, not Named & Aged[]
inResizeArray([{ name: "Ada" }]);

// @ts-expect-error the union is (Named & Aged) | string, so a Named alone is in neither case
inUnion({ name: "Ada" });
