import { colors } from "@teck/design-tokens";
import type { ReactNode } from "react";
import { StyleSheet, Text, useColorScheme, View } from "react-native";
import { Button } from "../components/Button";

export interface NoticeProps {
  children: ReactNode;
  tone?: "info" | "error";
  action?: { label: string; onPress: () => void };
}

export function Notice({ children, tone = "info", action }: NoticeProps) {
  const palette = colors[useColorScheme() === "dark" ? "dark" : "light"];
  const isError = tone === "error";
  const backgroundColor = isError ? palette.background : palette.muted;
  const borderColor = isError ? palette.destructive : palette.border;
  const textColor = isError ? palette.destructive : palette.foreground;
  return (
    <View
      accessibilityLiveRegion={isError ? "assertive" : "polite"}
      accessibilityRole={isError ? "alert" : "text"}
      style={[styles.container, { backgroundColor, borderColor }]}
    >
      <Text style={[styles.message, { color: textColor }]}>{children}</Text>
      {action ? (
        <Button
          label={action.label}
          onPress={action.onPress}
          style={styles.action}
          variant="secondary"
        />
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    borderRadius: 8,
    borderWidth: 1,
    gap: 8,
    padding: 12,
  },
  message: {
    fontSize: 14,
  },
  action: {
    alignSelf: "flex-start",
  },
});
