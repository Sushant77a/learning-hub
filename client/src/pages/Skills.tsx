import { useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api, errorMessage, formatDate } from "../api";
import type { PlanSnapshot, SkillCard } from "../api/types";
import { useApp } from "../context/AppContext";
import { useApi } from "../hooks/useApi";
import { Icon } from "../components/icons";
import { ReflowModal } from "../components/ReflowModal";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Card,
  ConfidenceMeter,
  EmptyState,
  ErrorNote,
  Field,
  Loading,
  Modal,
  TextArea,
  TextInput
} from "../components/ui";

const badgeClassByTone: Record<string, string> = {
  accent: "bg-[var(--accent-soft)] text-[var(--accent-strong)]",
  warn: "bg-[var(--gold-soft)] text-[var(--gold)]",
  success: "bg-[var(--success-soft)] text-[var(--success)]",
  muted: "border border-[var(--line)] text-[var(--muted)]"
};

export function SkillsPage() {
  const { data, loading, error } = useApi<SkillCard[]>("/skills");
  const { notify, bump } = useApp();
  const navigate = useNavigate();
  const [addOpen, setAddOpen] = useState(false);
  const [form, setForm] = useState({ name: "", area: "", description: "" });
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [reflow, setReflow] = useState<{ before: PlanSnapshot; after: PlanSnapshot; skillId: string } | null>(null);
  const [reflowBusy, setReflowBusy] = useState(false);

  const groups = useMemo(() => {
    const map = new Map<string, SkillCard[]>();
    for (const skill of data ?? []) {
      map.set(skill.area, [...(map.get(skill.area) ?? []), skill]);
    }
    return [...map.entries()].sort(([a], [b]) => a.localeCompare(b));
  }, [data]);

  const createSkill = async () => {
    if (!form.name.trim()) {
      setFormError("A name is required.");
      return;
    }
    setSaving(true);
    setFormError(null);
    try {
      const created = await api.post<{ id: string }>("/skills", {
        name: form.name,
        area: form.area || "My skills",
        description: form.description
      });
      notify("Skill added. Add lessons to its curriculum folder when you are ready.", "success");
      setAddOpen(false);
      setForm({ name: "", area: "", description: "" });
      bump();
      navigate(`/skills/${created.id}`);
    } catch (err) {
      setFormError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const startStudy = async (skill: SkillCard) => {
    if (skill.trackState === "active") {
      navigate(`/skills/${skill.id}`);
      return;
    }
    try {
      const before = await api.get<PlanSnapshot>("/plan");
      const after = await api.post<PlanSnapshot>("/tracks/preview", { skillId: skill.id });
      setReflow({ before, after, skillId: skill.id });
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  const applyStudy = async () => {
    if (!reflow) return;
    setReflowBusy(true);
    try {
      await api.post("/tracks", { skillId: reflow.skillId });
      notify("Track started. The plan has been reflowed.", "success");
      setReflow(null);
      bump();
    } catch (err) {
      notify(errorMessage(err), "error");
    } finally {
      setReflowBusy(false);
    }
  };

  if (loading && !data) return <div className="page"><Loading label="Loading skills…" /></div>;
  if (error) return <div className="page"><ErrorNote>{error}</ErrorNote></div>;

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <span className="eyebrow">Library</span>
          <h1>Skills</h1>
          <p className="page-subtitle">
            Everything you are learning, grouped by area. Confidence blends your ratings, accuracy, tasks, and time since practice.
          </p>
        </div>
        <Button onClick={() => setAddOpen(true)}>
          <Icon name="plus" size={14} /> Add a skill
        </Button>
      </header>

      {groups.length === 0 ? (
        <EmptyState title="No skills yet" action={<Button variant="outline" onClick={() => setAddOpen(true)}>Add your first skill</Button>}>
          Add anything you want to learn. A skill can be as broad as C# or as narrow as Nginx configuration.
        </EmptyState>
      ) : null}

      {groups.map(([area, skills]) => (
        <section className="area-section" key={area}>
          <div className="section-heading">
            <h2>{area}</h2>
            <span className="muted small">{skills.length} skill{skills.length === 1 ? "" : "s"}</span>
          </div>
          <div className="skill-grid">
            {skills.map((skill) => (
              <SkillCardView key={skill.id} skill={skill} onStudy={() => startStudy(skill)} />
            ))}
          </div>
        </section>
      ))}

      <Modal
        open={addOpen}
        title="Add a skill"
        onClose={() => setAddOpen(false)}
        footer={
          <>
            <Button variant="ghost" onClick={() => setAddOpen(false)}>Cancel</Button>
            <Button onClick={createSkill} disabled={saving}>{saving ? "Creating…" : "Create skill"}</Button>
          </>
        }
      >
        <Field label="Name" htmlFor="skill-name" hint="For example: Embeddings, PostgreSQL, System design.">
          <TextInput
            id="skill-name"
            value={form.name}
            onChange={(event) => setForm({ ...form, name: event.target.value })}
            placeholder="Skill name"
            autoFocus
          />
        </Field>
        <Field label="Area" htmlFor="skill-area" hint="Leave blank for 'My skills'.">
          <TextInput
            id="skill-area"
            value={form.area}
            onChange={(event) => setForm({ ...form, area: event.target.value })}
            placeholder="Languages, Data & AI, Infrastructure…"
          />
        </Field>
        <Field label="Why are you learning it?" htmlFor="skill-description">
          <TextArea
            id="skill-description"
            rows={3}
            value={form.description}
            onChange={(event) => setForm({ ...form, description: event.target.value })}
            placeholder="A sentence that keeps the goal concrete."
          />
        </Field>
        {formError ? <ErrorNote>{formError}</ErrorNote> : null}
      </Modal>

      <ReflowModal
        open={Boolean(reflow)}
        title={reflow?.after.tracks.some((t) => t.state === "active" && t.firstLessonDate === reflow.after.today) ? "Start this track?" : "Resume this track?"}
        description="Starting a track changes your daily time split. Check the projected dates before applying."
        before={reflow?.before}
        after={reflow?.after ?? null}
        onCancel={() => setReflow(null)}
        onConfirm={applyStudy}
        busy={reflowBusy}
        confirmLabel="Start studying"
      />
    </div>
  );
}

function SkillCardView({ skill, onStudy }: { skill: SkillCard; onStudy: () => void }) {
  const stateLabel =
    skill.trackState === "active"
      ? "Active"
      : skill.trackState === "paused"
        ? "Paused"
        : skill.trackState === "completed"
          ? "Completed"
          : "Not started";
  const tone =
    skill.trackState === "active"
      ? "accent"
      : skill.trackState === "paused"
        ? "warn"
        : skill.trackState === "completed"
          ? "success"
          : "muted";
  const badgeClass = badgeClassByTone[tone] ?? "";

  return (
    <Card className={`skill-card state-${skill.trackState}`} as="article">
      <div className="skill-card-head">
        <span className="skill-glyph">{skill.name.slice(0, 2).toUpperCase()}</span>
        <div className="skill-card-title">
          <h3>{skill.name}</h3>
          <div className="card-badges">
            <Badge variant="outline" className={badgeClass}>{stateLabel}</Badge>
            {skill.needsRefresh ? (
              <Badge variant="outline" className={badgeClassByTone.warn}>Needs a refresher</Badge>
            ) : null}
          </div>
        </div>
      </div>

      <p className="skill-description">{skill.description || "No description yet."}</p>

      <div className="skill-stats">
        <div>
          <span className="stat-value">{skill.lessonsDone}<small>/{skill.lessonsTotal}</small></span>
          <span className="stat-label">lessons</span>
        </div>
        <div className="confidence-stat">
          <ConfidenceMeter score={skill.confidence} compact />
          <span className="stat-label">confidence</span>
        </div>
        <div>
          <span className="stat-value small-date">{skill.lastPractised ? formatDate(skill.lastPractised) : "—"}</span>
          <span className="stat-label">
            {skill.lastPractised ? `${skill.daysSincePractice}d ago` : "last practised"}
          </span>
        </div>
      </div>

      {skill.weakSubtopics.length > 0 ? (
        <div className="weak-topics">
          <span className="muted small">Weak spots</span>
          <div className="chip-row">
            {skill.weakSubtopics.map((topic) => (
              <span className="chip static" key={topic.name}>
                {topic.name} · {Math.round(topic.accuracy * 100)}%
              </span>
            ))}
          </div>
        </div>
      ) : null}

      {skill.isDsa && skill.currentDsaTopic ? (
        <div className="current-topic">
          <Icon name="target" size={14} />
          Current topic: <strong>{skill.currentDsaTopic.replace(/-/g, " ")}</strong>
        </div>
      ) : null}

      {skill.needsRefresh ? (
        <p className="refresher-note">
          A short refresher would restore this. Confidence decays without practice.
        </p>
      ) : null}

      <div className="skill-card-foot">
        <Link to={`/skills/${skill.id}`} className="text-link">
          Details <Icon name="arrowRight" size={14} />
        </Link>
        <Button variant={skill.trackState === "active" ? "outline" : "default"} size="sm" onClick={onStudy}>
          {skill.trackState === "active" ? "Continue" : skill.trackState === "paused" ? "Resume" : "Study this"}
        </Button>
      </div>
    </Card>
  );
}
