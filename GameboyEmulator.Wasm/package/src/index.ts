export interface Emulator {
  // Fill in as [JSExport]ed methods are added on the C# side.
}

export interface InitOptions {
  /** URL where the runtime files are hosted (trailing slash optional). */
  baseUrl: string;
}

export async function init(opts: InitOptions): Promise<Emulator> {
  const baseUrl = opts.baseUrl.endsWith('/') ? opts.baseUrl : opts.baseUrl + '/';
  const { dotnet } = await import(baseUrl + 'dotnet.js');

  const runtime = await dotnet
    .withResourceLoader((_type: string, name: string) => baseUrl + name)
    .create();

  await runtime.runMain();

  const config = runtime.getConfig();
  const assemblyName: string = config.mainAssemblyName ?? 'GameBoyEmulator.Wasm';
  await runtime.getAssemblyExports(assemblyName);

  return {};
}
