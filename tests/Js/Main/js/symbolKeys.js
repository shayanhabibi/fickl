export const state = Symbol("state");
export function readState(value) { return value[state]; }
