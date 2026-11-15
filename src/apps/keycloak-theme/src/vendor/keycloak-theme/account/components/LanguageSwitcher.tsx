import { Languages } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { accountControlLabels, accountLanguageLinks } from "../../../../account-presentation";
import type { KcContext } from "../KcContext";

export function LanguageSwitcher({ kcContext }: { kcContext: KcContext }) {
    const labels = accountControlLabels(kcContext.locale?.currentLanguageTag);
    const languages = accountLanguageLinks(kcContext.locale, kcContext.realm.internationalizationEnabled);
    if (languages.length === 0) return null;

    return (
        <DropdownMenu>
            <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" className="h-9 w-9" aria-label={labels.languageSelect}>
                    <Languages className="h-4 w-4" />
                </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent side="top" align="center" className="max-h-[300px] overflow-y-auto">
                {languages.map((language) => (
                    <DropdownMenuItem key={language.languageTag} asChild>
                        <a href={language.url} lang={language.languageTag} aria-current={kcContext.locale?.currentLanguageTag === language.languageTag ? "true" : undefined}>
                            <span>{language.label}</span>
                            {kcContext.locale?.currentLanguageTag === language.languageTag && <span className="ml-auto">✓</span>}
                        </a>
                    </DropdownMenuItem>
                ))}
            </DropdownMenuContent>
        </DropdownMenu>
    );
}
