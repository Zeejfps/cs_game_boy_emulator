import { execSync } from 'child_process';
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync } from 'fs';
import { dirname, join, resolve } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const PACKAGE_DIR = join(__dirname, '..');
const PROJECT_DIR = join(PACKAGE_DIR, '..');
const TMP  = join(PACKAGE_DIR, '.publish-tmp');
const DIST = join(PACKAGE_DIR, 'dist');

// Numeric semver-ish comparator (e.g. "10.0.6" > "9.0.12"). String-sort gets
// this wrong because '1' < '9' lexicographically.
const compareVersions = (a, b) => {
  const pa = a.split('.').map(Number);
  const pb = b.split('.').map(Number);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const diff = (pa[i] ?? 0) - (pb[i] ?? 0);
    if (diff !== 0) return diff;
  }
  return 0;
};

// Bootstrap the package's own devDeps (typescript) if a fresh checkout hasn't
// installed them yet — the script is invoked via `npm --prefix`, which doesn't
// auto-install the target package's deps.
if (!existsSync(join(PACKAGE_DIR, 'node_modules'))) {
  console.log('Installing package dependencies…');
  execSync('npm install', { stdio: 'inherit', cwd: PACKAGE_DIR });
}

console.log('Publishing C# project…');
execSync(`dotnet publish "${PROJECT_DIR}" -c Release -o "${TMP}"`, { stdio: 'inherit' });

// Microsoft.NET.Sdk.WebAssembly emits the AppBundle under wwwroot/_framework/.
// Boot manifest is embedded inside dotnet.js (no separate dotnet.boot.json), so we
// just mirror _framework/ verbatim — content-hashed filenames and all.
const FRAMEWORK = join(TMP, 'wwwroot', '_framework');
if (!existsSync(FRAMEWORK)) throw new Error(`Expected ${FRAMEWORK} after publish`);

rmSync(DIST, { recursive: true, force: true });
mkdirSync(DIST, { recursive: true });

let copied = 0;
for (const entry of readdirSync(FRAMEWORK, { withFileTypes: true })) {
  if (!entry.isFile()) continue;
  // Skip pre-compressed siblings — npm consumers' bundlers/servers handle compression.
  if (entry.name.endsWith('.br') || entry.name.endsWith('.gz')) continue;
  cpSync(join(FRAMEWORK, entry.name), join(DIST, entry.name));
  copied++;
}
console.log(`Copied ${copied} runtime files from _framework/.`);

// dotnet.d.ts isn't part of publish output — pull it from the runtime pack so
// src/dotnet.d.ts stays in sync with whatever runtime version we just published against.
// `dotnet --info` reports "Base Path: <dotnetRoot>/sdk/<version>/", so walk up two
// levels with `path.resolve` to get <dotnetRoot> in a way that works on both
// Windows (backslashes) and Unix (forward slashes).
const sdkBase = execSync('dotnet --info', { encoding: 'utf8' })
  .split('\n').find(l => l.includes('Base Path:'))?.split('Base Path:')[1]?.trim();
if (!sdkBase) throw new Error('Could not determine dotnet SDK base path from `dotnet --info`');
const dotnetRoot = resolve(sdkBase, '..', '..');
const packRoot = join(dotnetRoot, 'packs', 'Microsoft.NETCore.App.Runtime.Mono.browser-wasm');
if (!existsSync(packRoot)) throw new Error(`Expected runtime pack at ${packRoot}`);

// Constrain to the major version the C# project targets so the d.ts matches
// the runtime the publish step actually used (not just whatever's newest on disk).
const csprojPath = join(PROJECT_DIR, 'GameboyEmulator.Wasm.csproj');
const tfmMatch = readFileSync(csprojPath, 'utf8').match(/<TargetFramework>net(\d+)\.(\d+)<\/TargetFramework>/);
if (!tfmMatch) throw new Error(`Could not parse TargetFramework from ${csprojPath}`);
const tfmMajor = Number(tfmMatch[1]);
const versions = readdirSync(packRoot)
  .filter(v => Number(v.split('.')[0]) === tfmMajor)
  .sort(compareVersions);
if (versions.length === 0) throw new Error(`No runtime pack matching net${tfmMajor}.x found under ${packRoot}`);
const dts = join(packRoot, versions[versions.length - 1], 'runtimes', 'browser-wasm', 'native', 'dotnet.d.ts');
cpSync(dts, join(PACKAGE_DIR, 'src', 'dotnet.d.ts'));
cpSync(dts, join(DIST, 'dotnet.d.ts'));
console.log(`Updated dotnet.d.ts from ${versions[versions.length - 1]}.`);

rmSync(TMP, { recursive: true, force: true });

console.log('Compiling TypeScript…');
execSync('npx tsc', { stdio: 'inherit', cwd: PACKAGE_DIR });

console.log('Done → dist/');
