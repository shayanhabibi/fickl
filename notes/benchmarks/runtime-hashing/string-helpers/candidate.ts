export function filter(pred: (char: string) => boolean, x: string) {
  // Keep legacy behavior for null and non-primitive interop values.
  if (typeof x !== "string") {
    return (x as string).split("").filter((c) => pred(c)).join("");
  }
  let result = "";
  for (let i = 0; i < x.length; i++) {
    const c = x[i];
    if (pred(c)) { result += c; }
  }
  return result;
}
