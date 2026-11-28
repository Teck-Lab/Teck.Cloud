import { colors } from "@teck/design-tokens";
import { useState } from "react";
import {
  Pressable,
  type PressableProps,
  type StyleProp,
  StyleSheet,
  Text,
  useColorScheme,
  type ViewStyle,
} from "react-native";

type Palette = (typeof colors)["light"];

export interface ButtonProps extends Omit<PressableProps, "children" | "style"> {
  label: string;
  variant?: "primary" | "secondary" | "danger";
  style?: StyleProp<ViewStyle>;
}

function buttonColors(variant: NonNullable<ButtonProps["variant"]>, palette: Palette) {
  if (variant === "primary") {
    return {
      background: palette.primary,
      foreground: palette["primary-foreground"],
      restingBorder: palette.primary,
      focusBorder: palette["primary-foreground"],
    };
  }
  if (variant === "danger") {
    return {
      background: palette.destructive,
      foreground: palette["destructive-foreground"],
      restingBorder: palette.destructive,
      focusBorder: palette["destructive-foreground"],
    };
  }
  return {
    background: palette.secondary,
    foreground: palette["secondary-foreground"],
    restingBorder: palette.border,
    focusBorder: palette.ring,
  };
}

export function Button({
  label,
  variant = "primary",
  disabled,
  onBlur,
  onFocus,
  style,
  ...props
}: ButtonProps) {
  const [focused, setFocused] = useState(false);
  const palette = colors[useColorScheme() === "dark" ? "dark" : "light"];
  const paletteForVariant = buttonColors(variant, palette);
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled: Boolean(disabled) }}
      disabled={disabled}
      focusable
      onBlur={(event) => {
        setFocused(false);
        onBlur?.(event);
      }}
      onFocus={(event) => {
        setFocused(true);
        onFocus?.(event);
      }}
      style={(state) => [
        styles.base,
        {
          backgroundColor: paletteForVariant.background,
          borderColor: focused ? paletteForVariant.focusBorder : paletteForVariant.restingBorder,
        },
        disabled ? styles.disabled : null,
        state.pressed ? styles.pressed : null,
        style,
      ]}
      {...props}
    >
      <Text style={[styles.label, { color: paletteForVariant.foreground }]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: {
    alignItems: "center",
    borderRadius: 8,
    borderWidth: 2,
    justifyContent: "center",
    minHeight: 40,
    paddingHorizontal: 16,
    paddingVertical: 8,
  },
  label: {
    fontSize: 14,
    fontWeight: "600",
  },
  disabled: {
    opacity: 0.5,
  },
  pressed: {
    opacity: 0.85,
  },
});
