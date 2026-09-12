import { describe, expect, it } from "vitest";
import { aSpacePage } from "@/shared/testing";
import { ancestorsOf, childrenOf, descendantsOf, levels, takesChildren, trailOf } from "./tree";

const pages = [
  aSpacePage("company", "The company"),
  aSpacePage("company/onboarding", "Onboarding"),
  aSpacePage("company/onboarding/day-one", "The first day"),
  aSpacePage("product", "The product"),
];

describe("the tree of a space (ADR 0028)", () => {
  it("reads the pages above one out of its address alone", () => {
    expect(ancestorsOf("company/onboarding/day-one")).toEqual(["company", "company/onboarding"]);
    expect(ancestorsOf("company")).toEqual([]);
  });

  it("takes the children of a page, and the pages directly under the space for none", () => {
    expect(childrenOf(pages, null).map((page) => page.path)).toEqual(["company", "product"]);
    expect(childrenOf(pages, "company").map((page) => page.path)).toEqual(["company/onboarding"]);
    expect(childrenOf(pages, "product")).toEqual([]);
  });

  it("walks from the root down to a page, the page last", () => {
    expect(trailOf(pages, "company/onboarding/day-one").map((page) => page.title)).toEqual([
      "The company",
      "Onboarding",
      "The first day",
    ]);
  });

  // The tree is read for the frame and a page can be opened before it has
  // arrived, so a row that is not there is left out rather than guessed at.
  it("leaves out what the tree does not hold", () => {
    expect(trailOf([], "company/onboarding").map((page) => page.path)).toEqual([]);
  });

  it("takes everything under a page, however deep, and never the page itself", () => {
    expect(descendantsOf(pages, "company").map((page) => page.path)).toEqual([
      "company/onboarding",
      "company/onboarding/day-one",
    ]);
  });

  // Three levels and no fourth (VISION 18): the instance counts from zero, so
  // the third level is `2` and has no room below it.
  it("knows where the third level is", () => {
    expect(levels).toBe(3);
    expect(takesChildren(0)).toBe(true);
    expect(takesChildren(1)).toBe(true);
    expect(takesChildren(2)).toBe(false);
  });
});
