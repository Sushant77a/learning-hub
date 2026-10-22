import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api, errorMessage } from "../api";
import type { SearchResponse } from "../api/types";
import { useApp } from "../context/AppContext";
import { Icon } from "./icons";

const typeIcons: Record<string, string> = {
  lesson: "book",
  note: "notes",
  question: "review",
  mistake: "warning",
  resource: "resource",
  reflection: "reflections",
  skill: "skills"
};

export function SearchOverlay() {
  const { searchOpen, setSearchOpen } = useApp();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<SearchResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [active, setActive] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();

  useEffect(() => {
    if (!searchOpen) return;
    setQuery("");
    setResults(null);
    setActive(0);
    window.setTimeout(() => inputRef.current?.focus(), 30);
  }, [searchOpen]);

  useEffect(() => {
    if (!searchOpen) return;
    const term = query.trim();
    if (term.length < 2) {
      setResults(null);
      setError(null);
      return;
    }
    const timer = window.setTimeout(() => {
      setLoading(true);
      api
        .get<SearchResponse>(`/search?q=${encodeURIComponent(term)}`)
        .then((response) => {
          setResults(response);
          setActive(0);
          setError(null);
        })
        .catch((err) => setError(errorMessage(err)))
        .finally(() => setLoading(false));
    }, 180);
    return () => window.clearTimeout(timer);
  }, [query, searchOpen]);

  const flat = useMemo(() => results?.groups.flatMap((group) => group.results) ?? [], [results]);

  useEffect(() => {
    if (!searchOpen) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setSearchOpen(false);
      } else if (event.key === "ArrowDown") {
        event.preventDefault();
        setActive((value) => Math.min(flat.length - 1, value + 1));
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        setActive((value) => Math.max(0, value - 1));
      } else if (event.key === "Enter" && flat[active]) {
        event.preventDefault();
        navigate(flat[active].route);
        setSearchOpen(false);
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [searchOpen, flat, active, navigate, setSearchOpen]);

  if (!searchOpen) return null;

  let index = -1;
  return (
    <div className="search-overlay" onMouseDown={(event) => event.target === event.currentTarget && setSearchOpen(false)}>
      <div className="search-panel" role="dialog" aria-modal="true" aria-label="Search everything">
        <div className="search-panel-input">
          <Icon name="search" size={19} />
          <input
            ref={inputRef}
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Search lessons, notes, mistakes, resources…"
            aria-label="Search everything"
            autoComplete="off"
            spellCheck={false}
          />
          <kbd>esc</kbd>
        </div>

        <div className="search-panel-body">
          {query.trim().length < 2 ? (
            <div className="search-hint">
              <p>Search the whole hub. A few things to try:</p>
              <div className="search-suggestions">
                {["Big-O", "null", "ref", "LINQ", "async", "arrays"].map((suggestion) => (
                  <button key={suggestion} onClick={() => setQuery(suggestion)}>
                    {suggestion}
                  </button>
                ))}
              </div>
            </div>
          ) : loading && !results ? (
            <div className="loading"><span className="spinner" /> Searching…</div>
          ) : error ? (
            <div className="search-empty">{error}</div>
          ) : results && results.total === 0 ? (
            <div className="search-empty">
              <Icon name="search" size={20} />
              <p>No results for “{results.query}”.</p>
            </div>
          ) : results ? (
            results.groups.map((group) => (
              <section key={group.type} className="search-group">
                <h3>{group.label}</h3>
                {group.results.map((hit) => {
                  const itemIndex = ++index;
                  return (
                    <button
                      key={`${hit.type}-${hit.route}-${itemIndex}`}
                      className={`search-result ${itemIndex === active ? "active" : ""}`}
                      onMouseEnter={() => setActive(itemIndex)}
                      onClick={() => {
                        navigate(hit.route);
                        setSearchOpen(false);
                      }}
                    >
                      <span className="search-result-icon">
                        <Icon name={typeIcons[hit.type] ?? "book"} size={16} />
                      </span>
                      <span className="search-result-copy">
                        <span className="search-result-title">{hit.title}</span>
                        {hit.snippet ? <span className="search-result-snippet">{hit.snippet}</span> : null}
                      </span>
                      <span className="search-result-meta">{hit.meta}</span>
                    </button>
                  );
                })}
              </section>
            ))
          ) : null}
        </div>

        <footer className="search-panel-foot">
          <span><kbd>↑</kbd><kbd>↓</kbd> navigate</span>
          <span><kbd>enter</kbd> open</span>
          <span><kbd>esc</kbd> close</span>
        </footer>
      </div>
    </div>
  );
}
