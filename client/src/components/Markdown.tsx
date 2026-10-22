import { isValidElement, useState, type ReactNode } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import hljs from "highlight.js/lib/core";
import csharp from "highlight.js/lib/languages/csharp";
import typescript from "highlight.js/lib/languages/typescript";
import javascript from "highlight.js/lib/languages/javascript";
import bash from "highlight.js/lib/languages/bash";
import json from "highlight.js/lib/languages/json";
import sql from "highlight.js/lib/languages/sql";
import xml from "highlight.js/lib/languages/xml";

hljs.registerLanguage("csharp", csharp);
hljs.registerLanguage("cs", csharp);
hljs.registerLanguage("typescript", typescript);
hljs.registerLanguage("ts", typescript);
hljs.registerLanguage("javascript", javascript);
hljs.registerLanguage("js", javascript);
hljs.registerLanguage("bash", bash);
hljs.registerLanguage("sh", bash);
hljs.registerLanguage("json", json);
hljs.registerLanguage("sql", sql);
hljs.registerLanguage("xml", xml);
hljs.registerLanguage("html", xml);

function languageLabel(language: string) {
  const map: Record<string, string> = {
    csharp: "C#",
    cs: "C#",
    typescript: "TypeScript",
    ts: "TypeScript",
    javascript: "JavaScript",
    js: "JavaScript",
    bash: "Shell",
    sh: "Shell",
    json: "JSON",
    sql: "SQL",
    xml: "XML",
    html: "HTML"
  };
  return map[language] ?? language.toUpperCase();
}

export function CodeBlock({
  code,
  language = "text",
  compact = false
}: {
  code: string;
  language?: string;
  compact?: boolean;
}) {
  const [copied, setCopied] = useState(false);
  const normalized = (language || "text").toLowerCase();
  let html: string;
  try {
    html = hljs.getLanguage(normalized)
      ? hljs.highlight(code, { language: normalized, ignoreIllegals: true }).value
      : hljs.highlightAuto(code).value;
  } catch {
    html = code.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  }

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(code);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      // Clipboard may be unavailable; leave the button silent.
    }
  };

  return (
    <figure className={`code-block ${compact ? "code-compact" : ""}`}>
      <div className="code-head">
        <span>{languageLabel(normalized)}</span>
        <button type="button" onClick={copy} className="code-copy">
          {copied ? "Copied" : "Copy"}
        </button>
      </div>
      <pre>
        <code dangerouslySetInnerHTML={{ __html: html }} />
      </pre>
    </figure>
  );
}

function extractCode(children: ReactNode): { code: string; language: string } | null {
  if (!isValidElement(children)) return null;
  const props = children.props as { className?: string; children?: ReactNode };
  const className = props.className ?? "";
  const match = /language-([\w-]+)/.exec(className);
  const raw = props.children;
  const code = Array.isArray(raw) ? raw.join("") : String(raw ?? "");
  return { code: code.replace(/\n$/, ""), language: match?.[1] ?? "text" };
}

export function Markdown({ children, className = "" }: { children: string; className?: string }) {
  return (
    <div className={`markdown ${className}`.trim()}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          pre({ children }) {
            const extracted = extractCode(children);
            if (!extracted) return <pre>{children}</pre>;
            return <CodeBlock code={extracted.code} language={extracted.language} />;
          },
          code({ className, children, ...props }) {
            const inline = !className && !String(children).includes("\n");
            if (inline) {
              return (
                <code className="inline-code" {...props}>
                  {children}
                </code>
              );
            }
            return (
              <code className={className} {...props}>
                {children}
              </code>
            );
          },
          a({ href, children, ...props }) {
            return (
              <a href={href} target="_blank" rel="noreferrer noopener" {...props}>
                {children}
              </a>
            );
          }
        }}
      >
        {children}
      </ReactMarkdown>
    </div>
  );
}
