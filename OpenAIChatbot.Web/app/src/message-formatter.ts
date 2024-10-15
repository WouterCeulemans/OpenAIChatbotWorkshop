import hljs from "highlight.js";
import markdownit, { Options } from "markdown-it";

export function formatAsMarkdown(input: string): string {
    const mdOptions: Options = {
        linkify: true,
        typographer: true,
        highlight: (str, lang) => {
            if (lang && hljs.getLanguage(lang)) {
                try {
                    return hljs.highlight(str, { language: lang }).value;
                } catch (__) { }
            }

            return '';
        }
    };
    const md = new markdownit(mdOptions);
    const result = md.render(input);
    return result;
}