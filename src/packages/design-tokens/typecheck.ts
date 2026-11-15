import { colors, type colorTokens } from "@teck/design-tokens";

const primary: string = colors.light.primary;
const token: (typeof colorTokens)[number] = "primary";
// @ts-expect-error Unknown tokens must not be accepted.
const missing = colors.dark.doesNotExist;

void primary;
void token;
void missing;
