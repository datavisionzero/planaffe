import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { admitUrl } from "./links";
import { Markdown } from "./Markdown";

describe("the Markdown pipeline (ADR 0007)", () => {
  it("renders GitHub-flavoured Markdown to components", () => {
    render(<Markdown>{"| a | b |\n|---|---|\n| 1 | 2 |\n\n- [x] done\n- [ ] open\n\n~~gone~~"}</Markdown>);

    expect(screen.getByRole("table")).toBeInTheDocument();
    expect(screen.getAllByRole("checkbox")).toHaveLength(2);
    expect(screen.getByText("gone").tagName).toBe("DEL");
  });

  // ADR 0020: the one departure from CommonMark. A person types into a text
  // area and presses Enter; nothing that reaches here is hard-wrapped, so a
  // newline is only ever meant.
  it("makes a line break of a single newline", () => {
    const { container } = render(<Markdown>{"Zeile eins\nZeile zwei\n\nAbsatz zwei"}</Markdown>);

    expect(container.querySelectorAll("br")).toHaveLength(1);
    expect(container.querySelectorAll("p")).toHaveLength(2);
  });

  it("never interprets HTML", () => {
    const { container } = render(
      <Markdown>{'before <img src="x" onerror="alert(1)"> <script>alert(1)</script> after'}</Markdown>,
    );

    expect(container.querySelector("img")).toBeNull();
    expect(container.querySelector("script")).toBeNull();
    expect(container.textContent).toContain("before");
    expect(container.textContent).toContain("after");
  });

  // ADR 0007, addendum: image syntax is never loaded, from anywhere. It
  // becomes a foreign link labelled with its alt text, so a pixel written into
  // a ticket asks no server for anything.
  it("never loads an image and makes a foreign link of it", () => {
    const { container } = render(
      <Markdown>{"![pixel](https://tracker.example.org/p.png) ![](https://example.org/bare.png) ![own](/logo.png) ![bad](javascript:alert(1))"}</Markdown>,
    );

    expect(container.querySelector("img")).toBeNull();
    expect(document.head.querySelector('link[rel="preload"]')).toBeNull();

    const pixel = screen.getByRole("link", { name: "pixel" });
    expect(pixel).toHaveAttribute("href", "https://tracker.example.org/p.png");
    expect(pixel).toHaveAttribute("rel", "noopener noreferrer");
    expect(screen.getByRole("link", { name: "https://example.org/bare.png" })).toHaveAttribute("href", "https://example.org/bare.png");

    // A relative source and a refused scheme are text like any other link.
    expect(screen.queryByRole("link", { name: "own" })).toBeNull();
    expect(screen.getByText("own")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "bad" })).toBeNull();
  });

  it("refuses an image source outright", () => {
    const img = { type: "element" as const, tagName: "img", properties: {}, children: [] };

    expect(admitUrl("https://example.org/p.png", "src", img)).toBeUndefined();
  });

  it("puts no hast node on the DOM", () => {
    const { container } = render(<Markdown>{"# Title\n\ntext [link](https://example.org)\n\n- item"}</Markdown>);

    expect(container.querySelector("[node]")).toBeNull();
  });

  it("opens links as foreign links and admits three schemes", () => {
    render(<Markdown>{"[ok](https://example.org) [mail](mailto:a@example.org) [no](javascript:alert(1)) [rel](docs/api.md)"}</Markdown>);

    const ok = screen.getByRole("link", { name: "ok" });
    expect(ok).toHaveAttribute("href", "https://example.org");
    expect(ok).toHaveAttribute("rel", "noopener noreferrer");
    expect(ok).toHaveAttribute("target", "_blank");
    expect(screen.getByRole("link", { name: "mail" })).toHaveAttribute("href", "mailto:a@example.org");

    expect(screen.queryByRole("link", { name: "no" })).toBeNull();
    expect(screen.queryByRole("link", { name: "rel" })).toBeNull();
    expect(screen.getByText("no")).toBeInTheDocument();
  });

  it("refuses what the library would have admitted", () => {
    expect(admitUrl("irc://irc.example.org/#x", "href", { type: "element", tagName: "a", properties: {}, children: [] })).toBeUndefined();
    expect(admitUrl("xmpp:a@b", "href", { type: "element", tagName: "a", properties: {}, children: [] })).toBeUndefined();
    expect(admitUrl("http://example.org", "href", { type: "element", tagName: "a", properties: {}, children: [] })).toBe("http://example.org");
  });

  it("marks fenced code apart from inline code", () => {
    const { container } = render(<Markdown>{"say `pa next`\n\n```sh\npa next --claim\n```"}</Markdown>);

    expect(container.querySelector("pre code")).toHaveTextContent("pa next --claim");
    expect(container.querySelectorAll("code")).toHaveLength(2);
  });

  // ADR 0017: nothing tokenizes code here, so the word the fence named is what
  // says what the block is.
  it("names the language of a fenced block and highlights nothing", () => {
    const { container } = render(<Markdown>{"```csharp\nvar x = 1;\n```"}</Markdown>);

    expect(screen.getByText("csharp")).toBeInTheDocument();
    expect(container.querySelectorAll("pre code span")).toHaveLength(0);
  });

  it("says nothing above a fence that named nothing", () => {
    const { container } = render(<Markdown>{"```\nplain\n```"}</Markdown>);

    expect(container.querySelector("pre")).toHaveTextContent("plain");
    expect(container.querySelector("pre")?.previousElementSibling).toBeNull();
  });
});
