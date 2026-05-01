import { defineConfig, type Plugin } from 'vite';
import { execSync } from 'node:child_process';
import path from 'node:path';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// CI sets APP_VERSION to the git tag (e.g. "v0.1.0"). Locally, fall back to a
// short git description so dev builds still get a unique token.
function resolveAppVersion(): string {
  if (process.env.APP_VERSION) return process.env.APP_VERSION;
  try {
    return execSync('git describe --tags --always --dirty', { cwd: __dirname }).toString().trim();
  } catch {
    return 'dev';
  }
}
const APP_VERSION = resolveAppVersion();

const WASM_PREFIX = '/wasm/';
const WASM_DIR = path.resolve(__dirname, '../GameboyEmulator.Wasm/package/dist');
const SKIP_FILES = new Set(['index.js', 'index.d.ts', 'dotnet.d.ts', 'package.json']);

const MIME: Record<string, string> = {
  '.js':   'application/javascript',
  '.mjs':  'application/javascript',
  '.wasm': 'application/wasm',
  '.json': 'application/json',
  '.map':  'application/json',
};

// Make the gameboy-emulator npm package's runtime files available at /wasm/*.
// Dev: a middleware streams files from the package's dist folder.
// Build: emit each file as a Vite asset under wasm/ in the build output.
function serveWasmAssets(): Plugin {
  return {
    name: 'serve-wasm-assets',
    configureServer(server) {
      if (!fs.existsSync(WASM_DIR)) {
        server.config.logger.warn(
          `[serve-wasm-assets] ${WASM_DIR} does not exist. Run \`npm run build:wasm\` first.`,
        );
      }
      server.middlewares.use((req, res, next) => {
        const url = req.url;
        if (!url || !url.startsWith(WASM_PREFIX)) return next();
        const rel = url.slice(WASM_PREFIX.length).split('?')[0];
        const filePath = path.join(WASM_DIR, rel);
        if (!filePath.startsWith(WASM_DIR)) return next();
        fs.stat(filePath, (err, stat) => {
          if (err || !stat.isFile()) {
            res.statusCode = 404;
            res.end();
            return;
          }
          res.setHeader('Content-Type', MIME[path.extname(filePath).toLowerCase()] ?? 'application/octet-stream');
          res.setHeader('Content-Length', stat.size);
          fs.createReadStream(filePath).pipe(res);
        });
      });
    },
    generateBundle() {
      if (!fs.existsSync(WASM_DIR)) {
        this.warn(`${WASM_DIR} does not exist. Run \`npm run build:wasm\` before \`vite build\`.`);
        return;
      }
      for (const name of fs.readdirSync(WASM_DIR)) {
        if (SKIP_FILES.has(name)) continue;
        const full = path.join(WASM_DIR, name);
        if (!fs.statSync(full).isFile()) continue;
        this.emitFile({
          type: 'asset',
          fileName: `wasm/${name}`,
          source: fs.readFileSync(full),
        });
      }
    },
  };
}

export default defineConfig({
  plugins: [serveWasmAssets()],
  define: {
    __APP_VERSION__: JSON.stringify(APP_VERSION),
  },
});
