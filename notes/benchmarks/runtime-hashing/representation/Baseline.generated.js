
import { FSharpRef, Record } from "../../../../../../../../temp/fable-library-js/Types.js";
import { class_type, record_type, int32_type } from "../../../../../../../../temp/fable-library-js/Reflection.js";
import { concat } from "../../../../../../../../temp/fable-library-js/Array.js";

export class Point extends Record {
    constructor(X, Y) {
        super();
        this.X = (X | 0);
        this.Y = (Y | 0);
    }
}

export function Point_$reflection() {
    return record_type("StructFixture.Point", [], Point, () => [["X", int32_type], ["Y", int32_type]]);
}

export function localKernel(iterations, seed) {
    let checksum = seed;
    for (let i = 0; i <= (iterations - 1); i++) {
        const p = new Point(checksum + i, checksum - i);
        const q = new Point(p.Y + 3, p.Y);
        checksum = (((q.X ^ p.X) + q.Y) | 0);
    }
    return checksum | 0;
}

export class MutablePoint extends Record {
    constructor(X, Cell) {
        super();
        this.X = (X | 0);
        this.Cell = Cell;
    }
}

export function MutablePoint_$reflection() {
    return record_type("StructFixture.MutablePoint", [], MutablePoint, () => [["X", int32_type], ["Cell", record_type("Microsoft.FSharp.Core.FSharpRef`1", [int32_type], FSharpRef, () => [["contents", int32_type]])]]);
}

export class Value extends Record {
    constructor(x) {
        super();
        this.x = (x | 0);
    }
    get Value() {
        const _ = this;
        return _.x | 0;
    }
}

export function Value_$reflection() {
    return class_type("StructFixture.Value", undefined, Value, class_type("System.ValueType"));
}

export function Value_$ctor_Z524259A4(x) {
    return new Value(x);
}

export function Value__get_X(_) {
    return _.x | 0;
}

export function mutableCopy() {
    let a = new MutablePoint(1, new FSharpRef(5));
    let b = a;
    b.X = 9;
    b.Cell.contents = 7;
    return new Int32Array([a.X, b.X, a.Cell.contents, b.Cell.contents]);
}

export function boxPoint(x) {
    return new Point(x, x + 1);
}

export function interfaceValue(x) {
    return Value_$ctor_Z524259A4(x).Value | 0;
}

export function closurePoint(x) {
    const p = new Point(x, x + 1);
    return () => p;
}

export function observe() {
    const boxed = boxPoint(4);
    const closure = closurePoint(8);
    return concat([mutableCopy(), new Int32Array([boxed.X, boxed.Y, interfaceValue(6), closure().X, closure().Y])], Int32Array);
}

