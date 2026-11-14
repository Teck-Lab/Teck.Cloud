import lint from "@commitlint/lint";
import load from "@commitlint/load";

const config = await load({ extends: ["@commitlint/config-conventional"] });

export async function validateTitle(title) {
  if (!title?.trim() || /[\r\n]/u.test(title)) {
    return {
      valid: false,
      errors: [{ message: "Provide one nonempty Conventional Commit title." }],
    };
  }
  return lint(title, config.rules, {
    parserOpts: config.parserPreset?.parserOpts,
    defaultIgnores: false,
  });
}

if (import.meta.main) {
  const result = await validateTitle(process.env.PR_TITLE);
  for (const error of result.errors) console.error(error.message);
  if (!result.valid) process.exitCode = 1;
}
