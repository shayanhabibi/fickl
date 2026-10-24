export function parse(str: string): number {
  if (str != null && /\S/.test(str)) {
    const v = +str.replace("_", "");
    if (!Number.isNaN(v)) { return v; }
  }
  throw new Exception(`The input string ${str} was not in a correct format.`);
}
