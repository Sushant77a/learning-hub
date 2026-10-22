import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { api, errorMessage, formatDate } from "../api";
import type { Note, SkillCard } from "../api/types";
import { useApp } from "../context/AppContext";
import { useApi } from "../hooks/useApi";
import { Markdown } from "../components/Markdown";
import { Icon } from "../components/icons";
import {
  Badge,
  Button,
  EmptyState,
  ErrorNote,
  Field,
  Loading,
  Modal,
  Segmented,
  Select,
  TextArea,
  TextInput
} from "../components/ui";

export function NotesPage() {
  const [params] = useSearchParams();
  const { notify } = useApp();
  const [query, setQuery] = useState("");
  const [skillFilter, setSkillFilter] = useState("");
  const { data: notes, loading, error, reload } = useApi<Note[]>(
    `/notes?${new URLSearchParams({ ...(query ? { q: query } : {}), ...(skillFilter ? { skillId: skillFilter } : {}) })}`
  );
  const { data: skills } = useApi<SkillCard[]>("/skills");
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [draft, setDraft] = useState<{ id?: number; title: string; body: string; skillId: string; lessonId: string }>({
    title: "",
    body: "",
    skillId: "",
    lessonId: ""
  });
  const [tab, setTab] = useState<"write" | "preview">("write");
  const [saving, setSaving] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [errorState, setErrorState] = useState<string | null>(null);

  const queryNote = Number(params.get("note") ?? 0);
  const isNew = params.get("new") === "1";
  const lessonParam = params.get("lesson") ?? "";
  const skillParam = params.get("skill") ?? "";

  useEffect(() => {
    if (!notes) return;
    if (isNew) {
      setDraft({
        title: "",
        body: "",
        skillId: skillParam,
        lessonId: lessonParam
      });
      setSelectedId(null);
      return;
    }
    const target = queryNote ? notes.find((note) => note.id === queryNote) : notes[0];
    if (target) {
      setSelectedId(target.id);
      setDraft({
        id: target.id,
        title: target.title,
        body: target.body,
        skillId: target.skillId ?? "",
        lessonId: target.lessonId ?? ""
      });
    } else if (notes.length === 0) {
      setSelectedId(null);
      setDraft({ title: "", body: "", skillId: "", lessonId: "" });
    }
  }, [notes, queryNote, isNew, lessonParam, skillParam]);

  const selected = useMemo(() => notes?.find((note) => note.id === selectedId) ?? null, [notes, selectedId]);

  const selectNote = (note: Note) => {
    setSelectedId(note.id);
    setDraft({
      id: note.id,
      title: note.title,
      body: note.body,
      skillId: note.skillId ?? "",
      lessonId: note.lessonId ?? ""
    });
    setTab("write");
  };

  const newNote = () => {
    setSelectedId(null);
    setDraft({ title: "", body: "", skillId: skillFilter || "", lessonId: "" });
    setTab("write");
  };

  const save = async () => {
    if (!draft.title.trim()) {
      setErrorState("Give the note a title.");
      return;
    }
    setSaving(true);
    setErrorState(null);
    try {
      if (draft.id) {
        await api.patch(`/notes/${draft.id}`, { title: draft.title, body: draft.body });
        notify("Note saved.", "success");
        reload();
      } else {
        const created = await api.post<Note>("/notes", {
          title: draft.title,
          body: draft.body,
          skillId: draft.skillId || null,
          lessonId: draft.lessonId || null
        });
        setSelectedId(created.id);
        setDraft({ ...draft, id: created.id });
        notify("Note created.", "success");
        reload();
      }
    } catch (err) {
      setErrorState(errorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const remove = async () => {
    if (!draft.id) return;
    try {
      await api.del(`/notes/${draft.id}`);
      notify("Note deleted.", "success");
      setDeleteOpen(false);
      setSelectedId(null);
      setDraft({ title: "", body: "", skillId: "", lessonId: "" });
      reload();
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  if (loading && !notes) return <div className="page"><Loading label="Opening notes…" /></div>;
  if (error) return <div className="page"><ErrorNote>{error}</ErrorNote></div>;

  return (
    <div className="page notes-page">
      <header className="page-header">
        <div>
          <span className="eyebrow">Writing</span>
          <h1>Notes</h1>
          <p className="page-subtitle">Markdown notes attached to a lesson or a skill. Code blocks are highlighted.</p>
        </div>
        <Button variant="primary" icon="plus" onClick={newNote}>
          New note
        </Button>
      </header>

      <div className="notes-layout">
        <aside className="notes-list">
          <div className="notes-tools">
            <div className="search-input">
              <Icon name="search" size={15} />
              <input
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Search notes…"
                aria-label="Search notes"
              />
            </div>
            <Select value={skillFilter} onChange={(event) => setSkillFilter(event.target.value)} aria-label="Filter notes by skill">
              <option value="">All skills</option>
              {(skills ?? []).map((skill) => (
                <option key={skill.id} value={skill.id}>
                  {skill.name}
                </option>
              ))}
            </Select>
          </div>

          {!notes || notes.length === 0 ? (
            <div className="notes-empty-list">
              <p>No notes yet.</p>
              <Button variant="ghost" size="sm" onClick={newNote}>
                Write the first one
              </Button>
            </div>
          ) : (
            <ul>
              {notes.map((note) => (
                <li key={note.id}>
                  <button className={`note-item ${note.id === selectedId ? "on" : ""}`} onClick={() => selectNote(note)}>
                    <span className="note-item-title">{note.title}</span>
                    <span className="note-item-meta">
                      {note.lessonId ? "Lesson" : note.skillId ? "Skill" : "General"} · {formatDate(note.updatedAt)}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </aside>

        <section className="note-editor">
          {selected?.lessonId ? (
            <div className="note-context">
              <Badge tone="accent">Lesson note</Badge>
              <Link to={`/lesson/${selected.lessonId}`} className="text-link">
                Open the lesson <Icon name="arrowRight" size={13} />
              </Link>
            </div>
          ) : lessonParam && !draft.id ? (
            <div className="note-context">
              <Badge tone="accent">New lesson note</Badge>
              <span className="muted small">This note will be attached to the lesson you came from.</span>
            </div>
          ) : null}

          <div className="note-title-row">
            <TextInput
              className="note-title-input"
              value={draft.title}
              onChange={(event) => setDraft({ ...draft, title: event.target.value })}
              placeholder="Note title"
              aria-label="Note title"
            />
            <div className="button-row">
              {draft.id ? (
                <Button variant="ghost" size="sm" icon="trash" onClick={() => setDeleteOpen(true)}>
                  Delete
                </Button>
              ) : null}
              <Button variant="primary" size="sm" onClick={save} loading={saving}>
                {draft.id ? "Save" : "Create note"}
              </Button>
            </div>
          </div>

          {!draft.id ? (
            <div className="note-meta-row">
              <Field label="Skill" htmlFor="note-skill">
                <Select
                  id="note-skill"
                  value={draft.skillId}
                  onChange={(event) => setDraft({ ...draft, skillId: event.target.value })}
                >
                  <option value="">No skill</option>
                  {(skills ?? []).map((skill) => (
                    <option key={skill.id} value={skill.id}>
                      {skill.name}
                    </option>
                  ))}
                </Select>
              </Field>
            </div>
          ) : null}

          <div className="note-tabs">
            <Segmented
              value={tab}
              onChange={setTab}
              options={[
                { value: "write", label: "Write" },
                { value: "preview", label: "Preview" }
              ]}
            />
            <span className="muted small">{draft.body.length} characters</span>
          </div>

          {tab === "write" ? (
            <TextArea
              className="note-body"
              rows={22}
              value={draft.body}
              onChange={(event) => setDraft({ ...draft, body: event.target.value })}
              placeholder="# A heading&#10;&#10;Write in Markdown. Use code fences for examples.&#10;&#10;```csharp&#10;var note = new Note();&#10;```"
              aria-label="Note body"
              spellCheck={false}
            />
          ) : (
            <div className="note-preview">
              {draft.body.trim() ? <Markdown>{draft.body}</Markdown> : <p className="muted">Nothing to preview yet.</p>}
            </div>
          )}

          {errorState ? <ErrorNote>{errorState}</ErrorNote> : null}
        </section>
      </div>

      <Modal
        open={deleteOpen}
        title="Delete this note?"
        onClose={() => setDeleteOpen(false)}
        footer={
          <>
            <Button variant="ghost" onClick={() => setDeleteOpen(false)}>Cancel</Button>
            <Button variant="danger" onClick={remove}>Delete note</Button>
          </>
        }
      >
        <p>This cannot be undone. The note is not included in future backups once deleted.</p>
      </Modal>
    </div>
  );
}
