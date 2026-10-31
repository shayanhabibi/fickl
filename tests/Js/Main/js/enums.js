export const Dir = Object.freeze({ Up: "UP", Down: "DOWN" });

export function move(dir) {
    return "moved " + dir;
}

// The member values differ from the F# CompiledName, which shows F# reads them from the object
export const Level = Object.freeze({ Low: "low-from-object", High: "high-from-object" });

export function rawHigh() {
    return "high-from-object";
}
