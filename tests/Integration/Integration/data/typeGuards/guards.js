export function isUserLike(value) {
    return typeof value === "object" && value !== null && "name" in value;
}
