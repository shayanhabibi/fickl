// Type-checked with the snapshot: the compiled guards narrow their argument for TypeScript callers
import { assertUser, checkUser, isText, tryUser } from "./TypeGuards.ts";

export function describe(value: unknown): string {
    // @ts-expect-error unknown has no length before it is narrowed
    value.length;

    if (isText(value)) {
        return value.toUpperCase();
    }

    if (tryUser(value)) {
        return value.name;
    }

    return "";
}

export function asserted(value: unknown): string {
    assertUser(value);
    return value.name;
}

export function checked(value: unknown): string {
    checkUser(value);
    return value.name;
}
