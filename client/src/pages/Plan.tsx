import { useEffect, useState, type ReactNode } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api, errorMessage, formatDate, minutesLabel, todayIso, weekdaysShort } from "../api";
import type { PlanSnapshot, SkillCard } from "../api/types";
import { useApp } from "../context/AppContext";
import { useApi } from "../hooks/useApi";
import { Icon } from "../components/icons";
import { ReflowModal } from "../components/ReflowModal";
import {
  Badge,
  Button,
  Card,
  EmptyState,
  ErrorNote,
  Field,
  InfoNote,
  Loading,
  Modal,
  ProgressBar,
  Segmented,
  Select,
  TextInput
} from "../components/ui";

interface TimeBoxDraft {
  skillId: string;
  newName: string;
  newArea: string;
  startDate: string;
  days: number;
  mode: "pause" | "parallel";
  detourPercent: number;
  label: string;
}

interface PendingReflow {
  title: string;
  description: string;
  before: PlanSnapshot | null;
  after: PlanSnapshot;
  label: string;
  apply: () => Promise<void>;
}

export function PlanPage() {
  const { data: plan, loading, error, reload } = useApi<PlanSnapshot>("/plan");
  const { data: skills } = useApi<SkillCard[]>("/skills");
  const { notify, bump, settings } = useApp();
  const navigate = useNavigate();
  const [dailyMinutes, setDailyMinutes] = useState(120);
  const [restWeekdays, setRestWeekdays] = useState<string[]>([]);
  const [startDate, setStartDate] = useState(todayIso());
  const [pending, setPending] = useState<PendingReflow | null>(null);
  const [busy, setBusy] = useState(false);
  const [timeBoxOpen, setTimeBoxOpen] = useState(false);
  const [detourDecision, setDetourDecision] = useState<"resume" | "keepBoth" | "extend" | null>(null);
  const [extendTo, setExtendTo] = useState("");
  const [generateFor, setGenerateFor] = useState<string | null>(null);

  useEffect(() => {
    if (!plan) return;
    setDailyMinutes(plan.dailyMinutes);
    setRestWeekdays(plan.restWeekdays);
    setStartDate(plan.startDate);
  }, [plan?.dailyMinutes, plan?.restWeekdays.join(","), plan?.startDate]);

  if (loading && !plan) return <div className="page"><Loading label="Building the plan…" /></div>;
  if (error || !plan) return <div className="page"><ErrorNote>{error ?? "The plan could not be loaded."}</ErrorNote></div>;

  const activeCount = plan.tracks.filter((track) => track.state === "active").length;
  const settingsDirty =
    dailyMinutes !== plan.dailyMinutes ||
    restWeekdays.slice().sort().join(",") !== plan.restWeekdays.slice().sort().join(",") ||
    startDate !== plan.startDate;

  const previewSettings = async () => {
    try {
      const after = await api.post<PlanSnapshot>("/plan/preview", {
        dailyMinutes,
        restWeekdays: restWeekdays.join(","),
        startDate
      });
      setPending({
        title: "Apply these plan settings?",
        description: "Here is how the change moves every projected finish date.",
        before: plan,
        after,
        label: "Apply and reflow",
        apply: async () => {
          await api.post("/plan/apply", {
            dailyMinutes,
            restWeekdays: restWeekdays.join(","),
            startDate
          });
        }
      });
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  const previewTrackState = async (trackId: number, state: "active" | "paused", name: string) => {
    try {
      const after = await api.post<PlanSnapshot>(`/tracks/${trackId}/state/preview`, { state });
      setPending({
        title: state === "paused" ? `Pause ${name}?` : `Resume ${name}?`,
        description:
          state === "paused"
            ? "A paused track keeps its progress and shifts its remaining lessons forward."
            : "Resuming gives this track a share of your daily time and reflows the plan.",
        before: plan,
        after,
        label: state === "paused" ? "Pause and reflow" : "Resume and reflow",
        apply: async () => {
          await api.post(`/tracks/${trackId}/state`, { state });
        }
      });
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  const applyPending = async () => {
    if (!pending) return;
    setBusy(true);
    try {
      await pending.apply();
      notify("Plan updated. New dates applied.", "success");
      setPending(null);
      setTimeBoxOpen(false);
      reload();
      bump();
    } catch (err) {
      notify(errorMessage(err), "error");
    } finally {
      setBusy(false);
    }
  };

  const prepareTimeBox = async (draft: TimeBoxDraft) => {
    try {
      let skillId = draft.skillId;
      let skillName = skills?.find((skill) => skill.id === skillId)?.name ?? "";
      if (!skillId) {
        if (!draft.newName.trim()) {
          notify("Name the new skill first.", "error");
          return;
        }
        const created = await api.post<{ id: string; name: string }>("/skills", {
          name: draft.newName,
          area: draft.newArea || "My skills",
          description: `Focused study for a ${draft.days}-day time box.`
        });
        skillId = created.id;
        skillName = created.name;
      }

      const body = {
        skillId,
        startDate: draft.startDate,
        days: draft.days,
        mode: draft.mode,
        detourPercent: draft.detourPercent,
        label: draft.label || `${skillName} detour`
      };
      const after = await api.post<PlanSnapshot>("/timeboxes/preview", body);
      setPending({
        title: `${skillName} time box`,
        description:
          draft.mode === "pause"
            ? `Pause the current tracks for ${draft.days} day${draft.days === 1 ? "" : "s"} and give ${skillName} all of your daily time.`
            : `Run ${skillName} in parallel at ${draft.detourPercent}% of daily time for ${draft.days} day${draft.days === 1 ? "" : "s"}.`,
        before: plan,
        after,
        label: "Start the detour",
        apply: async () => {
          const result = await api.post<{ needsCurriculum: boolean; trackId: number }>("/timeboxes", body);
          setTimeBoxOpen(false);
          if (result.needsCurriculum) {
            notify("Detour started, but this skill has no lessons yet. Generate a mini-curriculum next.", "info");
            navigate(`/skills/${skillId}`);
          }
        }
      });
      setTimeBoxOpen(false);
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  const endDetour = async (choice: "resume" | "keepBoth" | "extend") => {
    if (!plan.detour) return;
    try {
      await api.post(`/timeboxes/${plan.detour.timeBoxId}/end`, {
        choice,
        extendTo: choice === "extend" ? extendTo : null
      });
      notify(choice === "extend" ? "Detour extended." : "Detour ended and the plan reflowed.", "success");
      setDetourDecision(null);
      reload();
      bump();
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  const generateCurriculum = async (skillId: string) => {
    setGenerateFor(skillId);
    try {
      await api.post(`/skills/${skillId}/generate-curriculum`, {
        days: plan.detour
          ? Math.max(2, Math.min(8, Math.ceil((new Date(plan.detour.endDate).getTime() - new Date().getTime()) / 86400000) + 1))
          : 5
      });
      notify("Mini-curriculum generated. Review the files in the curriculum folder.", "success");
      reload();
      bump();
    } catch (err) {
      notify(errorMessage(err), "error");
    } finally {
      setGenerateFor(null);
    }
  };

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <span className="eyebrow">Plan</span>
          <h1>Your study plan</h1>
          <p className="page-subtitle">
            {minutesLabel(plan.dailyMinutes)} a day across {activeCount} active {activeCount === 1 ? "track" : "tracks"}. Change
            anything here and review the new dates before applying.
          </p>
        </div>
        <Button variant="primary" icon="target" onClick={() => setTimeBoxOpen(true)}>
          Switch focus
        </Button>
      </header>

      <div className="chip-row" style={{ marginBottom: "1.25rem" }} aria-label="Plan at a glance">
        <span className="chip static">{minutesLabel(plan.dailyMinutes)} a day</span>
        <span className="chip static">
          {activeCount} active {activeCount === 1 ? "track" : "tracks"}
        </span>
        <span className="chip static">
          {plan.restWeekdays.length > 0 ? `${plan.restWeekdays.join(" & ")} off` : "no rest days"}
        </span>
        <span className="chip static">started {formatDate(plan.startDate)}</span>
      </div>

      {activeCount > 3 ? (
        <InfoNote tone="warn">
          {activeCount} tracks are active. Daily time per track is thin, which makes progress slow everywhere. You can continue, but
          pausing one track will make the others more effective.
        </InfoNote>
      ) : null}

      {plan.detour ? (
        <Card className="detour-plan-card">
          <div className="card-heading">
            <div>
              <Badge tone="warn">Active detour</Badge>
              <h2>{plan.detour.skillName}</h2>
            </div>
            <span className="muted small">
              {formatDate(plan.detour.startDate)} – {formatDate(plan.detour.endDate)} · day {plan.detour.dayNumber} of{" "}
              {plan.detour.totalDays}
            </span>
          </div>
          <p>
            {plan.detour.mode === "pause"
              ? `${plan.detour.pausedSkillNames.join(", ") || "Other tracks"} paused. ${plan.detour.skillName} gets all of your daily time.`
              : `Running in parallel: ${plan.detour.skillName} at ${plan.detour.detourPercent}%, the rest at ${
                  100 - plan.detour.detourPercent
                }%.`}
          </p>
          {plan.detour.decisionDue ? (
            <InfoNote tone="warn">The time box has ended. Choose what happens next.</InfoNote>
          ) : null}
          <div className="button-row">
            <Button size="sm" variant="primary" onClick={() => setDetourDecision("resume")}>
              End and resume paused
            </Button>
            <Button size="sm" variant="secondary" onClick={() => setDetourDecision("keepBoth")}>
              Keep both running
            </Button>
            <Button size="sm" variant="ghost" onClick={() => setDetourDecision("extend")}>
              Extend
            </Button>
            {skills?.find((skill) => skill.id === plan.detour?.skillId)?.lessonsTotal === 0 ? (
              <Button
                size="sm"
                variant="ghost"
                icon="sparkles"
                loading={generateFor === plan.detour.skillId}
                onClick={() => generateCurriculum(plan.detour!.skillId)}
              >
                Generate mini-curriculum
              </Button>
            ) : null}
          </div>
        </Card>
      ) : null}

      <div className="plan-grid">
        <Card className="plan-settings">
          <div className="card-heading">
            <h2>Daily shape</h2>
            {settingsDirty ? <Badge tone="accent">Unsaved</Badge> : <span className="muted small">in effect</span>}
          </div>

          <Field label="Daily study time" htmlFor="daily-minutes">
            <div className="minutes-row">
              <input
                id="daily-minutes"
                type="range"
                min={15}
                max={360}
                step={15}
                value={dailyMinutes}
                onChange={(event) => setDailyMinutes(Number(event.target.value))}
                className="range"
              />
              <span className="minutes-value">{minutesLabel(dailyMinutes)}</span>
            </div>
            <div className="preset-row">
              {[30, 60, 90, 120, 180].map((minutes) => (
                <button
                  key={minutes}
                  type="button"
                  className={`chip ${dailyMinutes === minutes ? "on" : ""}`}
                  onClick={() => setDailyMinutes(minutes)}
                >
                  {minutesLabel(minutes)}
                </button>
              ))}
            </div>
          </Field>

          <div className="field">
            <span className="field-label">Rest days</span>
            <div className="weekday-row">
              {weekdaysShort().map((day) => {
                const selected = restWeekdays.includes(day.key);
                return (
                  <button
                    key={day.key}
                    type="button"
                    className={`weekday ${selected ? "on" : ""}`}
                    aria-pressed={selected}
                    onClick={() =>
                      setRestWeekdays((current) =>
                        selected ? current.filter((item) => item !== day.key) : [...current, day.key]
                      )
                    }
                  >
                    {day.label}
                  </button>
                );
              })}
            </div>
            <div className="field-hint">Rest days never count as missed and do not break the streak.</div>
          </div>

          <Field label="Plan start" htmlFor="plan-start" hint="Past uncompleted days shift forward automatically.">
            <TextInput
              id="plan-start"
              type="date"
              value={startDate}
              onChange={(event) => setStartDate(event.target.value)}
            />
          </Field>

          <Button variant="primary" onClick={previewSettings} disabled={!settingsDirty}>
            Preview new dates
          </Button>
        </Card>

        <Card className="plan-tracks">
          <div className="card-heading">
            <h2>Tracks</h2>
            <span className="muted small">projected finish per track</span>
          </div>
          {plan.tracks.filter((track) => track.lessonsTotal > 0).length === 0 ? (
            <EmptyState title="No tracks yet" icon="skills" action={<Link to="/skills" className="btn btn-primary btn-sm">Choose a skill</Link>}>
              Start a skill to create your first track.
            </EmptyState>
          ) : (
            <div className="track-list">
              {plan.tracks
                .filter((track) => track.lessonsTotal > 0)
                .map((track) => (
                  <div className={`track-row state-${track.state}`} key={track.trackId}>
                    <div className="track-row-main">
                      <div className="track-row-title">
                        <Link to={`/skills/${track.skillId}`}>{track.skillName}</Link>
                        <Badge tone={track.state === "active" ? "accent" : track.state === "paused" ? "warn" : track.state === "completed" ? "success" : "muted"}>
                          {track.state}
                        </Badge>
                        {track.isDetour ? <Badge tone="warn">detour</Badge> : null}
                      </div>
                      <div className="track-row-meta">
                        <span>
                          {track.lessonsDone}/{track.lessonsTotal} lessons
                        </span>
                        <span>
                          {track.dailyMinutes > 0
                            ? `${minutesLabel(track.dailyMinutes)}/day · ${track.sharePercent}%`
                            : "no time allocated"}
                        </span>
                        <span>
                          finish{" "}
                          {track.finishDate
                            ? formatDate(track.finishDate, { day: "numeric", month: "short", year: "numeric" })
                            : "—"}
                        </span>
                      </div>
                      <ProgressBar value={track.lessonsDone} max={Math.max(1, track.lessonsTotal)} small />
                    </div>
                    <div className="track-row-actions">
                      {track.state === "active" ? (
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => previewTrackState(track.trackId, "paused", track.skillName)}
                        >
                          Pause
                        </Button>
                      ) : track.state === "paused" ? (
                        <Button
                          size="sm"
                          variant="secondary"
                          onClick={() => previewTrackState(track.trackId, "active", track.skillName)}
                        >
                          Resume
                        </Button>
                      ) : null}
                      {track.resumeOn ? (
                        <span className="muted small">resumes {formatDate(track.resumeOn)}</span>
                      ) : null}
                    </div>
                  </div>
                ))}
            </div>
          )}
          <div className="plan-foot-note">
            <Icon name="clock" size={14} />
            <span>
              Projections assume one lesson per track per study day and use the estimate in each lesson file.
            </span>
          </div>
        </Card>
      </div>

      <section className="plan-switch" id="switch-focus">
        <div>
          <span className="eyebrow">Detour</span>
          <h2>Switch focus for a while</h2>
          <p>
            Time-box a different skill and choose whether it pauses your current tracks or runs in parallel. The old schedule shifts;
            nothing is lost.
          </p>
        </div>
        <Button variant="secondary" size="lg" icon="target" onClick={() => setTimeBoxOpen(true)}>
          Plan a detour
        </Button>
      </section>

      <TimeBoxModal open={timeBoxOpen} onClose={() => setTimeBoxOpen(false)} skills={skills ?? []} onPrepare={prepareTimeBox} />

      <ReflowModal
        open={Boolean(pending)}
        title={pending?.title ?? "Confirm the new schedule"}
        description={pending?.description}
        before={pending?.before}
        after={pending?.after ?? null}
        onCancel={() => setPending(null)}
        onConfirm={applyPending}
        busy={busy}
        confirmLabel={pending?.label ?? "Apply new dates"}
      />

      <Modal
        open={detourDecision !== null}
        title={detourDecision === "extend" ? "Extend the detour" : detourDecision === "keepBoth" ? "Keep both tracks" : "End the detour"}
        onClose={() => setDetourDecision(null)}
        footer={
          <>
            <Button variant="ghost" onClick={() => setDetourDecision(null)}>Cancel</Button>
            <Button
              variant="primary"
              disabled={detourDecision === "extend" && !extendTo}
              onClick={() => detourDecision && endDetour(detourDecision)}
            >
              Confirm
            </Button>
          </>
        }
      >
        {detourDecision === "extend" ? (
          <Field label="New end date" htmlFor="detour-end">
            <TextInput
              id="detour-end"
              type="date"
              min={todayIso()}
              value={extendTo}
              onChange={(event) => setExtendTo(event.target.value)}
            />
          </Field>
        ) : detourDecision === "keepBoth" ? (
          <p>Both tracks stay active and share your daily time equally from tomorrow.</p>
        ) : (
          <p>The detour ends, its track pauses, and the tracks it paused become active again.</p>
        )}
      </Modal>
    </div>
  );
}

function TimeBoxModal({
  open,
  onClose,
  skills,
  onPrepare
}: {
  open: boolean;
  onClose: () => void;
  skills: SkillCard[];
  onPrepare: (draft: TimeBoxDraft) => Promise<void>;
}) {
  const [draft, setDraft] = useState<TimeBoxDraft>({
    skillId: "",
    newName: "",
    newArea: "",
    startDate: todayIso(),
    days: 5,
    mode: "pause",
    detourPercent: 40,
    label: ""
  });
  const [creatingNew, setCreatingNew] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!open) return;
    setDraft((current) => ({ ...current, skillId: skills[0]?.id ?? "", startDate: todayIso() }));
    setCreatingNew(skills.length === 0);
  }, [open, skills.length]);

  const submit = async () => {
    if (!creatingNew && !draft.skillId) return;
    if (creatingNew && !draft.newName.trim()) return;
    setBusy(true);
    try {
      await onPrepare(draft);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      open={open}
      title="Switch focus"
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button variant="primary" onClick={submit} loading={busy}>
            Preview new dates
          </Button>
        </>
      }
    >
      <p className="modal-lead">
        A time box is a deliberate detour. At the end you decide whether to resume, extend, or keep both tracks.
      </p>

      <div className="field">
        <span className="field-label">Skill</span>
        <Segmented
          value={creatingNew ? "new" : "existing"}
          onChange={(value) => setCreatingNew(value === "new")}
          options={[
            { value: "existing", label: "Existing skill" },
            { value: "new", label: "New skill" }
          ]}
          ariaLabel="Choose an existing or new skill"
        />
      </div>

      {creatingNew ? (
        <>
          <Field label="New skill name" htmlFor="tb-name">
            <TextInput
              id="tb-name"
              value={draft.newName}
              onChange={(event) => setDraft({ ...draft, newName: event.target.value })}
              placeholder="e.g. Nginx"
            />
          </Field>
          <Field label="Area" htmlFor="tb-area" hint="Leave blank for 'My skills'.">
            <TextInput
              id="tb-area"
              value={draft.newArea}
              onChange={(event) => setDraft({ ...draft, newArea: event.target.value })}
              placeholder="Infrastructure"
            />
          </Field>
        </>
      ) : (
        <Field label="Existing skill" htmlFor="tb-skill">
          <Select
            id="tb-skill"
            value={draft.skillId}
            onChange={(event) => setDraft({ ...draft, skillId: event.target.value })}
          >
            {skills.map((skill) => (
              <option value={skill.id} key={skill.id}>
                {skill.name}
              </option>
            ))}
          </Select>
        </Field>
      )}

      <div className="form-row">
        <Field label="Start" htmlFor="tb-start">
          <TextInput
            id="tb-start"
            type="date"
            value={draft.startDate}
            onChange={(event) => setDraft({ ...draft, startDate: event.target.value })}
          />
        </Field>
        <Field label="Duration (days)" htmlFor="tb-days">
          <TextInput
            id="tb-days"
            type="number"
            min={1}
            max={60}
            value={draft.days}
            onChange={(event) => setDraft({ ...draft, days: Math.max(1, Math.min(60, Number(event.target.value))) })}
          />
        </Field>
      </div>

      <div className="field">
        <span className="field-label">How should it run?</span>
        <Segmented
          value={draft.mode}
          onChange={(mode) => setDraft({ ...draft, mode })}
          options={[
            { value: "pause", label: "Pause current track" },
            { value: "parallel", label: "Run in parallel" }
          ]}
        />
        <div className="field-hint">
          {draft.mode === "pause"
            ? "The detour gets all of your daily time. Paused tracks resume afterwards."
            : "Both tracks stretch. Choose how much of the day the detour gets."}
        </div>
      </div>

      {draft.mode === "parallel" ? (
        <div className="split-control">
          <div className="split-labels">
            <span>Current tracks {100 - draft.detourPercent}%</span>
            <span>Detour {draft.detourPercent}%</span>
          </div>
          <input
            type="range"
            min={10}
            max={90}
            step={5}
            value={draft.detourPercent}
            onChange={(event) => setDraft({ ...draft, detourPercent: Number(event.target.value) })}
            className="range"
            aria-label="Detour share of daily time"
          />
        </div>
      ) : null}

      <Field label="Label" htmlFor="tb-label" hint="Optional. Shown in the Today banner.">
        <TextInput
          id="tb-label"
          value={draft.label}
          onChange={(event) => setDraft({ ...draft, label: event.target.value })}
          placeholder="e.g. Nginx detour"
        />
      </Field>

      <InfoNote tone="info">
        If the chosen skill has no lessons, the detour can still start. You will be offered an AI-generated mini-curriculum sized to
        the time box, saved as editable files.
      </InfoNote>
    </Modal>
  );
}
