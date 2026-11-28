import { afterEach, describe, expect, test } from "bun:test";
import { colors } from "@teck/design-tokens";
import { Notice } from "@teck/ui/blocks/notice";
import { cleanup, render, screen } from "@testing-library/react";

afterEach(cleanup);

const AA_NORMAL_TEXT = 4.5;
const DESTRUCTIVE_TINT_ALPHA = 0.1;

type Rgb = { r: number; g: number; b: number };

function hslToRgb(value: string): Rgb {
  const match = /^hsl\(([\d.]+), ([\d.]+)%, ([\d.]+)%\)$/u.exec(value);
  if (match === null) {
    throw new Error(`Unsupported color: ${value}`);
  }

  const hue = Number(match[1]) / 360;
  const saturation = Number(match[2]) / 100;
  const lightness = Number(match[3]) / 100;
  if (saturation === 0) {
    return { r: lightness, g: lightness, b: lightness };
  }

  const high =
    lightness < 0.5
      ? lightness * (1 + saturation)
      : lightness + saturation - lightness * saturation;
  const low = 2 * lightness - high;
  const channel = (offset: number) => {
    let position = hue + offset;
    if (position < 0) position += 1;
    if (position > 1) position -= 1;
    if (position < 1 / 6) return low + (high - low) * 6 * position;
    if (position < 1 / 2) return high;
    if (position < 2 / 3) return low + (high - low) * (2 / 3 - position) * 6;
    return low;
  };

  return { r: channel(1 / 3), g: channel(0), b: channel(-1 / 3) };
}

function blend(foreground: Rgb, background: Rgb, alpha: number): Rgb {
  return {
    r: foreground.r * alpha + background.r * (1 - alpha),
    g: foreground.g * alpha + background.g * (1 - alpha),
    b: foreground.b * alpha + background.b * (1 - alpha),
  };
}

function relativeLuminance({ r, g, b }: Rgb): number {
  const linear = (value: number) =>
    value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  return 0.2126 * linear(r) + 0.7152 * linear(g) + 0.0722 * linear(b);
}

function contrastRatio(first: Rgb, second: Rgb): number {
  const firstLuminance = relativeLuminance(first);
  const secondLuminance = relativeLuminance(second);
  const lighter = Math.max(firstLuminance, secondLuminance);
  const darker = Math.min(firstLuminance, secondLuminance);
  return (lighter + 0.05) / (darker + 0.05);
}

describe("Notice contrast", () => {
  test("renders destructive text with the contrast-checked foreground token", () => {
    render(<Notice tone="error">Checkout recovery is unavailable in this browser.</Notice>);

    const notice = screen.getByRole("alert");
    expect(notice.textContent).toBe("Checkout recovery is unavailable in this browser.");
    expect(notice.className).toContain("text-foreground");
    expect(notice.className).toContain("bg-destructive/10");
  });

  test("keeps notice text at AA contrast in both palettes", () => {
    for (const scheme of ["light", "dark"] as const) {
      const palette = colors[scheme];
      const foreground = hslToRgb(palette.foreground);
      const background = hslToRgb(palette.background);
      const tint = blend(hslToRgb(palette.destructive), background, DESTRUCTIVE_TINT_ALPHA);

      // Web error notice on the destructive tint.
      expect(contrastRatio(foreground, tint)).toBeGreaterThanOrEqual(AA_NORMAL_TEXT);
      // Native error notice on the plain background and info notice on muted.
      expect(contrastRatio(foreground, background)).toBeGreaterThanOrEqual(AA_NORMAL_TEXT);
      expect(contrastRatio(foreground, hslToRgb(palette.muted))).toBeGreaterThanOrEqual(
        AA_NORMAL_TEXT,
      );
    }
  });
});
