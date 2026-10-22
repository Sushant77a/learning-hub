import { useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api, errorMessage } from "../api";
import type { AiGrade, LessonDetail, LessonQuestion, Question } from "../api/types";
import { useApp } from "../context/AppContext";
import { useApi } from "../hooks/useApi";
import { Markdown, CodeBlock } from "../components/Markdown";
import { QuestionCard } from "../components/QuestionCard";
import { PracticeSection } from "../components/PracticeSection";
import { Badge, Button, ErrorNote, Field, InfoNote, Loading, TextArea, TextInput } from "../components/ui";
import { Icon } from "../components/icons";

type StepKey = "warmup" | "theory" | "worked" | "check" | "explain" | "task" | "bugfix" | "wrapup";

interface Step {
  key: StepKey;
  label: string;
  short: string;
}

export function LessonPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { notify, bump, settings, setResourceContext, openResources } = useApp();
  const { data, loading, error, reload } = useApi<LessonDetail>(id ? `/lessons/${id}` : null);
  const [current, setCurrent] = useState(0);
  const [maxReached, setMaxReached] = useState(0);
  const [initialized, setInitialized] = useState(false);
  const [checkAnswered, setCheckAnswered] = useState<number[]>([]);
  const [explainText, setExplainText] = useState("");
  const [explainSaved, setExplainSaved] = useState(false);
  const [grade, setGrade] = useState<AiGrade | null>(null);
  const [confidence, setConfidence] = useState(3);
  const [learnedNote, setLearnedNote] = useState("");
  const [practiceOpen, setPracticeOpen] = useState(false);
  const [finishing, setFinishing] = useState(false);
  const [finished, setFinished] = useState<{ message: string; nextLessonDate?: string | null } | null>(null);
  const started = useRef(false);

  const steps = useMemo<Step[]>(() => {
    if (!data) return [];
    const result: Step[] = [];
    if (data.warmup.length > 0) result.push({ key: "warmup", label: "Recall warm-up", short: "Warm-up" });
    result.push({ key: "theory", label: "Theory", short: "Theory" });
    if (data.workedExample) result.push({ key: "worked", label: "Worked example", short: "Example" });
    if (data.questions.length > 0) result.push({ key: "check", label: "Check your understanding", short: "Check" });
    result.push({ key: "explain", label: "Explain it back", short: "Explain" });
    if (data.task) result.push({ key: "task", label: "Practice task", short: "Task" });
    if (data.bugFix && data.includeBugFix) result.push({ key: "bugfix", label: "Fix the bug", short: "Debug" });
    result.push({ key: "wrapup", label: "Wrap-up", short: "Wrap-up" });
    return result;
  }, [data]);

  useEffect(() => {
    if (!data || initialized) return;
    const initial = Math.min(Math.max(0, data.progress.currentStep - 1), Math.max(0, steps.length - 1));
    setCurrent(initial);
    setMaxReached(initial);
    setExplainText(data.progress.explainBack ?? "");
    setExplainSaved(Boolean(data.progress.explainBack));
    setLearnedNote(data.progress.learnedNote ?? "");
    setConfidence(data.progress.confidenceRating ?? 3);
    if (data.progress.explainGrade) {
      try {
        setGrade(JSON.parse(data.progress.explainGrade) as AiGrade);
      } catch {
        // Stored grade could not be parsed; leave it blank.
      }
    }
    setInitialized(true);
  }, [data, initialized, steps.length]);

  useEffect(() => {
    if (!data) return;
    setResourceContext({
      lessonId: data.id,
      lessonTitle: data.title,
      skillId: data.skillId,
      skillName: data.skillName
    });
  }, [data, setResourceContext]);

  useEffect(() => {
    if (!data || started.current) return;
    if (data.track.trackId && data.progress.status !== "completed") {
      started.current = true;
      api
        .post(`/lessons/${data.id}/start`, { trackId: data.track.trackId })
        .catch(() => undefined);
    }
  }, [data]);

  const step = steps[current];

  const goTo = (index: number) => {
    if (index < 0 || index > maxReached) return;
    setCurrent(index);
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const saveStep = (index: number) => {
    if (!data?.track.trackId) return;
    void api
      .post(`/lessons/${data.id}/step`, { trackId: data.track.trackId, step: index + 1 })
      .catch(() => undefined);
  };

  const next = async () => {
    if (!data || !step) return;
    if (step.key === "check" && checkAnswered.length < data.questions.length) {
      notify("Answer every check question before moving on.", "error");
      return;
    }
    if (step.key === "explain" && !explainText.trim()) {
      notify("Write a short explanation before moving on.", "error");
      return;
    }
    if (step.key === "explain" && !explainSaved) {
      try {
        await api.post(`/lessons/${data.id}/explain`, {
          trackId: data.track.trackId,
          text: explainText
        });
        setExplainSaved(true);
      } catch (err) {
        notify(errorMessage(err), "error");
        return;
      }
    }

    const nextIndex = current + 1;
    saveStep(nextIndex);
    setCurrent(nextIndex);
    setMaxReached((value) => Math.max(value, nextIndex));
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const finish = async () => {
    if (!data?.track.trackId) {
      notify("Start a track for this skill before completing the lesson.", "error");
      return;
    }
    setFinishing(true);
    try {
      const result = await api.post<{ message: string; nextLessonDate?: string | null; skillCompleted: boolean }>(
        `/lessons/${data.id}/complete`,
        {
          trackId: data.track.trackId,
          confidence,
          learnedNote
        }
      );
      setFinished({ message: result.message, nextLessonDate: result.nextLessonDate });
      notify("Lesson complete.", "success");
      bump();
      reload();
    } catch (err) {
      notify(errorMessage(err), "error");
    } finally {
      setFinishing(false);
    }
  };

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (!event.altKey) return;
      const target = event.target as HTMLElement | null;
      if (target && ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName)) return;
      if (event.key === "ArrowRight") {
        event.preventDefault();
        void next();
      } else if (event.key === "ArrowLeft") {
        event.preventDefault();
        goTo(current - 1);
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  });

  if (loading && !data) {
    return (
      <div className="page">
        <Loading label="Opening the lesson…" />
      </div>
    );
  }
  if (error || !data) {
    return (
      <div className="page">
        <ErrorNote>{error ?? "This lesson could not be opened."}</ErrorNote>
        <Link to="/" className="text-link">
          Back to Today
        </Link>
      </div>
    );
  }

  const completed = data.progress.status === "completed" || finished !== null;
  const progressPercent = steps.length > 0 ? ((current + (completed ? 1 : 0)) / steps.length) * 100 : 0;

  if (finished) {
    return (
      <div className="page lesson-finish">
        <div className="finish-mark">
          <Icon name="check" size={28} />
        </div>
        <span className="eyebrow">{data.skillName}</span>
        <h1>Lesson complete.</h1>
        <p className="page-subtitle">{finished.message}</p>
        <div className="finish-note">
          <span className="muted small">What you learned</span>
          <p>{learnedNote || "You chose not to add a note."}</p>
          <span className="confidence-static">Confidence {confidence} / 5</span>
        </div>
        <div className="button-row">
          <Link to="/" className="btn btn-primary btn-md">
            Back to Today
          </Link>
          {data.nextLessonId ? (
            <Link to={`/lesson/${data.nextLessonId}`} className="btn btn-secondary btn-md">
              Look at the next lesson
            </Link>
          ) : null}
        </div>
      </div>
    );
  }

  if (!data.track.trackId) {
    return (
      <div className="page">
        <div className="lesson-simple-head">
          <span className="eyebrow">
            {data.skillName} · lesson {data.order}
          </span>
          <h1>{data.title}</h1>
          <p className="page-subtitle">{data.summary}</p>
        </div>
        <InfoNote tone="warn">
          This skill does not have an active track yet. Start it from the skill page so the lesson can be scheduled and saved.
        </InfoNote>
        <Link to={`/skills/${data.skillId}`} className="btn btn-primary btn-md">
          Open {data.skillName}
        </Link>
      </div>
    );
  }

  return (
    <div className="lesson-shell">
      <div className="lesson-progress-mobile">
        <div className="progress" aria-hidden="true">
          <span style={{ width: `${progressPercent}%` }} />
        </div>
        <span className="muted small">
          {step?.short} · {current + 1} of {steps.length}
        </span>
      </div>

      <aside className="lesson-rail" aria-label="Lesson steps">
        <Link to="/" className="lesson-back">
          <Icon name="arrowLeft" size={15} /> Today
        </Link>
        <div className="rail-skill">
          <span className="eyebrow">{data.skillName}</span>
          <span className="muted small">{data.estimatedMinutes} min read</span>
        </div>
        <ol className="stepper">
          {steps.map((item, index) => (
            <li key={item.key}>
              <button
                className={`step ${index === current ? "current" : ""} ${index < current ? "done" : ""} ${
                  index > maxReached ? "locked" : ""
                }`}
                onClick={() => goTo(index)}
                disabled={index > maxReached}
                aria-current={index === current ? "step" : undefined}
              >
                <span className="step-number">{index < current ? <Icon name="check" size={13} /> : index + 1}</span>
                <span className="step-label">{item.label}</span>
              </button>
            </li>
          ))}
        </ol>
        <div className="rail-foot">
          {settings?.ai.configured ? (
            <span className="ai-status">
              <span className="status-dot on" /> AI help available
            </span>
          ) : (
            <span className="ai-status muted">
              <span className="status-dot" /> AI off · lesson works fully
            </span>
          )}
          <button
            className="text-link rail-resource"
            onClick={() =>
              openResources({
                lessonId: data.id,
                lessonTitle: data.title,
                skillId: data.skillId,
                skillName: data.skillName
              })
            }
            type="button"
          >
            <Icon name="resource" size={14} /> Resources
          </button>
        </div>
      </aside>

      <article className="lesson-main">
        <header className="lesson-head">
          <div className="lesson-head-meta">
            <span className="eyebrow">
              {data.topic ? data.topic.replace(/-/g, " ") : data.skillName} · lesson {data.order}
            </span>
            {data.progress.status === "completed" ? <Badge tone="success">Completed</Badge> : null}
            {data.isDsa ? <Badge tone="accent">DSA</Badge> : null}
          </div>
          <h1>{data.title}</h1>
          <p className="lesson-summary">{data.summary}</p>
          <div className="lesson-head-actions">
            <button
              className="btn btn-ghost btn-sm"
              type="button"
              onClick={() =>
                openResources({
                  lessonId: data.id,
                  lessonTitle: data.title,
                  skillId: data.skillId,
                  skillName: data.skillName
                })
              }
            >
              <Icon name="resource" size={15} /> Resources
            </button>
            <Link to={`/notes?lesson=${data.id}&skill=${data.skillId}&new=1`} className="btn btn-ghost btn-sm">
              <Icon name="notes" size={15} /> Add a note
            </Link>
          </div>
        </header>

        <div className="lesson-step-head">
          <span className="step-index">
            {current + 1} / {steps.length}
          </span>
          <h2>{step?.label}</h2>
        </div>

        {step?.key === "warmup" ? (
          <WarmupStep questions={data.warmup} trackId={data.track.trackId} />
        ) : null}

        {step?.key === "theory" ? (
          <TheoryStep
            data={data}
            practiceOpen={practiceOpen}
            onTogglePractice={() => setPracticeOpen((open) => !open)}
          />
        ) : null}

        {step?.key === "worked" && data.workedExample ? (
          <section className="step-body">
            <h3 className="subhead">{data.workedExample.title}</h3>
            <Markdown>{data.workedExample.markdown}</Markdown>
            <ol className="worked-steps">
              {data.workedExample.steps.map((item, index) => (
                <li key={index}>
                  <span className="worked-number">{index + 1}</span>
                  <div>
                    <Markdown>{item}</Markdown>
                  </div>
                </li>
              ))}
            </ol>
          </section>
        ) : null}

        {step?.key === "check" ? (
          <CheckStep
            questions={data.questions}
            trackId={data.track.trackId}
            answered={checkAnswered}
            onAnswered={(questionId) => setCheckAnswered((current) => [...new Set([...current, questionId])])}
            onMorePractice={() => void morePractice(data, reload, notify)}
          />
        ) : null}

        {step?.key === "explain" ? (
          <ExplainStep
            data={data}
            text={explainText}
            setText={setExplainText}
            saved={explainSaved}
            setSaved={setExplainSaved}
            grade={grade}
            setGrade={setGrade}
          />
        ) : null}

        {step?.key === "task" && data.task ? (
          <TaskStep data={data} />
        ) : null}

        {step?.key === "bugfix" && data.bugFix ? (
          <BugFixStep data={data} />
        ) : null}

        {step?.key === "wrapup" ? (
          <WrapUpStep
            data={data}
            note={learnedNote}
            setNote={setLearnedNote}
            confidence={confidence}
            setConfidence={setConfidence}
          />
        ) : null}

        <div className="lesson-nav">
          <Button variant="ghost" onClick={() => goTo(current - 1)} disabled={current === 0}>
            <Icon name="arrowLeft" size={16} /> Back
          </Button>
          <span className="muted small lesson-nav-hint">Alt + ← / → to move</span>
          {step?.key === "wrapup" ? (
            <Button variant="primary" size="lg" onClick={finish} loading={finishing} icon="check">
              Finish lesson
            </Button>
          ) : (
            <Button variant="primary" size="lg" onClick={() => void next()}>
              {step?.key === "check" && checkAnswered.length < data.questions.length
                ? `Answer ${data.questions.length - checkAnswered.length} more`
                : "Continue"}
              <Icon name="arrowRight" size={16} />
            </Button>
          )}
        </div>
      </article>
    </div>
  );
}

