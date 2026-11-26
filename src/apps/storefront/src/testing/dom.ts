import { afterEach, mock } from "bun:test";
import { JSDOM } from "jsdom";

// Next aliases the server-only marker for its server bundle; Bun tests get the same empty module.
mock.module("server-only", () => ({}));

const dom = new JSDOM('<!doctype html><html lang="en"><body></body></html>', {
  url: "http://localhost:3000/",
});

const window = dom.window as unknown as Record<string, unknown>;

const browserGlobals = [
  "window",
  "document",
  "navigator",
  "location",
  "history",
  "localStorage",
  "sessionStorage",
  "customElements",
  "CSS",
  "CSSStyleSheet",
  "CSSStyleDeclaration",
  "Node",
  "NodeList",
  "HTMLCollection",
  "Element",
  "HTMLElement",
  "HTMLAnchorElement",
  "HTMLBodyElement",
  "HTMLButtonElement",
  "HTMLCanvasElement",
  "HTMLDivElement",
  "HTMLFormElement",
  "HTMLHeadElement",
  "HTMLHeadingElement",
  "HTMLHtmlElement",
  "HTMLImageElement",
  "HTMLInputElement",
  "HTMLLabelElement",
  "HTMLLIElement",
  "HTMLMetaElement",
  "HTMLOListElement",
  "HTMLParagraphElement",
  "HTMLSelectElement",
  "HTMLSpanElement",
  "HTMLTableElement",
  "HTMLTableRowElement",
  "HTMLTableCellElement",
  "HTMLTemplateElement",
  "HTMLTextAreaElement",
  "HTMLUListElement",
  "SVGElement",
  "SVGSVGElement",
  "Text",
  "Comment",
  "Document",
  "DocumentFragment",
  "ShadowRoot",
  "Range",
  "Selection",
  "DOMParser",
  "XMLSerializer",
  "DOMRect",
  "DOMRectReadOnly",
  "Event",
  "CustomEvent",
  "MouseEvent",
  "KeyboardEvent",
  "PointerEvent",
  "FocusEvent",
  "InputEvent",
  "CompositionEvent",
  "DragEvent",
  "SubmitEvent",
  "TouchEvent",
  "UIEvent",
  "WheelEvent",
  "MutationObserver",
  "ResizeObserver",
  "IntersectionObserver",
  "getComputedStyle",
  "matchMedia",
  "requestAnimationFrame",
  "cancelAnimationFrame",
  "requestIdleCallback",
  "cancelIdleCallback",
  "scrollTo",
  "scrollBy",
] as const;

for (const key of browserGlobals) {
  const value = window[key];
  if (value === undefined) {
    continue;
  }
  Object.defineProperty(globalThis, key, {
    configurable: true,
    writable: true,
    value,
  });
}

(globalThis as unknown as Record<string, unknown>).IS_REACT_ACT_ENVIRONMENT = true;

if (!("ResizeObserver" in globalThis)) {
  class TestResizeObserver {
    observe() {
      return this;
    }
    unobserve() {
      return this;
    }
    disconnect() {
      return this;
    }
  }
  Object.defineProperty(globalThis, "ResizeObserver", {
    configurable: true,
    writable: true,
    value: TestResizeObserver,
  });
  window.ResizeObserver = TestResizeObserver;
}

if (!("IntersectionObserver" in globalThis)) {
  class TestIntersectionObserver {
    observe() {
      return this;
    }
    unobserve() {
      return this;
    }
    disconnect() {
      return this;
    }
    takeRecords() {
      return [];
    }
  }
  Object.defineProperty(globalThis, "IntersectionObserver", {
    configurable: true,
    writable: true,
    value: TestIntersectionObserver,
  });
  window.IntersectionObserver = TestIntersectionObserver;
}

if (typeof Element !== "undefined" && !Element.prototype.scrollIntoView) {
  Element.prototype.scrollIntoView = () => {};
}

// Static import would evaluate react-dom before the jsdom globals below exist.
const { cleanup } = await import("@testing-library/react");

afterEach(() => {
  cleanup();
});
