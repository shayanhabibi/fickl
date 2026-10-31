// Type-checked with the snapshot: methods returning `this` keep a subinterface's members in reach
import { type IQuery, type ISelect, select } from "./ReturnsThis.ts";

export function chain(query: ISelect): string {
    return query.where("a > 1").columns(["a"]).limit(10).text();
}

export function fromCompiled(): ISelect {
    return select().limit(1).columns(["b"]);
}

// A TypeScript class implementing the interface returns its own type from the inherited methods
export class Logged implements ISelect {
    log: string[] = [];
    where(condition: string): this { this.log.push(condition); return this; }
    limit(count: number): this { this.log.push(String(count)); return this; }
    columns(names: string[]): this { this.log.push(names.join()); return this; }
    text(): string { return this.log.join(" "); }
    flush(): string[] { return this.log; }
}

export const flushed: string[] = new Logged().where("x").flush();

export function base(query: IQuery): IQuery {
    return query.where("y");
}
