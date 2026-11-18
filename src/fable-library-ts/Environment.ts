// process.env is Node-specific. A browser page has no environment variables, so there every
// variable is missing, which .NET reports as null.
declare const process: { env: Record<string, string | undefined> } | undefined;

export function getEnvironmentVariable(name: string): string {
  return (typeof process !== "undefined" && process.env != null ? process.env[name] : undefined) as string;
}
