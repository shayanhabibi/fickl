// fickl: numeric arrays, which are typed arrays, and their ArrayBuffer

/** The array's own buffer when the array covers all of it, otherwise a copy of the part it covers. */
export function asArrayBuffer(array: unknown): ArrayBuffer {
  // invariant: F# only calls this on numeric arrays, which are typed arrays
  const view = array as ArrayBufferView;
  const buffer = view.buffer as ArrayBuffer;
  return view.byteOffset === 0 && view.byteLength === buffer.byteLength
    ? buffer
    : buffer.slice(view.byteOffset, view.byteOffset + view.byteLength);
}
