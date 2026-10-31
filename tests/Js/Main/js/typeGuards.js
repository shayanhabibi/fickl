export function isNamed(value) {
    return typeof value === "object" && value !== null && typeof value.name === "string";
}

export function assertNamed(value) {
    if (!isNamed(value)) {
        throw new TypeError("expected a name");
    }
}
