import { useCallback, useEffect, useState } from "react";
import { accountControlLabels } from "../../../../account-presentation";
import { Monitor, Moon, Sun } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import type { KcContext } from "../KcContext";

type Theme = "light" | "dark" | "system";

export function ThemeSwitcher({ kcContext }: { kcContext: KcContext }) {
    const labels = accountControlLabels(kcContext.locale?.currentLanguageTag);
    const [theme, setTheme] = useState<Theme>("system");
    const [mounted, setMounted] = useState(false);

    const updateTheme = useCallback((newTheme: Theme, isDark?: boolean) => {
        const root = document.documentElement;
        if (newTheme === "system") {
            root.classList.toggle("dark", isDark ?? window.matchMedia("(prefers-color-scheme: dark)").matches);
        } else {
            root.classList.toggle("dark", newTheme === "dark");
        }
    }, []);

    useEffect(() => {
        setMounted(true);
        const stored = localStorage.getItem("theme");
        const selected: Theme = stored === "light" || stored === "dark" ? stored : "system";
        setTheme(selected);
        updateTheme(selected);
    }, [updateTheme]);

    useEffect(() => {
        if (!mounted || theme !== "system") return;
        const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
        const handleChange = (event: MediaQueryListEvent) => updateTheme("system", event.matches);
        mediaQuery.addEventListener("change", handleChange);
        return () => mediaQuery.removeEventListener("change", handleChange);
    }, [theme, mounted, updateTheme]);

    const handleThemeChange = (selected: Theme) => {
        setTheme(selected);
        localStorage.setItem("theme", selected);
        updateTheme(selected);
    };

    if (!mounted) {
        return <Button variant="ghost" size="icon" className="h-9 w-9" aria-label={labels.themeToggle}><Sun className="h-4 w-4" /></Button>;
    }

    return (
        <DropdownMenu>
            <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" className="h-9 w-9" aria-label={labels.themeToggle}>
                    {theme === "light" ? <Sun className="h-4 w-4" /> : theme === "dark" ? <Moon className="h-4 w-4" /> : <Monitor className="h-4 w-4" />}
                </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent side="top" align="center">
                <DropdownMenuItem onClick={() => handleThemeChange("light")}>
                    <Sun className="mr-2 h-4 w-4" />
                    <span>{labels.themeLight}</span>
                    {theme === "light" && <span className="ml-auto">✓</span>}
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => handleThemeChange("dark")}>
                    <Moon className="mr-2 h-4 w-4" />
                    <span>{labels.themeDark}</span>
                    {theme === "dark" && <span className="ml-auto">✓</span>}
                </DropdownMenuItem>
                <DropdownMenuItem onClick={() => handleThemeChange("system")}>
                    <Monitor className="mr-2 h-4 w-4" />
                    <span>{labels.themeSystem}</span>
                    {theme === "system" && <span className="ml-auto">✓</span>}
                </DropdownMenuItem>
            </DropdownMenuContent>
        </DropdownMenu>
    );
}
