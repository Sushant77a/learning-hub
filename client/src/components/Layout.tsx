import { useEffect, useRef, useState } from "react";
import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useApp } from "../context/AppContext";
import { Icon } from "./icons";
import { Modal } from "./ui";
import { SearchOverlay } from "./SearchOverlay";
import { ResourcesDrawer } from "./ResourcesDrawer";

// Four primary areas. Everything else lives behind "More" so the daily path stays short.
const primaryNav = [
  { to: "/", label: "Today", icon: "today", end: true },
  { to: "/skills", label: "Learn", icon: "skills" },
  { to: "/plan", label: "Plan", icon: "plan" },
  { to: "/review", label: "Review", icon: "review" }
];

const moreNav = [
  { to: "/schedule", label: "Schedule", icon: "schedule" },
  { to: "/notes", label: "Notes", icon: "notes" },
  { to: "/progress", label: "Progress", icon: "progress" },
  { to: "/reflections", label: "Reflections", icon: "reflections" },
  { to: "/resources", label: "Resources", icon: "resource" },
  { to: "/settings", label: "Settings", icon: "settings" }
];

const secondaryRoutes = moreNav.map((item) => item.to);

export function Layout() {
  const {
    theme,
    toggleTheme,
    setSearchOpen,
    openResources,
    settings,
    settingsError
  } = useApp();
  const navigate = useNavigate();
  const location = useLocation();
  const [shortcutsOpen, setShortcutsOpen] = useState(false);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [moreOpen, setMoreOpen] = useState(secondaryRoutes.includes(location.pathname));
  const lastG = useRef<number>(0);

  useEffect(() => {
    setSidebarOpen(false);
    if (secondaryRoutes.includes(location.pathname)) setMoreOpen(true);
  }, [location.pathname]);

  useEffect(() => {
    const isTyping = (target: EventTarget | null) => {
      const element = target as HTMLElement | null;
      return Boolean(
        element &&
          (element.tagName === "INPUT" ||
            element.tagName === "TEXTAREA" ||
            element.tagName === "SELECT" ||
            element.isContentEditable)
      );
    };

    const onKey = (event: KeyboardEvent) => {
      if (isTyping(event.target)) return;
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setSearchOpen(true);
        return;
      }
      if (event.metaKey || event.ctrlKey || event.altKey) return;

      if (event.key === "/") {
        event.preventDefault();
        setSearchOpen(true);
        return;
      }
      if (event.key === "?") {
        event.preventDefault();
        setShortcutsOpen(true);
        return;
      }
      if (event.key.toLowerCase() === "d") {
        toggleTheme();
        return;
      }
      if (event.key.toLowerCase() === "r") {
        openResources();
        return;
      }
      if (event.key.toLowerCase() === "g") {
        lastG.current = Date.now();
        return;
      }
      if (Date.now() - lastG.current < 900) {
        const routes: Record<string, string> = {
          t: "/",
          s: "/skills",
          p: "/plan",
          c: "/schedule",
          v: "/review",
          n: "/notes",
          r: "/progress",
          f: "/reflections",
          ",": "/settings"
        };
        const route = routes[event.key.toLowerCase()];
        if (route) {
          event.preventDefault();
          navigate(route);
          lastG.current = 0;
        }
      }
    };

    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [navigate, openResources, setSearchOpen, toggleTheme]);

  return (
    <div className="app-shell">
      <a href="#main" className="skip-link">
        Skip to content
      </a>

      <aside className={`sidebar ${sidebarOpen ? "open" : ""}`}>
        <div className="brand">
          <span className="brand-mark">LH</span>
          <span className="brand-copy">
            <strong>Learning Hub</strong>
            <span>theory first</span>
          </span>
        </div>

        <nav className="nav" aria-label="Main">
          {primaryNav.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) => `nav-link ${isActive ? "active" : ""}`}
            >
              <Icon name={item.icon} size={18} />
              <span>{item.label}</span>
            </NavLink>
          ))}

          <button
            type="button"
            className="nav-link nav-button"
            aria-expanded={moreOpen}
            aria-controls="nav-more"
            onClick={() => setMoreOpen((open) => !open)}
          >
            <Icon name="chevronDown" size={18} className={moreOpen ? "nav-chevron open" : "nav-chevron"} />
            <span>More</span>
          </button>

          {moreOpen ? (
            <div className="nav-group" id="nav-more">
              {moreNav.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  className={({ isActive }) => `nav-link nav-link-secondary ${isActive ? "active" : ""}`}
                >
                  <Icon name={item.icon} size={16} />
                  <span>{item.label}</span>
                </NavLink>
              ))}
            </div>
          ) : null}
        </nav>

        <div className="sidebar-foot">
          <button className="nav-link nav-button" onClick={() => setShortcutsOpen(true)}>
            <Icon name="command" size={18} />
            <span>Shortcuts</span>
            <kbd>?</kbd>
          </button>
          <div className="sidebar-note">
            {settings?.ai.configured ? (
              <>
                <span className="status-dot on" /> <span>AI on demand · {settings.ai.provider}</span>
              </>
            ) : (
              <>
                <span className="status-dot" /> <span>AI off · works fully offline</span>
              </>
            )}
          </div>
        </div>
      </aside>

      <div className="main-wrap">
        <header className="topbar">
          <button className="icon-btn mobile-only" onClick={() => setSidebarOpen(true)} aria-label="Open navigation">
            <Icon name="menu" />
          </button>
          <button className="search-trigger" onClick={() => setSearchOpen(true)} aria-label="Search everything">
            <Icon name="search" size={16} />
            <span>Search everything</span>
            <kbd>{navigator.platform.toLowerCase().includes("mac") ? "⌘K" : "Ctrl K"}</kbd>
          </button>
          <div className="topbar-actions">
            <button className="icon-btn" onClick={() => openResources()} aria-label="Open resources drawer" title="Resources (R)">
              <Icon name="resource" />
            </button>
            <button className="icon-btn" onClick={toggleTheme} aria-label="Toggle dark mode" title="Toggle theme (D)">
              <Icon name={theme === "dark" ? "sun" : "moon"} />
            </button>
          </div>
        </header>

        {settingsError ? (
          <div className="global-error">
            The local server is not responding. Start it with <code>start.cmd</code>, then reload.
          </div>
        ) : null}

        <main id="main" className="main">
          <Outlet />
        </main>
      </div>

      <nav className="mobile-nav" aria-label="Mobile navigation">
        {primaryNav.map((item) => (
          <NavLink key={item.to} to={item.to} end={item.end} className={({ isActive }) => (isActive ? "active" : "")}>
            <Icon name={item.icon} size={19} />
            <span>{item.label}</span>
          </NavLink>
        ))}
        <button onClick={() => setSidebarOpen(true)} aria-label="More">
          <Icon name="menu" size={19} />
          <span>More</span>
        </button>
      </nav>

      <SearchOverlay />
      <ResourcesDrawer />

      <Modal open={shortcutsOpen} title="Keyboard shortcuts" onClose={() => setShortcutsOpen(false)}>
        <div className="shortcut-grid">
          {[
            ["Ctrl / ⌘ + K", "Search everything"],
            ["/", "Search everything"],
            ["g then t", "Today"],
            ["g then s", "Skills"],
            ["g then p", "Plan"],
            ["g then c", "Schedule"],
            ["g then v", "Review"],
            ["g then n", "Notes"],
            ["g then r", "Progress"],
            ["g then f", "Reflections"],
            ["d", "Toggle dark mode"],
            ["r", "Open resources"],
            ["?", "This list"],
            ["Esc", "Close a panel"]
          ].map(([key, description]) => (
            <div className="shortcut-row" key={key}>
              <kbd>{key}</kbd>
              <span>{description}</span>
            </div>
          ))}
        </div>
        <p className="muted small">Shortcuts are ignored while you are typing in a field.</p>
      </Modal>
    </div>
  );
}
