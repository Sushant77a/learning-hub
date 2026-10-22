import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api, errorMessage, formatDate } from "../api";
import type { Resource } from "../api/types";
import { useApp } from "../context/AppContext";
import { Icon } from "./icons";
import { Badge, Button, ErrorNote, Field, Select, TextInput } from "./ui";

const typeOptions = [
  { value: "article", label: "Article" },
  { value: "video", label: "Video" },
  { value: "docs", label: "Docs" },
  { value: "other", label: "Other" }
];

export function ResourcesDrawer() {
  const { resourcesOpen, closeResources, resourceContext, notify, version } = useApp();
  const [resources, setResources] = useState<Resource[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const [type, setType] = useState("");
  const [adding, setAdding] = useState(false);
  const [form, setForm] = useState({ title: "", url: "", type: "article", notes: "" });
  const [saving, setSaving] = useState(false);

  const load = () => {
    if (!resourcesOpen) return;
    setLoading(true);
    const params = new URLSearchParams();
    if (resourceContext.skillId) params.set("skillId", resourceContext.skillId);
    if (query.trim()) params.set("q", query.trim());
    if (type) params.set("type", type);
    api
      .get<Resource[]>(`/resources?${params.toString()}`)
      .then((result) => {
        setResources(result);
        setError(null);
      })
      .catch((err) => setError(errorMessage(err)))
      .finally(() => setLoading(false));
  };

  useEffect(load, [resourcesOpen, resourceContext.skillId, query, type, version]);

  useEffect(() => {
    if (!resourcesOpen) return;
    setAdding(false);
    setForm({ title: "", url: "", type: "article", notes: "" });
  }, [resourcesOpen, resourceContext.lessonId]);

  useEffect(() => {
    if (!resourcesOpen) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") closeResources();
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [resourcesOpen, closeResources]);

  const addResource = async () => {
    if (!form.title.trim() || !form.url.trim()) {
      notify("A title and URL are required.", "error");
      return;
    }
    setSaving(true);
    try {
      await api.post("/resources", {
        title: form.title,
        url: form.url,
        type: form.type,
        notes: form.notes,
        skillId: resourceContext.skillId ?? null,
        lessonId: resourceContext.lessonId ?? null
      });
      notify("Resource saved.", "success");
      setForm({ title: "", url: "", type: "article", notes: "" });
      setAdding(false);
      load();
    } catch (err) {
      notify(errorMessage(err), "error");
    } finally {
      setSaving(false);
    }
  };

  const setStatus = async (resource: Resource, status: string) => {
    try {
      await api.patch(`/resources/${resource.id}`, { status });
      setResources((current) => current.map((item) => (item.id === resource.id ? { ...item, status: status as Resource["status"] } : item)));
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  const lessonResources = resources.filter((item) => item.lessonId && item.lessonId === resourceContext.lessonId);
  const skillResources = resources.filter(
    (item) => item.skillId === resourceContext.skillId && item.lessonId !== resourceContext.lessonId
  );
  const otherResources = resources.filter(
    (item) => item.skillId !== resourceContext.skillId && item.lessonId !== resourceContext.lessonId
  );

  const renderResource = (resource: Resource) => {
    const done = resource.status === "read" || resource.status === "watched";
    return (
      <div className={`drawer-resource ${done ? "is-done" : ""}`} key={resource.id}>
        <div className="drawer-resource-main">
          <a href={resource.url} target="_blank" rel="noreferrer noopener" className="resource-title">
            {resource.title}
            <Icon name="external" size={13} />
          </a>
          <div className="resource-meta">
            <Badge tone="muted">{resource.type}</Badge>
            {resource.lessonTitle ? <span className="truncate">{resource.lessonTitle}</span> : null}
            {!resource.lessonTitle && resource.skillName ? <span>{resource.skillName}</span> : null}
            <span>{formatDate(resource.updatedAt)}</span>
          </div>
          {resource.notes ? <p className="resource-notes">{resource.notes}</p> : null}
        </div>
        <div className="drawer-resource-actions">
          <button
            type="button"
            className="icon-btn small"
            title={done ? "Mark unread" : "Mark read / watched"}
            aria-label={done ? "Mark unread" : "Mark read or watched"}
            onClick={() => setStatus(resource, done ? "unread" : resource.type === "video" ? "watched" : "read")}
          >
            <Icon name={done ? "check" : "clock"} size={15} />
          </button>
        </div>
      </div>
    );
  };

  return (
    <>
      <div className={`drawer-backdrop ${resourcesOpen ? "open" : ""}`} onClick={closeResources} aria-hidden="true" />
      <aside className={`drawer ${resourcesOpen ? "open" : ""}`} aria-hidden={!resourcesOpen} aria-label="Resources">
        <header className="drawer-head">
          <div>
            <span className="eyebrow">Resources</span>
            <h2>{resourceContext.lessonTitle ?? resourceContext.skillName ?? "All bookmarks"}</h2>
            {resourceContext.lessonTitle && resourceContext.skillName ? (
              <p className="muted small">{resourceContext.skillName}</p>
            ) : null}
          </div>
          <button className="icon-btn" onClick={closeResources} aria-label="Close resources">
            <Icon name="close" />
          </button>
        </header>

        <div className="drawer-tools">
          <div className="search-input">
            <Icon name="search" size={15} />
            <input
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Search resources…"
              aria-label="Search resources"
            />
          </div>
          <select className="input compact-select" value={type} onChange={(event) => setType(event.target.value)} aria-label="Filter by type">
            <option value="">All types</option>
            {typeOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </div>

        <div className="drawer-body">
          {error ? <ErrorNote>{error}</ErrorNote> : null}
          {loading && resources.length === 0 ? <div className="loading"><span className="spinner" /> Loading…</div> : null}

          {adding ? (
            <div className="drawer-add">
              <Field label="Title" htmlFor="res-title">
                <TextInput
                  id="res-title"
                  value={form.title}
                  onChange={(event) => setForm({ ...form, title: event.target.value })}
                  placeholder="e.g. C# async deep dive"
                />
              </Field>
              <Field label="URL" htmlFor="res-url">
                <TextInput
                  id="res-url"
                  value={form.url}
                  onChange={(event) => setForm({ ...form, url: event.target.value })}
                  placeholder="https://…"
                />
              </Field>
              <div className="form-row">
                <Field label="Type" htmlFor="res-type">
                  <Select id="res-type" value={form.type} onChange={(event) => setForm({ ...form, type: event.target.value })}>
                    {typeOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
              <Field label="Notes" htmlFor="res-notes" hint="Optional — why this is worth keeping.">
                <TextInput
                  id="res-notes"
                  value={form.notes}
                  onChange={(event) => setForm({ ...form, notes: event.target.value })}
                  placeholder="A short note"
                />
              </Field>
              <div className="button-row">
                <Button variant="primary" size="sm" onClick={addResource} loading={saving}>
                  Save resource
                </Button>
                <Button variant="ghost" size="sm" onClick={() => setAdding(false)}>
                  Cancel
                </Button>
              </div>
            </div>
          ) : (
            <Button variant="secondary" size="sm" icon="plus" onClick={() => setAdding(true)}>
              Add resource
            </Button>
          )}

          {lessonResources.length > 0 ? (
            <section className="drawer-section">
              <h3>For this lesson</h3>
              {lessonResources.map(renderResource)}
            </section>
          ) : null}
          {skillResources.length > 0 ? (
            <section className="drawer-section">
              <h3>For this skill</h3>
              {skillResources.map(renderResource)}
            </section>
          ) : null}
          {otherResources.length > 0 ? (
            <section className="drawer-section">
              <h3>{resourceContext.skillId ? "Elsewhere" : "All resources"}</h3>
              {otherResources.map(renderResource)}
            </section>
          ) : null}

          {!loading && resources.length === 0 && !error ? (
            <div className="drawer-empty">
              <Icon name="book" size={20} />
              <p>No resources here yet. Keep the good links close.</p>
            </div>
          ) : null}
        </div>

        <footer className="drawer-foot">
          <Link to="/resources" onClick={closeResources} className="text-link">
            Browse all resources <Icon name="arrowRight" size={14} />
          </Link>
        </footer>
      </aside>
    </>
  );
}
