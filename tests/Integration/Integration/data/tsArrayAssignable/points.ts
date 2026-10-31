export interface Point {
    x: number;
    y: number;
}

export function countPoints(points: Point[]): number {
    return points.length;
}

export function joinAll(parts: string[]): string {
    return parts.join(",");
}

export function sum(values: ArrayLike<number>): number {
    let total = 0;
    for (let i = 0; i < values.length; i++) {
        total += values[i];
    }
    return total;
}

export class Holder {
    items: string[] = [];
}
