// A library whose interface the F# module augments
export interface PluginProperties {
    version: string;
}

export function properties(): PluginProperties {
    return { version: "1" } as PluginProperties;
}
