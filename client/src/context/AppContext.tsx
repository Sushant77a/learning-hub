import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode
} from "react";
import { api, errorMessage, todayIso } from "../api";
import type { Settings } from "../api/types";

export interface ResourceContext {
  lessonId?: string;
  lessonTitle?: string;
  skillId?: string;
  skillName?: string;
}

type ToastKind = "info" | "success" | "error";

interface Toast {
  id: number;
  message: string;
  kind: ToastKind;
}

type ThemePreference = "light" | "dark" | "system";

interface AppContextValue {
  settings: Settings | null;
  settingsLoading: boolean;
  settingsError: string | null;
  refreshSettings: () => void;
  version: number;
  bump: () => void;
  theme: "light" | "dark";
  themePreference: ThemePreference;
  setThemePreference: (theme: ThemePreference) => void;
  toggleTheme: () => void;
  notify: (message: string, kind?: ToastKind) => void;
  searchOpen: boolean;
  setSearchOpen: (open: boolean) => void;
  resourcesOpen: boolean;
  resourceContext: ResourceContext;
  setResourceContext: (context: ResourceContext) => void;
  openResources: (context?: ResourceContext) => void;
  closeResources: () => void;
  today: string;
}

const AppContext = createContext<AppContextValue | null>(null);

function resolveTheme(preference: ThemePreference): "light" | "dark" {
  if (preference === "light" || preference === "dark") return preference;
  return window.matchMedia?.("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function applyTheme(theme: "light" | "dark") {
  document.documentElement.dataset.theme = theme;
}

export function AppProvider({ children }: { children: ReactNode }) {
  const [settings, setSettings] = useState<Settings | null>(null);
  const [settingsLoading, setSettingsLoading] = useState(true);
  const [settingsError, setSettingsError] = useState<string | null>(null);
  const [version, setVersion] = useState(0);
  const [themePreference, setThemePreferenceState] = useState<ThemePreference>(() => {
    const stored = localStorage.getItem("lh-theme");
    return stored === "light" || stored === "dark" ? stored : "system";
  });
  const [theme, setTheme] = useState<"light" | "dark">(() => resolveTheme(themePreference));
  const [toasts, setToasts] = useState<Toast[]>([]);
  const [searchOpen, setSearchOpen] = useState(false);
  const [resourcesOpen, setResourcesOpen] = useState(false);
  const [resourceContext, setResourceContext] = useState<ResourceContext>({});
  const toastId = useRef(0);

  const bump = useCallback(() => setVersion((v) => v + 1), []);

  const refreshSettings = useCallback(() => {
    setSettingsLoading(true);
    api
      .get<Settings>("/settings")
      .then((result) => {
        setSettings(result);
        setSettingsError(null);
        if (!localStorage.getItem("lh-theme")) {
          const preference = result.theme === "dark" ? "dark" : result.theme === "light" ? "light" : "system";
          setThemePreferenceState(preference);
        }
      })
      .catch((error) => setSettingsError(errorMessage(error)))
      .finally(() => setSettingsLoading(false));
  }, []);

  useEffect(() => {
    refreshSettings();
  }, [refreshSettings, version]);

  useEffect(() => {
    applyTheme(theme);
  }, [theme]);

  useEffect(() => {
    if (themePreference !== "system") return;
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const listener = () => setTheme(media.matches ? "dark" : "light");
    media.addEventListener("change", listener);
    return () => media.removeEventListener("change", listener);
  }, [themePreference]);

  const notify = useCallback((message: string, kind: ToastKind = "info") => {
    const id = ++toastId.current;
    setToasts((current) => [...current, { id, message, kind }]);
    window.setTimeout(() => {
      setToasts((current) => current.filter((toast) => toast.id !== id));
    }, kind === "error" ? 7000 : 4200);
  }, []);

  const setThemePreference = useCallback(
    (preference: ThemePreference) => {
      setThemePreferenceState(preference);
      setTheme(resolveTheme(preference));
      if (preference === "system") localStorage.removeItem("lh-theme");
      else localStorage.setItem("lh-theme", preference);
      void api.patch("/settings", { theme: preference }).then(() => bump()).catch(() => undefined);
    },
    [bump]
  );

  const toggleTheme = useCallback(() => {
    const next = theme === "dark" ? "light" : "dark";
    setThemePreference(next);
  }, [theme, setThemePreference]);

  const openResources = useCallback((context?: ResourceContext) => {
    if (context) setResourceContext(context);
    setResourcesOpen(true);
  }, []);

  const value = useMemo<AppContextValue>(
    () => ({
      settings,
      settingsLoading,
      settingsError,
      refreshSettings,
      version,
      bump,
      theme,
      themePreference,
      setThemePreference,
      toggleTheme,
      notify,
      searchOpen,
      setSearchOpen,
      resourcesOpen,
      resourceContext,
      setResourceContext,
      openResources,
      closeResources: () => setResourcesOpen(false),
      today: todayIso()
    }),
    [
      settings,
      settingsLoading,
      settingsError,
      refreshSettings,
      version,
      bump,
      theme,
      themePreference,
      setThemePreference,
      toggleTheme,
      notify,
      searchOpen,
      resourcesOpen,
      resourceContext,
      setResourceContext,
      openResources
    ]
  );

  return (
    <AppContext.Provider value={value}>
      {children}
      <div className="toast-stack" aria-live="polite">
        {toasts.map((toast) => (
          <div key={toast.id} className={`toast toast-${toast.kind}`}>
            {toast.message}
          </div>
        ))}
      </div>
    </AppContext.Provider>
  );
}

export function useApp() {
  const context = useContext(AppContext);
  if (!context) throw new Error("useApp must be used inside AppProvider");
  return context;
}
