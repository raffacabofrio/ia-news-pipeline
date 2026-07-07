import { defineConfig } from "vite";
import path from "node:path";

const themeRoot = path.resolve(import.meta.dirname);

export default defineConfig({
  build: {
    emptyOutDir: true,
    manifest: true,
    outDir: path.join(themeRoot, "assets", "dist"),
    rollupOptions: {
      input: {
        theme: path.join(themeRoot, "src", "scripts", "main.js"),
      },
    },
  },
});
