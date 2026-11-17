export function isPublicationEnabled(value) {
  return value === "true";
}
export function requirePublicationEnabled(value) {
  if (!isPublicationEnabled(value)) {
    throw new Error(
      "Image publication is disabled; set ENABLE_IMAGE_PUBLICATION=true to activate it.",
    );
  }
  return true;
}

if (process.argv[1]?.endsWith("release-activation.mjs")) {
  try {
    requirePublicationEnabled(process.env.ENABLE_IMAGE_PUBLICATION);
    console.log("Image publication activation is enabled.");
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
  }
}
