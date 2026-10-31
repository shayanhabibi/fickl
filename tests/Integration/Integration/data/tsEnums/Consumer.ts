// Type-checked with the snapshot: TsEnum values as TypeScript code uses them. `goUp` in the snapshot passes `Dir.Up` to a function typed by the string enum in dir.d.ts
import { Access, Color, combine, next } from "./Enums.ts";

export const blue: Color = next(Color.Green);
export const name: string | undefined = Color[blue];
export const both: Access = combine(Access.Read, Access.Write);

// @ts-expect-error 5 is not a Color
next(5);
