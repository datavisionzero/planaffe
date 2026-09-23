import { Children, isValidElement, type ComponentProps, type ReactNode } from "react";
import ReactMarkdown, { type Components } from "react-markdown";
import remarkBreaks from "remark-breaks";
import remarkGfm from "remark-gfm";
import { cn } from "@/lib/utils";
import { admitUrl, imagesAsLinks } from "./links";

/**
 * The Markdown pipeline of ADR 0007: `react-markdown` with `remark-gfm`, parsed
 * to a component tree, with raw HTML skipped — never interpreted, never set as
 * innerHTML — because what it renders was written by agents quoting things
 * nobody vetted (VISION 13).
 *
 * `remark-breaks` is the third plugin and the one that is not CommonMark: a
 * single newline is a line break rather than a space, because this text is
 * typed into a text area by people who press Enter and expect a break, and
 * because nothing that reaches here is hard-wrapped any more (ADR 0020).
 *
 * Links are foreign links. The library's default admits `irc`, `ircs` and
 * `xmpp` beside the three below; ADR 0007 admits exactly `http`, `https` and
 * `mailto`, and a URL with any other scheme, or a relative one, loses its
 * `href` here and stays text (ADR 0017).
 *
 * Images are never loaded, from anywhere (ADR 0007, addendum): `![alt](url)`
 * becomes a foreign link labelled with its alt text, so a tracking pixel in a
 * ticket costs nobody who opens it their address.
 *
 * Every mapped component takes `node` out of its props before spreading them:
 * react-markdown always passes the hast node, and spread onto an element it
 * would stand in the DOM as `node="[object Object]"`.
 */
const components: Components = {
  h1: ({ node: _node, className, ...props }) => <h2 className={cn("mt-6 mb-2 text-base font-semibold first:mt-0", className)} {...props} />,
  h2: ({ node: _node, className, ...props }) => <h3 className={cn("mt-5 mb-2 text-sm font-semibold first:mt-0", className)} {...props} />,
  h3: ({ node: _node, className, ...props }) => <h4 className={cn("mt-4 mb-1 text-sm font-medium first:mt-0", className)} {...props} />,
  p: ({ node: _node, className, ...props }) => <p className={cn("my-2 leading-6 first:mt-0 last:mb-0", className)} {...props} />,
  a: ({ node: _node, className, href, ...props }) =>
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
  ul: ({ node: _node, className, ...props }) => <ul className={cn("my-2 list-disc pl-5 marker:text-muted-foreground", className)} {...props} />,
  ol: ({ node: _node, className, ...props }) => <ol className={cn("my-2 list-decimal pl-5 marker:text-muted-foreground", className)} {...props} />,
  li: ({ node: _node, className, ...props }) => <li className={cn("my-0.5 leading-6", className)} {...props} />,
  blockquote: ({ node: _node, className, ...props }) => (
    <blockquote className={cn("my-2 border-l-2 pl-3 text-muted-foreground", className)} {...props} />
  ),
  hr: ({ node: _node, className, ...props }) => <hr className={cn("my-4", className)} {...props} />,
  code: ({ node: _node, className, children, ...props }) => {
    // A fenced block arrives as `code` inside `pre` with a language class; an
    // inline span arrives bare. Neither is highlighted: ADR 0017 settles that
    // nothing tokenizes code here, and the fence's own word says the language.
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
  pre: ({ node: _node, className, children, ...props }) => {
    const language = languageOf(children);

    return (
      <div className="my-3 overflow-hidden rounded-md border bg-muted">
        {language !== undefined && (
          <div className="border-b px-3 py-1 font-mono text-[0.7rem] text-muted-foreground">{language}</div>
        )}
        <pre className={cn("overflow-x-auto p-3 text-xs leading-5", className)} {...props}>
          {children}
        </pre>
      </div>
    );
  },
  table: ({ node: _node, className, ...props }) => (
    <div className="my-3 overflow-x-auto">
      <table className={cn("w-full border-collapse text-sm", className)} {...props} />
    </div>
  ),
  th: ({ node: _node, className, ...props }) => (
    <th className={cn("border-b px-2 py-1 text-left font-medium", className)} {...props} />
  ),
  td: ({ node: _node, className, ...props }) => <td className={cn("border-b px-2 py-1 align-top", className)} {...props} />,
  input: ({ node: _node, className, ...props }) =>
    props.type === "checkbox" ? (
      // The box a task list in the description draws. It is disabled — the
      // text is the truth, and it is edited as text — so it is not a form
      // field anybody fills in, and it needs no name the browser could learn.
      // eslint-disable-next-line no-restricted-syntax
      <input className={cn("mr-1.5 align-middle accent-brand", className)} {...props} disabled />
    ) : null,
};

/**
 * The language a fence named, from the `language-…` class `react-markdown` puts
 * on the `code` inside the `pre`. Nothing acts on it but the label: the block
 * is not highlighted (ADR 0017).
 */
function languageOf(children: ReactNode): string | undefined {
  const first = Children.toArray(children)[0];

  if (!isValidElement<{ className?: string }>(first)) {
    return undefined;
  }

  return /language-([\w+#-]+)/.exec(first.props.className ?? "")?.[1];
}

export function Markdown({ children, className }: { children: string; className?: string }) {
  return (
    <div className={cn("text-sm", className)}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm, remarkBreaks]}
        rehypePlugins={[imagesAsLinks]}
        skipHtml
        urlTransform={admitUrl}
        components={components}
      >
        {children}
      </ReactMarkdown>
    </div>
  );
}

export type MarkdownProps = ComponentProps<typeof Markdown>;
