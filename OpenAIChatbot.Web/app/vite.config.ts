import { defineConfig } from "vite";

export default defineConfig(({ mode }) => ({
    build: {
        outDir: "../wwwroot/js",
        lib: {
            entry: "src/main.ts",
            name: "openai-chatbot",
            fileName: (format) => `index.js`,
            formats: ["es"]
        },
        minify: mode !== "development"
    }
}));