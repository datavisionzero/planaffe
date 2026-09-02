import type { ComponentProps } from "react";
import ReactMarkdown, { type Components } from "react-markdown";
import remarkGfm from "remark-gfm";
import { cn } from "@/lib/utils";
import { admitUrl } from "./links";

/**
 * The Markdown pipeline of ADR 0007: `react-markdown` with `remark-gfm`, parsed
 * to a component tree, with raw HTML skipped — never interpreted, never set as
 * innerHTML — because what it renders was written by agents quoting things
 * nobody vetted (VISION 13).
 *
 * Links are foreign links. The library's default admits `irc`, `ircs` and
 * `xmpp` beside the three below; ADR 0007 admits exactly `http`, `https` and
 * `mailto`, and a URL with any other scheme, or a relative one, loses its
 * `href` here and stays text (ADR 0017).
 */
const components: Components = {
  h1: ({ className, ...props }) => <h2 className={cn("mt-6 mb-2 text-base font-semibold first:mt-0", className)} {...props} />,
  h2: ({ className, ...props }) => <h3 className={cn("mt-5 mb-2 text-sm font-semibold first:mt-0", className)} {...props} />,
  h3: ({ className, ...props }) => <h4 className={cn("mt-4 mb-1 text-sm font-medium first:mt-0", className)} {...props} />,
  p: ({ className, ...props }) => <p className={cn("my-2 leading-6 first:mt-0 last:mb-0", className)} {...props} />,
  a: ({ className, href, ...props }) =>
    href === undefined ? (
      <span className={cn("text-muted-foreground", className)} {...props} />
    ) : (
      <a
        href={href}
        target="_blank"
        rel="noopener noreferrer"
        className={cn("text-brand underline-offset-4 hover:underline", className)}
        {...props}
      />
    ),
  ul: ({ className, ...props }) => <ul className={cn("my-2 list-disc pl-5 marker:text-muted-foreground", className)} {...props} />,
  ol: ({ className, ...props }) => <ol className={cn("my-2 list-decimal pl-5 marker:text-muted-foreground", className)} {...props} />,
  li: ({ className, ...props }) => <li className={cn("my-0.5 leading-6", className)} {...props} />,
  blockquote: ({ className, ...props }) => (
    <blockquote className={cn("my-2 border-l-2 pl-3 text-muted-foreground", className)} {...props} />
  ),
  hr: ({ className, ...props }) => <hr className={cn("my-4", className)} {...props} />,
  code: ({ className, children, ...props }) => {
    // A fenced block arrives as `code` inside `pre` with a language class; an
    // inline span arrives bare. Highlighting is decided later (ADR 0017).
    const fenced = /language-/.test(className ?? "");

    return (
      <code
        className={cn(
          "font-mono text-[0.85em]",
          !fenced && "rounded-sm bg-muted px-1 py-0.5",
          className,
        )}
        {...props}
      >
        {children}
      </code>
    );
  },
  pre: ({ className, ...props }) => (
    <pre className={cn("my-3 overflow-x-auto rounded-md border bg-muted p-3 text-xs leading-5", className)} {...props} />
  ),
  table: ({ className, ...props }) => (
    <div className="my-3 overflow-x-auto">
      <table className={cn("w-full border-collapse text-sm", className)} {...props} />
    </div>
  ),
  th: ({ className, ...props }) => (
    <th className={cn("border-b px-2 py-1 text-left font-medium", className)} {...props} />
  ),
  td: ({ className, ...props }) => <td className={cn("border-b px-2 py-1 align-top", className)} {...props} />,
  input: ({ className, ...props }) =>
    props.type === "checkbox" ? (
      <input className={cn("mr-1.5 align-middle accent-brand", className)} {...props} disabled />
    ) : null,
};

export function Markdown({ children, className }: { children: string; className?: string }) {
  return (
    <div className={cn("text-sm", className)}>
      <ReactMarkdown remarkPlugins={[remarkGfm]} skipHtml urlTransform={admitUrl} components={components}>
        {children}
      </ReactMarkdown>
    </div>
  );
}

export type MarkdownProps = ComponentProps<typeof Markdown>;