async function morePractice(data: LessonDetail, reload: () => void, notify: (message: string, kind?: "info" | "success" | "error") => void) {
  try {
    await api.post(`/ai/practice`, { lessonId: data.id });
    notify("Three practice questions added to this lesson.", "success");
    reload();
  } catch (err) {
    notify(errorMessage(err), "error");
  }
}

function WarmupStep({ questions, trackId }: { questions: Question[]; trackId: number }) {
  return (
    <section className="step-body">
      <p className="step-lead">
        A short recall before new material. These questions mix earlier lessons and, at a low rate, skills you have paused.
      </p>
      <div className="question-stack">
        {questions.map((question, index) => (
          <QuestionCard
            key={question.id}
            question={question}
            trackId={trackId}
            index={index}
            total={questions.length}
          />
        ))}
      </div>
    </section>
  );
}

function TheoryStep({
  data,
  practiceOpen,
  onTogglePractice
}: {
  data: LessonDetail;
  practiceOpen: boolean;
  onTogglePractice: () => void;
}) {
  const { settings, notify } = useApp();
  const [output, setOutput] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [aiError, setAiError] = useState<string | null>(null);

  const explainDifferently = async () => {
    setBusy(true);
    setAiError(null);
    try {
      const response = await api.post<{ text: string }>("/ai/explain", { lessonId: data.id });
      setOutput(response.text);
    } catch (err) {
      const message = errorMessage(err);
      setAiError(message);
      notify(message, "error");
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="step-body reading">
      <Markdown>{data.theory}</Markdown>

      <div className="ai-inline">
        <div>
          <strong>Stuck on the framing?</strong>
          <span className="muted small">
            {settings?.ai.configured
              ? "Ask for a different explanation, using this lesson as context."
              : "AI is off. The lesson is complete without it."}
          </span>
        </div>
        <Button variant="secondary" size="sm" icon="sparkles" onClick={explainDifferently} loading={busy}>
          Explain differently
        </Button>
      </div>
      {aiError ? <ErrorNote>{aiError}</ErrorNote> : null}
      {output ? (
        <div className="ai-panel">
          <div className="ai-panel-head">
            <span><Icon name="sparkles" size={15} /> A different angle</span>
            <button className="icon-btn small" onClick={() => setOutput(null)} aria-label="Dismiss explanation">
              <Icon name="close" size={14} />
            </button>
          </div>
          <Markdown>{output}</Markdown>
        </div>
      ) : null}

      {data.isDsa && data.practice.length > 0 ? (
        <PracticeSection problems={data.practice} open={practiceOpen} onToggle={onTogglePractice} />
      ) : null}
    </section>
  );
}

function CheckStep({
  questions,
  trackId,
  answered,
  onAnswered,
  onMorePractice
}: {
  questions: LessonQuestion[];
  trackId: number;
  answered: number[];
  onAnswered: (questionId: number) => void;
  onMorePractice: () => void;
}) {
  const checkQuestions = questions.filter((question) => question.kind !== "complexity");
  const complexityQuestions = questions.filter((question) => question.kind === "complexity");
  const ordered = [...checkQuestions, ...complexityQuestions];

  return (
    <section className="step-body">
      <p className="step-lead">
        Answer first, then read the explanation — including why the other options are wrong. Wrong answers return later in warm-ups.
      </p>
      <div className="question-stack">
        {ordered.map((question, index) => (
          <QuestionCard
            key={question.id}
            question={question}
            trackId={trackId}
            index={index}
            total={ordered.length}
            onAnswered={() => onAnswered(question.id)}
          />
        ))}
      </div>
      <div className="check-summary">
        <span className="muted small">
          {answered.length} of {ordered.length} answered
        </span>
        <Button variant="ghost" size="sm" icon="sparkles" onClick={onMorePractice}>
          More practice questions
        </Button>
      </div>
    </section>
  );
}

function ExplainStep({
  data,
  text,
  setText,
  saved,
  setSaved,
  grade,
  setGrade
}: {
  data: LessonDetail;
  text: string;
  setText: (value: string) => void;
  saved: boolean;
  setSaved: (value: boolean) => void;
  grade: AiGrade | null;
  setGrade: (value: AiGrade) => void;
}) {
  const { notify, settings } = useApp();
  const [saving, setSaving] = useState(false);
  const [grading, setGrading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const save = async () => {
    if (!text.trim()) {
      setError("Write a short explanation before saving.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await api.post(`/lessons/${data.id}/explain`, { trackId: data.track.trackId, text: text.trim() });
      setSaved(true);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const gradeAnswer = async () => {
    if (!text.trim()) {
      setError("Write your explanation first.");
      return;
    }
    setGrading(true);
    setError(null);
    try {
      await api.post(`/lessons/${data.id}/explain`, { trackId: data.track.trackId, text: text.trim() });
      setSaved(true);
      const result = await api.post<AiGrade>("/ai/grade", {
        lessonId: data.id,
        trackId: data.track.trackId,
        answer: text.trim()
      });
      setGrade(result);
      notify(result.verdict || "Explanation graded.", "success");
    } catch (err) {
      const message = errorMessage(err);
      setError(message);
      notify(message, "error");
    } finally {
      setGrading(false);
    }
  };

  return (
    <section className="step-body">
      <div className="explain-prompt">
        <p>{data.explainPrompt}</p>
        <span className="muted small">Write it as if teaching someone else. Short is fine; precise is better.</span>
      </div>
      <TextArea
        rows={7}
        value={text}
        onChange={(event) => {
          setText(event.target.value);
          setSaved(false);
        }}
        placeholder="The core idea is… It matters because… A common mistake is…"
        aria-label="Your explanation"
        className="reading-textarea"
      />
      <div className="explain-actions">
        <Button variant="secondary" size="sm" onClick={save} loading={saving} disabled={!text.trim()}>
          {saved ? "Saved" : "Save explanation"}
        </Button>
        <Button
          variant="primary"
          size="sm"
          icon="sparkles"
          onClick={gradeAnswer}
          loading={grading}
          disabled={!text.trim()}
        >
          Grade my answer
        </Button>
        <span className="muted small">
          {saved ? "Saved — you can continue." : "Saving is enough to continue."}
          {!settings?.ai.configured ? " AI grading is off; your answer is still saved." : ""}
        </span>
      </div>
      {error ? <ErrorNote>{error}</ErrorNote> : null}

      {grade ? (
        <div className="grade-panel">
          <header>
            <span className="grade-score">{grade.score}<small>/5</small></span>
            <div>
              <strong>{grade.verdict || "Feedback"}</strong>
              <span className="muted small">AI feedback on your explanation</span>
            </div>
          </header>
          <div className="grade-columns">
            {grade.strengths.length > 0 ? (
              <div>
                <h4>What landed</h4>
                <ul>
                  {grade.strengths.map((item, index) => <li key={index}>{item}</li>)}
                </ul>
              </div>
            ) : null}
            {grade.gaps.length > 0 ? (
              <div>
                <h4>Gaps</h4>
                <ul>
                  {grade.gaps.map((item, index) => <li key={index}>{item}</li>)}
                </ul>
              </div>
            ) : null}
          </div>
          {grade.review.length > 0 ? (
            <div className="grade-review">
              <h4>Review next</h4>
              <ul>
                {grade.review.map((item, index) => <li key={index}>{item}</li>)}
              </ul>
            </div>
          ) : null}
        </div>
      ) : null}
    </section>
  );
}

function TaskStep({ data }: { data: LessonDetail }) {
  const { notify } = useApp();
  const [code, setCode] = useState(data.task?.starterCode ?? "");
  const [problem, setProblem] = useState("");
  const [logged, setLogged] = useState(data.progress.taskDone);
  const [busy, setBusy] = useState(false);
  const [aiBusy, setAiBusy] = useState(false);
  const [aiText, setAiText] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [reason, setReason] = useState("Got stuck");

  const log = async (passed: boolean) => {
    setBusy(true);
    setError(null);
    try {
      await api.post(`/lessons/${data.id}/task`, {
        trackId: data.track.trackId,
        passed,
        reasonTag: passed ? null : reason,
        note: passed ? null : problem || null
      });
      setLogged(true);
      notify(passed ? "Task logged as complete." : "Logged. The mistake journal will remember the sticking point.", "success");
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const askWhy = async () => {
    setAiBusy(true);
    setError(null);
    try {
      const response = await api.post<{ text: string }>("/ai/debug", {
        lessonId: data.id,
        code,
        error: problem || "I am not sure what is wrong."
      });
      setAiText(response.text);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setAiBusy(false);
    }
  };

  if (!data.task) return null;

  return (
    <section className="step-body">
      <div className="task-card">
        <header>
          <Badge tone="accent">Task</Badge>
          {logged ? <Badge tone="success">Logged</Badge> : null}
          <h3>{data.task.title}</h3>
        </header>
        <Markdown>{data.task.prompt}</Markdown>
        {data.task.requirements.length > 0 ? (
          <div className="requirements">
            <h4>Done means</h4>
            <ul>
              {data.task.requirements.map((item) => <li key={item}>{item}</li>)}
            </ul>
          </div>
        ) : null}
        <div className="code-editor">
          <div className="code-editor-head">
            <span>C#</span>
            <span className="muted small">A scratchpad — Learning Hub does not execute code</span>
          </div>
          <textarea
            value={code}
            onChange={(event) => setCode(event.target.value)}
            spellCheck={false}
            aria-label="Your task code"
          />
        </div>
        <Field label="What is going wrong? (optional)" htmlFor="task-problem">
          <TextInput
            id="task-problem"
            value={problem}
            onChange={(event) => setProblem(event.target.value)}
            placeholder="Compile error, wrong output, or what confused you"
          />
        </Field>
      </div>

      <div className="ai-inline">
        <div>
          <strong>Why is my code wrong?</strong>
          <span className="muted small">AI reads your snippet and the lesson, then points at the rule you missed.</span>
        </div>
        <Button variant="secondary" size="sm" icon="sparkles" onClick={askWhy} loading={aiBusy} disabled={!code.trim()}>
          Ask about my code
        </Button>
      </div>
      {error ? <ErrorNote>{error}</ErrorNote> : null}
      {aiText ? (
        <div className="ai-panel">
          <div className="ai-panel-head">
            <span><Icon name="sparkles" size={15} /> Debugging help</span>
            <button className="icon-btn small" onClick={() => setAiText(null)} aria-label="Dismiss">
              <Icon name="close" size={14} />
            </button>
          </div>
          <Markdown>{aiText}</Markdown>
        </div>
      ) : null}

      <div className="task-actions">
        <Button variant="primary" onClick={() => log(true)} loading={busy}>
          I completed it
        </Button>
        <div className="stuck-actions">
          <TextInput
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            aria-label="Reason for not finishing"
            placeholder="What blocked you?"
          />
          <Button variant="ghost" onClick={() => log(false)} loading={busy}>
            I'm stuck
          </Button>
        </div>
      </div>
    </section>
  );
}

function BugFixStep({ data }: { data: LessonDetail }) {
  const { notify } = useApp();
  const [explanation, setExplanation] = useState("");
  const [revealed, setRevealed] = useState<{ explanation?: string; title?: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const [aiBusy, setAiBusy] = useState(false);
  const [aiText, setAiText] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [logged, setLogged] = useState(data.progress.bugFixDone);

  const submit = async (solved: boolean) => {
    setBusy(true);
    setError(null);
    try {
      await api.post(`/lessons/${data.id}/bugfix`, {
        trackId: data.track.trackId,
        solved,
        explanation
      });
      setLogged(true);
      const content = await api.get<{ explanation?: string; title?: string }>(`/lessons/${data.id}/bugfix/explanation`);
      setRevealed(content);
      if (!solved) notify("Logged as a debugging mistake.", "success");
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const askAi = async () => {
    setAiBusy(true);
    setError(null);
    try {
      const response = await api.post<{ text: string }>("/ai/debug", {
        lessonId: data.id,
        code: data.bugFix?.brokenCode ?? "",
        error: explanation || "Something in this code is broken."
      });
      setAiText(response.text);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setAiBusy(false);
    }
  };

  if (!data.bugFix) return null;

  return (
    <section className="step-body">
      <p className="step-lead">
        Sometimes the useful skill is finding the flaw. This one is optional — the explanation is shown after you commit to an answer.
      </p>
      <div className="task-card">
        <header>
          <Badge tone="warn">Fix the bug</Badge>
          {logged ? <Badge tone="success">Attempt logged</Badge> : null}
          <h3>{data.bugFix.title}</h3>
        </header>
        <Markdown>{data.bugFix.prompt}</Markdown>
        <CodeBlock code={data.bugFix.brokenCode} language={data.bugFix.language} />
        <Field label="Your diagnosis" htmlFor="bugfix-explanation" hint="Name the rule that is violated, not only the line.">
          <TextArea
            id="bugfix-explanation"
            rows={4}
            value={explanation}
            onChange={(event) => setExplanation(event.target.value)}
            placeholder="This fails because…"
          />
        </Field>
      </div>

      <div className="ai-inline">
        <div>
          <strong>Want a nudge?</strong>
          <span className="muted small">AI can point at the bug without rewriting your program.</span>
        </div>
        <Button variant="secondary" size="sm" icon="sparkles" onClick={askAi} loading={aiBusy} disabled={!explanation.trim()}>
          Ask for a hint
        </Button>
      </div>
      {error ? <ErrorNote>{error}</ErrorNote> : null}
      {aiText ? (
        <div className="ai-panel">
          <div className="ai-panel-head">
            <span><Icon name="sparkles" size={15} /> Hint</span>
            <button className="icon-btn small" onClick={() => setAiText(null)} aria-label="Dismiss">
              <Icon name="close" size={14} />
            </button>
          </div>
          <Markdown>{aiText}</Markdown>
        </div>
      ) : null}

      {revealed ? (
        <div className="reveal-panel">
          <h4>{revealed.title ?? "The explanation"}</h4>
          <p>{revealed.explanation}</p>
        </div>
      ) : (
        <div className="button-row">
          <Button variant="primary" onClick={() => submit(true)} loading={busy} disabled={!explanation.trim()}>
            I found it
          </Button>
          <Button variant="ghost" onClick={() => submit(false)} loading={busy}>
            Show me why
          </Button>
        </div>
      )}
    </section>
  );
}

function WrapUpStep({
  data,
  note,
  setNote,
  confidence,
  setConfidence
}: {
  data: LessonDetail;
  note: string;
  setNote: (value: string) => void;
  confidence: number;
  setConfidence: (value: number) => void;
}) {
  const labels = ["Shaky", "Fragile", "Okay", "Solid", "Secure"];
  return (
    <section className="step-body">
      <div className="wrapup">
        <div className="wrapup-field">
          <label htmlFor="learned-note">One line: what did you actually learn?</label>
          <TextInput
            id="learned-note"
            value={note}
            onChange={(event) => setNote(event.target.value)}
            placeholder="e.g. A string is a reference type, but it is immutable."
            maxLength={200}
          />
          <span className="field-hint">This appears on your Today card and in the lesson history. One honest sentence.</span>
        </div>

        <div className="confidence-picker">
          <span className="field-label">How confident do you feel?</span>
          <div className="confidence-options" role="radiogroup" aria-label="Confidence rating">
            {[1, 2, 3, 4, 5].map((value) => (
              <button
                key={value}
                type="button"
                role="radio"
                aria-checked={confidence === value}
                className={confidence === value ? "on" : ""}
                onClick={() => setConfidence(value)}
              >
                <span className="confidence-number">{value}</span>
                <span>{labels[value - 1]}</span>
              </button>
            ))}
          </div>
          <span className="field-hint">This feeds the skill confidence score together with accuracy and task results, and decays if you do not revisit.</span>
        </div>

        <div className="wrapup-summary">
          <div>
            <span className="muted small">Lesson</span>
            <strong>{data.title}</strong>
          </div>
          <div>
            <span className="muted small">Time</span>
            <strong>{data.estimatedMinutes} min estimated</strong>
          </div>
          <div>
            <span className="muted small">Next</span>
            <strong>{data.nextLessonId ? "Next lesson unlocks tomorrow" : "End of this skill's lessons"}</strong>
          </div>
        </div>
      </div>
    </section>
  );
}
