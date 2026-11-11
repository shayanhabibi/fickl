export let counter = 0;

export function increment() {
    counter++;
}

export function double(x) {
    return x * 2;
}

export default { name: "reexport" };
