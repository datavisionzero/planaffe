import type { UrlTransform } from "react-markdown";

/**
 * Links in issue content are foreign links (ADR 0007). The library's default
 * admits `irc`, `ircs` and `xmpp` beside the three below; planaffe admits
 * exactly `http`, `https` and `mailto`, and a URL with any other scheme, or a
 * relative one, loses its `href` and stays text (ADR 0017).
 *
 * An image source is never admitted, whatever its scheme or origin: images in
 * Markdown are not loaded (ADR 0007, addendum). The pipeline turns image
 * syntax into a link before this runs, so a `src` reaching here is one that
 * slipped past that, and it is dropped rather than fetched.
 */
const admitted = new Set(["http:", "https:", "mailto:"]);

export const admitUrl: UrlTransform = (url, key) => {
  if (key === "src") {
    return undefined;
  }

  try {
    return admitted.has(new URL(url).protocol) ? url : undefined;
  } catch {
    return undefined;
  }
};

/** The part of a hast node the image rewrite reads and writes. */
type HastNode = {
  type: string;
  tagName?: string;
  properties?: Record<string, unknown>;
  children?: HastNode[];
  value?: string;
};

/**
 * A rehype plugin that turns every `img` into a foreign link to its source,
 * labelled with its alt text, or with the URL where the alt text is empty. It
 * runs before `admitUrl`, so the link it leaves is judged like any other: a
 * scheme outside the three becomes text. Nothing is requested from the server
 * behind the URL — a tracking pixel in a ticket stays a line of text.
 */
export function imagesAsLinks() {
  return (tree: HastNode) => {
    rewrite(tree);
  };
}

function rewrite(node: HastNode) {
  for (const child of node.children ?? []) {
    if (child.type === "element" && child.tagName === "img") {
      const src = typeof child.properties?.src === "string" ? child.properties.src : "";
      const alt = typeof child.properties?.alt === "string" ? child.properties.alt.trim() : "";
      const title = child.properties?.title ?? undefined;

      child.tagName = "a";
      child.properties = title === undefined ? { href: src } : { href: src, title };
      child.children = [{ type: "text", value: alt === "" ? src : alt }];
    } else {
      rewrite(child);
    }
  }
}
