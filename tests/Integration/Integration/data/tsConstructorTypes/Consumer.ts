// Type-checked with the compiled snapshot: each line under a `@ts-expect-error` must be an error,
// which it is not when a constructor is `any`
import { Box$1, Circle, MyEl, Shape, boxCtor, create, elCtor, register, shapeCtor } from "./ConstructorTypes.ts";

export const el: MyEl = new elCtor("a");

// @ts-expect-error a constructor of MyEl does not make numbers
export const notNumber: number = new elCtor("a");

// @ts-expect-error a constructor of MyEl is not one of Circle
export const notCircle: Circle = new elCtor("a");

// @ts-expect-error a constructor of Circle is not one of MyEl
export const wrongClass: MyEl = register("c", Circle);

// The type argument is inferred from the class
export const circle: Circle = create(Circle, 1);
export const box: Box$1<number> = new boxCtor(1);

// @ts-expect-error
export const notBox: Box$1<string> = new boxCtor(1);

// An abstract construct signature accepts abstract and concrete classes, and cannot be instantiated
export const abstractCtor: typeof shapeCtor = Shape;
export const concreteCtor: typeof shapeCtor = Circle;

// @ts-expect-error cannot create an instance of an abstract class
export const shape: Shape = new shapeCtor();
