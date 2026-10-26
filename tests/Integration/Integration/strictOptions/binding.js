export class inspect {
    Read(value) {
        return value === null ? "null" : value === undefined ? "undefined" : value;
    }
}
export const binding = new inspect();
