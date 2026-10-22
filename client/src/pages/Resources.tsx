import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { api, errorMessage, formatDate } from "../api";
import type { Resource, SkillCard } from "../api/types";
import { useApp } from "../context/AppContext";
import { useApi } from "../hooks/useApi";
import { Icon } from "../components/icons";
import {
  Badge,
  Button,
  Card,
  EmptyState,
  ErrorNote,
  Field,
  Loading,
  Modal,
  Select,
  TextArea,
  TextInput
} from "../components/ui";

const typeOptions = ["article", "video", "docs", "other"];
const statusOptions = ["unread", "reading", "read", "watched"];

export function ResourcesPage() {
  const [params] = useSearchParams();
  const { notify } = useApp();
  const [query, setQuery] = useState("");
  const [type, setType] = useState("");
  const [status, setStatus] = useState("");
  const [skill, setSkill] = useState(params.get("skill") ?? "");
  const { data: resources, loading, error, reload } = useApi<Resource[]>(
    `/resources?${new URLSearchParams({
      ...(query ? { q: query } : {}),
      ...(type ? { type } : {}),
      ...(status ? { status } : {}),
      ...(skill ? { skillId: skill } : {})
    })}`
  );
  const { data: skills } = useApi<SkillCard[]>("/skills");
  const [addOpen, setAddOpen] = useState(false);
  const [form, setForm] = useState({ title: "", url: "", type: "article", notes: "", skillId: "" });
  const [saving, setSaving] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<Resource | null>(null);
  const highlight = Number(params.get("resource") ?? 0);

  useEffect(() => {
    if (!highlight || !resources) return;
    const element = document.getElementById(`resource-${highlight}`);
    element?.scrollIntoView({ behavior: "smooth", block: "center" });
  }, [highlight, resources]);

  const add = async () => {
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
        skillId: form.skillId || null,
        lessonId: null
      });
      notify("Resource saved.", "success");
      setAddOpen(false);
      setForm({ title: "", url: "", type: "article", notes: "", skillId: "" });
      reload();
    } catch (err) {
      notify(errorMessage(err), "error");
    } finally {
      setSaving(false);
    }
  };

  const updateStatus = async (resource: Resource, next: string) => {
    try {
      await api.patch(`/resources/${resource.id}`, { status: next });
      reload();
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  const remove = async () => {
    if (!deleteTarget) return;
    try {
      await api.del(`/resources/${deleteTarget.id}`);
      notify("Resource removed.", "success");
      setDeleteTarget(null);
      reload();
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  if (loading && !resources) return <div className="page"><Loading label="Opening resources…" /></div>;
  if (error) return <div className="page"><ErrorNote>{error}</ErrorNote></div>;

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <span className="eyebrow">Bookmarks</span>
          <h1>Resources</h1>
          <p className="page-subtitle">Links, videos, and docs attached to skills and lessons. Keep only what earns its place.</p>
        </div>
        <Button variant="primary" icon="plus" onClick={() => setAddOpen(true)}>
          Add resource
        </Button>
      </header>

      <div className="resource-filters">
        <div className="search-input">
          <Icon name="search" size={15} />
          <input
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Search title, URL, or notes…"
            aria-label="Search resources"
          />
        </div>
        <Select value={type} onChange={(event) => setType(event.target.value)} aria-label="Filter by type">
          <option value="">All types</option>
          {typeOptions.map((option) => <option key={option} value={option}>{option}</option>)}
        </Select>
        <Select value={status} onChange={(event) => setStatus(event.target.value)} aria-label="Filter by status">
          <option value="">Any status</option>
          {statusOptions.map((option) => <option key={option} value={option}>{option}</option>)}
        </Select>
        <Select value={skill} onChange={(event) => setSkill(event.target.value)} aria-label="Filter by skill">
          <option value="">All skills</option>
          {(skills ?? []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
        </Select>
      </div>

      {!resources || resources.length === 0 ? (
        <EmptyState
          title="No resources match"
          icon="resource"
          action={<Button onClick={() => setAddOpen(true)}>Add the first one</Button>}
        >
          The resources drawer is always one click away. This page is the full library.
        </EmptyState>
      ) : (
        <div className="resource-grid">
          {resources.map((resource) => {
            const done = resource.status === "read" || resource.status === "watched";
            return (
              <Card
                key={resource.id}
                id={`resource-${resource.id}`}
                className={`resource-card ${done ? "is-done" : ""} ${resource.id === highlight ? "highlight" : ""}`}
              >
                <div className="resource-card-head">
                  <Badge tone="muted">{resource.type}</Badge>
                  <Select
                    className="status-select"
                    value={resource.status}
                    onChange={(event) => updateStatus(resource, event.target.value)}
                    aria-label={`Status for ${resource.title}`}
                  >
                    {statusOptions.map((option) => <option key={option} value={option}>{option}</option>)}
                  </Select>
                </div>
                <a href={resource.url} target="_blank" rel="noreferrer noopener" className="resource-card-title">
                  {resource.title}
                  <Icon name="external" size={14} />
                </a>
                <div className="resource-card-context">
                  {resource.skillName ? <span>{resource.skillName}</span> : <span>Unattached</span>}
                  {resource.lessonTitle ? <span>· {resource.lessonTitle}</span> : null}
                  <span>· {formatDate(resource.updatedAt)}</span>
                </div>
                {resource.notes ? <p className="resource-card-notes">{resource.notes}</p> : null}
                <div className="resource-card-foot">
                  <a href={resource.url} target="_blank" rel="noreferrer noopener" className="text-link">
                    Open link <Icon name="external" size={13} />
                  </a>
                  <Button variant="ghost" size="sm" icon="trash" onClick={() => setDeleteTarget(resource)}>
                    Remove
                  </Button>
                </div>
              </Card>
            );
          })}
        </div>
      )}

      <Modal
        open={addOpen}
        title="Add a resource"
        onClose={() => setAddOpen(false)}
        footer={
          <>
            <Button variant="ghost" onClick={() => setAddOpen(false)}>Cancel</Button>
            <Button variant="primary" onClick={add} loading={saving}>Save resource</Button>
          </>
        }
      >
        <Field label="Title" htmlFor="r-title">
          <TextInput id="r-title" value={form.title} onChange={(event) => setForm({ ...form, title: event.target.value })} />
        </Field>
        <Field label="URL" htmlFor="r-url">
          <TextInput id="r-url" value={form.url} onChange={(event) => setForm({ ...form, url: event.target.value })} placeholder="https://…" />
        </Field>
        <div className="form-row">
          <Field label="Type" htmlFor="r-type">
            <Select id="r-type" value={form.type} onChange={(event) => setForm({ ...form, type: event.target.value })}>
              {typeOptions.map((option) => <option key={option} value={option}>{option}</option>)}
            </Select>
          </Field>
          <Field label="Skill" htmlFor="r-skill">
            <Select id="r-skill" value={form.skillId} onChange={(event) => setForm({ ...form, skillId: event.target.value })}>
              <option value="">No skill</option>
              {(skills ?? []).map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
            </Select>
          </Field>
        </div>
        <Field label="Notes" htmlFor="r-notes">
          <TextArea id="r-notes" rows={3} value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} />
        </Field>
      </Modal>

      <Modal
        open={Boolean(deleteTarget)}
        title="Remove this resource?"
        onClose={() => setDeleteTarget(null)}
        footer={
          <>
            <Button variant="ghost" onClick={() => setDeleteTarget(null)}>Cancel</Button>
            <Button variant="danger" onClick={remove}>Remove</Button>
          </>
        }
      >
        <p>{deleteTarget?.title} will be removed from your bookmarks. The link itself is not affected.</p>
      </Modal>
    </div>
  );
}
