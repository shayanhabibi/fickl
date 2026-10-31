export interface Template {
    strings: string[];
    values: unknown[];
}
export declare function html(strings: string[], ...values: unknown[]): Template;
