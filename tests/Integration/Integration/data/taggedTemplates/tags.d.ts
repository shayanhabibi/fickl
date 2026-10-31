export interface Template {
    strings: TemplateStringsArray;
    values: unknown[];
}
export declare function html(strings: TemplateStringsArray, ...values: unknown[]): Template;
