import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { api, errorMessage, formatDate, minutesLabel } from "../api";
import type { PlanSnapshot, SkillDetail } from "../api/types";
import { useApp } from "../context/AppContext";
import { useApi } from "../hooks/useApi";
import { Icon } from "../components/icons";
import { ReflowModal } from "../components/ReflowModal";
import { CustomQuestionModal } from "../components/CustomQuestionModal";
import {
  Badge,
  Button,
  Card,
  ConfidenceMeter,
  EmptyState,
  ErrorNote,
  InfoNote,
  Loading,
  Modal,
  ProgressBar,
  Sparkline
} from "../components/ui";

export function SkillDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { data, loading, error, reload } = useApi<SkillDetail>(id ? `/skills/${id}` : null);
  const { notify, bump, openResources } = useApp();
  const [reflow, setReflow] = useState<{ before: PlanSnapshot; after: PlanSnapshot; action: "pause" | "resume" | "start" } | null>(null);
  const [reflowBusy, setReflowBusy] = useState(false);
  const [topicModal, setTopicModal] = useState(false);
  const [questionOpen, setQuestionOpen] = useState(false);
  const [generating, setGenerating] = useState(false);
  const [generateError, setGenerateError] = useState<string | null>(null);

  const previewState = async (action: "pause" | "resume" | "start") => {
    if (!data) return;
    try {
      const before = await api.get<PlanSnapshot>("/plan");
      const after =
        action === "start" || !data.card.trackId
          ? await api.post<PlanSnapshot>("/tracks/preview", { skillId: data.card.id })
          : await api.post<PlanSnapshot>(`/tracks/${data.card.trackId}/state/preview`, {
              state: action === "pause" ? "paused" : "active"
            });
      setReflow({ before, after, action });
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  const applyState = async () => {
    if (!data || !reflow) return;
    setReflowBusy(true);
    try {
      if (reflow.action === "start" || !data.card.trackId) {
        await api.post("/tracks", { skillId: data.card.id });
      } else {
        await api.post(`/tracks/${data.card.trackId}/state`, {
          state: reflow.action === "pause" ? "paused" : "active"
        });
      }
      notify(
        reflow.action === "pause"
          ? "Track paused. Its lessons have shifted forward."
          : "Track active. The plan has been reflowed.",
        "success"
      );
      setReflow(null);
      reload();
      bump();
    } catch (err) {
      notify(errorMessage(err), "error");
    } finally {
      setReflowBusy(false);
    }
  };

  const completeTopic = async () => {
    if (!data?.card.trackId) return;
    try {
      await api.post(`/tracks/${data.card.trackId}/dsa/complete-topic`, { topic: data.card.currentDsaTopic });
      notify("Topic marked complete. The next topic is now unlocked.", "success");
      setTopicModal(false);
      reload();
      bump();
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  const generate = async () => {
    if (!data) return;
    setGenerating(true);
    setGenerateError(null);
    try {
      await api.post(`/skills/${data.card.id}/generate-curriculum`, { days: 5 });
      notify("Mini-curriculum generated into the curriculum folder.", "success");
      reload();
      bump();
    } catch (err) {
      setGenerateError(errorMessage(err));
    } finally {
      setGenerating(false);
    }
  };

  if (loading && !data) return <div className="page"><Loading label="Loading skill…" /></div>;
  if (error || !data) return <div className="page"><ErrorNote>{error ?? "Skill not found."}</ErrorNote></div>;

  const { card, lessons, dsaTopics } = data;
  const active = card.trackState === "active";
  const paused = card.trackState === "paused";
  const completed = card.trackState === "completed";
  const lessonsDone = lessons.filter((lesson) => lesson.status === "completed").length;

  return (
    <div className="page">
      <Link to="/skills" className="text-link back-link">
        <Icon name="arrowLeft" size={14} /> All skills
      </Link>

      <header className="skill-detail-head">
        <div className="skill-detail-identity">
          <span className="skill-glyph large">{card.name.slice(0, 2).toUpperCase()}</span>
          <div>
            <span className="eyebrow">{card.area}</span>
            <h1>{card.name}</h1>
            <p className="page-subtitle">{card.description || "No description yet."}</p>
          </div>
        </div>
        <div className="skill-detail-actions">
          {active ? (
            <Button variant="secondary" onClick={() => previewState("pause")}>Pause track</Button>
          ) : (
            <Button variant="primary" onClick={() => previewState(paused || completed ? "resume" : "start")}>
              {paused ? "Resume track" : completed ? "Study again" : "Start studying"}
            </Button>
          )}
          <Button variant="ghost" icon="plus" onClick={() => setQuestionOpen(true)}>
            Add question
          </Button>
        </div>
      </header>

      <div className="detail-grid">
        <Card className="detail-metrics">
          <div className="detail-metric">
            <span className="metric-label">Track</span>
            <Badge tone={active ? "accent" : paused ? "warn" : completed ? "success" : "muted"}>
              {card.trackState}
            </Badge>
          </div>
          <div className="detail-metric">
            <span className="metric-label">Lessons</span>
            <span className="metric-value">{card.lessonsDone}<small> / {card.lessonsTotal}</small></span>
          </div>
          <div className="detail-metric">
            <span className="metric-label">Confidence</span>
            <ConfidenceMeter score={card.confidence} />
          </div>
          <div className="detail-metric">
            <span className="metric-label">Last practised</span>
            <span className="metric-value small">{card.lastPractised ? formatDate(card.lastPractised) : "Not yet"}</span>
          </div>
        </Card>
        <Card className="detail-confidence">
          <div className="card-heading">
            <h3>Confidence over time</h3>
            <span className="muted small">decays without practice</span>
          </div>
          <Sparkline values={data.confidenceHistory.map((point) => point.score)} width={320} height={58} />
          <div className="confidence-factors">
            <span>Self {card.selfRating?.toFixed(1) ?? "—"}</span>
            <span>Accuracy {card.accuracy !== null && card.accuracy !== undefined ? `${Math.round(card.accuracy * 100)}%` : "—"}</span>
            <span>Tasks {card.taskScore !== null && card.taskScore !== undefined ? `${Math.round(card.taskScore * 100)}%` : "—"}</span>
            <span>{card.daysSincePractice}d since practice</span>
          </div>
          {card.needsRefresh ? (
            <InfoNote tone="warn">
              This skill has decayed below a comfortable level. A short refresher is suggested — open the most recent lesson or review.
            </InfoNote>
          ) : null}
        </Card>
      </div>

      {card.weakSubtopics.length > 0 ? (
        <Card>
          <div className="card-heading">
            <h3>Weak sub-topics</h3>
            <span className="muted small">from your answer history</span>
          </div>
          <div className="weak-grid">
            {card.weakSubtopics.map((topic) => (
              <div key={topic.name} className="weak-item">
                <span>{topic.name}</span>
                <ProgressBar value={topic.accuracy * 100} tone="gold" small />
                <span className="muted small">{Math.round(topic.accuracy * 100)}% over {topic.attempts}</span>
              </div>
            ))}
          </div>
        </Card>
      ) : null}

      {card.isDsa && dsaTopics.length > 0 ? (
        <section>
          <div className="section-heading">
            <h2>DSA sequence</h2>
            <span className="muted small">one topic at a time, in order</span>
          </div>
          <div className="topic-list">
            {dsaTopics.map((topic, index) => {
              const isCurrent = card.currentDsaTopic === topic.name;
              return (
                <div
                  className={`topic-row ${topic.completed ? "completed" : ""} ${isCurrent ? "current" : ""}`}
                  key={topic.name}
                >
                  <span className="topic-index">{topic.completed ? <Icon name="check" size={13} /> : index + 1}</span>
                  <span className="topic-name">{topic.name.replace(/-/g, " ")}</span>
                  <span className="topic-progress">
                    {topic.lessons === 0 ? "no lessons yet" : `${topic.lessonsDone}/${topic.lessons} lessons`}
                  </span>
                  {isCurrent ? <Badge tone="accent">Current</Badge> : topic.completed ? <Badge tone="success">Complete</Badge> : <Badge tone="muted">Later</Badge>}
                </div>
              );
            })}
          </div>
          {card.currentDsaTopic ? (
            <div className="topic-override">
              <span className="muted small">
                Stuck on <strong>{card.currentDsaTopic.replace(/-/g, " ")}</strong>? You can override the strict sequence.
              </span>
              <Button variant="ghost" size="sm" onClick={() => setTopicModal(true)}>
                Skip current topic
              </Button>
            </div>
          ) : null}
        </section>
      ) : null}

      <section>
        <div className="section-heading">
          <h2>Lessons</h2>
          <span className="muted small">{lessonsDone} completed</span>
        </div>
        {lessons.length === 0 ? (
          <EmptyState
            title="No lessons yet"
            icon="book"
            action={
              <Button variant="primary" icon="sparkles" onClick={generate} loading={generating}>
                Generate a 5-day mini-curriculum
              </Button>
            }
          >
            Write lesson JSON files in <code>curriculum/{card.id}/</code>, or let AI draft a small editable sequence sized to a time box.
            {generateError ? <ErrorNote>{generateError}</ErrorNote> : null}
          </EmptyState>
        ) : (
          <div className="lesson-list">
            {lessons.map((lesson) => {
              const status =
                lesson.status === "completed"
                  ? "Completed"
                  : lesson.status === "inprogress"
                    ? "In progress"
                    : lesson.locked
                      ? "Locked"
                      : "Not started";
              const tone =
                lesson.status === "completed"
                  ? "success"
                  : lesson.status === "inprogress"
                    ? "accent"
                    : lesson.locked
                      ? "muted"
                      : "neutral";
              return (
                <div className={`lesson-row ${lesson.locked ? "locked" : ""}`} key={lesson.id}>
                  <span className="lesson-number">{String(lesson.order).padStart(2, "0")}</span>
                  <div className="lesson-row-copy">
                    <div className="lesson-row-title">
                      <strong>{lesson.title}</strong>
                      <Badge tone={tone as "success" | "accent" | "muted" | "neutral"}>{status}</Badge>
                    </div>
                    <span className="muted small">
                      {lesson.topic ? `${lesson.topic.replace(/-/g, " ")} · ` : ""}
                      {minutesLabel(lesson.estimatedMinutes)}
                      {lesson.confidence ? ` · confidence ${lesson.confidence}/5` : ""}
                    </span>
                  </div>
                  {lesson.locked ? (
                    <span className="muted small">finish the current topic first</span>
                  ) : (
                    <Link to={`/lesson/${lesson.id}`} className="btn btn-ghost btn-sm">
                      Open <Icon name="arrowRight" size={14} />
                    </Link>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </section>

      <div className="detail-columns">
        <Card>
          <div className="card-heading">
            <h3>Notes</h3>
            <Link to={`/notes?skill=${card.id}&new=1`} className="text-link">Add note</Link>
          </div>
          {data.notes.length === 0 ? (
            <p className="muted small">No notes for this skill yet.</p>
          ) : (
            <ul className="note-list compact">
              {data.notes.map((note) => (
                <li key={note.id}>
                  <Link to={`/notes?note=${note.id}`}>
                    <strong>{note.title}</strong>
                    <span className="muted small">{formatDate(note.updatedAt)}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Card>
        <Card>
          <div className="card-heading">
            <h3>Resources</h3>
            <button
              className="text-link"
              onClick={() => openResources({ skillId: card.id, skillName: card.name })}
              type="button"
            >
              Open drawer
            </button>
          </div>
          {data.resources.length === 0 ? (
            <p className="muted small">No resources attached to this skill yet.</p>
          ) : (
            <ul className="resource-list compact">
              {data.resources.map((resource) => (
                <li key={resource.id}>
                  <a href={resource.url} target="_blank" rel="noreferrer noopener">
                    <span>{resource.title}</span>
                    <Icon name="external" size={13} />
                  </a>
                  <span className="muted small">{resource.status}</span>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>

      <ReflowModal
        open={Boolean(reflow)}
        title={reflow?.action === "pause" ? "Pause this track?" : reflow?.action === "resume" ? "Resume this track?" : "Start this track?"}
        description="The plan will reflow. These are the projected dates before anything is applied."
        before={reflow?.before}
        after={reflow?.after ?? null}
        onCancel={() => setReflow(null)}
        onConfirm={applyState}
        busy={reflowBusy}
        confirmLabel={reflow?.action === "pause" ? "Pause and reflow" : "Apply and reflow"}
      />

      <Modal
        open={topicModal}
        title="Skip the current DSA topic"
        onClose={() => setTopicModal(false)}
        footer={
          <>
            <Button variant="ghost" onClick={() => setTopicModal(false)}>Cancel</Button>
            <Button variant="primary" onClick={completeTopic}>Mark topic complete</Button>
          </>
        }
      >
        <p>
          This marks <strong>{card.currentDsaTopic?.replace(/-/g, " ")}</strong> complete and unlocks the next topic. The lesson itself
          stays available if you want to return to it later.
        </p>
        <InfoNote tone="warn">
          The default sequence is strict for a reason. Override it when you genuinely already know the material.
        </InfoNote>
      </Modal>

      <CustomQuestionModal
        open={questionOpen}
        onClose={() => setQuestionOpen(false)}
        defaultSkillId={card.id}
        onCreated={reload}
      />
    </div>
  );
}
