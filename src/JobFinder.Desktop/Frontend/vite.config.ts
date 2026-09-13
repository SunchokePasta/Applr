import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { fileURLToPath, URL } from "node:url";

export default defineConfig({
  plugins: [react()],
  base: "./",
  build: {
    outDir: fileURLToPath(new URL("../Web", import.meta.url)),
    emptyOutDir: true
  }
});
