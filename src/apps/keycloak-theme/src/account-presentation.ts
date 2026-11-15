import type {
  AccountProfile,
  UserProfileAttributeMetadata,
} from "./vendor/keycloak-theme/account/utils/accountProfile";

type AccountLanguage = { languageTag: string; label: string; url: string };
type AccountLocale = { supported: AccountLanguage[] };

const standardFields = new Set(["username", "email", "firstName", "lastName", "locale"]);
const internalFields = new Set(["is_temporary_admin"]);

export function accountLanguageLinks(locale?: AccountLocale, enabled = true): AccountLanguage[] {
  return enabled && locale && locale.supported.length > 1 ? locale.supported : [];
}

export function accountProfileFields(
  profile: AccountProfile,
): Array<UserProfileAttributeMetadata & { value: string[]; displayName: string }> {
  const metadata = profile.userProfileMetadata?.attributes ?? [];
  const values = profile.attributes ?? {};
  return metadata
    .filter(
      (attribute) =>
        Boolean(attribute.name) &&
        !standardFields.has(attribute.name) &&
        !internalFields.has(attribute.name),
    )
    .map((attribute) => ({
      ...attribute,
      value: values[attribute.name] ?? [],
      displayName:
        attribute.displayName && !/^\$\{.*\}$/.test(attribute.displayName)
          ? attribute.displayName
          : attribute.name,
    }));
}

type AccountControlLabels = {
  themeToggle: string;
  themeLight: string;
  themeDark: string;
  themeSystem: string;
  languageSelect: string;
};

const englishLabels: AccountControlLabels = {
  themeToggle: "Change appearance",
  themeLight: "Light",
  themeDark: "Dark",
  themeSystem: "System",
  languageSelect: "Language",
};
const labels: Record<string, AccountControlLabels> = {
  en: englishLabels,
  da: {
    themeToggle: "Skift udseende",
    themeLight: "Lys",
    themeDark: "Mørk",
    themeSystem: "System",
    languageSelect: "Sprog",
  },
  tr: {
    themeToggle: "Görünümü değiştir",
    themeLight: "Aydınlık",
    themeDark: "Koyu",
    themeSystem: "Sistem",
    languageSelect: "Dil",
  },
};

export function accountControlLabels(languageTag?: string): AccountControlLabels {
  return labels[languageTag ?? "en"] ?? labels[languageTag?.split("-")[0] ?? "en"] ?? englishLabels;
}
