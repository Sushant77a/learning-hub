import { useMemo, useState } from "react";
import { api, errorMessage } from "../api";
import type { Question } from "../api/types";
import { useApp } from "../context/AppContext";
import { useApi } from "../hooks/useApi";
import { QuestionCard } from "../components/QuestionCard";
import { CustomQuestionModal } from "../components/CustomQuestionModal";
import { Icon } from "../components/icons";
import {
  Badge,
  Button,
  Card,
  EmptyState,
  ErrorNote,
  Loading,
  Modal,
  ProgressBar
} from "../components/ui";

export function ReviewPage() {
  const { data, loading, error, reload } = useApi<Question[]>("/review?limit=60");
  const { notify } = useApp();
  const [skillFilter, setSkillFilter] = useState("all");
  const [session, setSession] = useState(false);
  const [index, setIndex] = useState(0);
  const [answered, setAnswered] = useState(false);
  const [score, setScore] = useState({ correct: 0, total: 0 });
  const [customOpen, setCustomOpen] = useState(false);
  const [finished, setFinished] = useState(false);

  const questions = useMemo(
    () => (data ?? []).filter((question) => skillFilter === "all" || question.skillId === skillFilter),
    [data, skillFilter]
  );

  const skills = useMemo(() => {
    const map = new Map<string, { name: string; count: number }>();
    for (const question of data ?? []) {
      const existing = map.get(question.skillId);
      map.set(question.skillId, { name: question.skillName, count: (existing?.count ?? 0) + 1 });
    }
    return [...map.entries()];
  }, [data]);

  const start = () => {
    setSession(true);
    setIndex(0);
    setAnswered(false);
    setScore({ correct: 0, total: 0 });
    setFinished(false);
  };

  const next = () => {
    if (index + 1 >= questions.length) {
      setFinished(true);
      setSession(false);
      notify("Review session complete.", "success");
      reload();
      return;
    }
    setIndex((value) => value + 1);
    setAnswered(false);
  };

  if (loading && !data) return <div className="page"><Loading label="Finding due questions…" /></div>;
  if (error) return <div className="page"><ErrorNote>{error}</ErrorNote></div>;

  if (finished) {
    const percent = score.total === 0 ? 0 : Math.round((score.correct / score.total) * 100);
    return (
      <div className="page review-finish">
        <div className="finish-mark"><Icon name="review" size={26} /></div>
        <span className="eyebrow">Review complete</span>
        <h1>{score.correct} of {score.total} recalled</h1>
        <p className="page-subtitle">
          {percent >= 80
            ? "Strong recall. The next intervals will stretch out."
            : "The questions you missed will come back tomorrow. That is the system working."}
        </p>
        <div className="review-score-ring">
          <span>{percent}%</span>
        </div>
        <div className="button-row">
          <Button variant="primary" onClick={start} disabled={questions.length === 0}>
            Go again
          </Button>
          <Button variant="secondary" onClick={() => { setFinished(false); reload(); }}>
            Back to review
          </Button>
        </div>
      </div>
    );
  }

  if (session && questions.length > 0) {
    const question = questions[index];
    return (
      <div className="page review-session">
        <header className="review-session-head">
          <div>
            <span className="eyebrow">Recall session</span>
            <h1>{question.skillName}</h1>
          </div>
          <div className="review-session-meta">
            <span className="muted small">{index + 1} / {questions.length}</span>
            <Button variant="ghost" size="sm" onClick={() => { setSession(false); reload(); }}>
              End session
            </Button>
          </div>
        </header>
        <ProgressBar value={index + (answered ? 1 : 0)} max={questions.length} small />
        <div className="review-question">
          <QuestionCard
            key={question.id}
            question={question}
            trackId={null}
            index={index}
            total={questions.length}
            onAnswered={(correct) => {
              setAnswered(true);
              setScore((current) => ({ correct: current.correct + (correct ? 1 : 0), total: current.total + 1 }));
            }}
          />
        </div>
        <div className="lesson-nav">
          <span className="muted small">{question.lessonTitle}</span>
          <Button variant="primary" onClick={next} disabled={!answered}>
            {index + 1 >= questions.length ? "Finish" : "Next question"}
            <Icon name="arrowRight" size={16} />
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <span className="eyebrow">Spaced repetition</span>
          <h1>Review</h1>
          <p className="page-subtitle">
            Everything due across every skill, in one session. Wrong answers come back tomorrow; correct ones stretch out.
          </p>
        </div>
        <Button variant="secondary" icon="plus" onClick={() => setCustomOpen(true)}>
          Add question
        </Button>
      </header>

      {data?.length === 0 ? (
        <EmptyState
          title="Nothing due right now"
          icon="review"
          action={<Button onClick={() => setCustomOpen(true)}>Add a custom question</Button>}
        >
          Finish a lesson or answer a few questions and they will return here on their own schedule. You can also add your own questions.
        </EmptyState>
      ) : (
        <>
          <Card className="review-overview">
            <div className="review-big-count">
              <span className="big-number">{questions.length}</span>
              <span className="muted">questions ready</span>
            </div>
            <div className="review-skill-filter">
              <div className="chip-row">
                <button className={`chip ${skillFilter === "all" ? "on" : ""}`} onClick={() => setSkillFilter("all")}>
                  All skills
                </button>
                {skills.map(([skillId, skill]) => (
                  <button
                    key={skillId}
                    className={`chip ${skillFilter === skillId ? "on" : ""}`}
                    onClick={() => setSkillFilter(skillId)}
                  >
                    {skill.name} <span className="chip-count">{skill.count}</span>
                  </button>
                ))}
              </div>
            </div>
            <Button variant="primary" size="lg" onClick={start} disabled={questions.length === 0}>
              Start session
            </Button>
          </Card>

          <section className="review-list-section">
            <div className="section-heading">
              <h2>In this session</h2>
              <span className="muted small">grouped by skill</span>
            </div>
            {skills
              .filter(([skillId]) => skillFilter === "all" || skillFilter === skillId)
              .map(([skillId, skill]) => (
                <div className="review-skill-group" key={skillId}>
                  <h3>
                    {skill.name} <Badge tone="muted">{skill.count}</Badge>
                  </h3>
                  <ul>
                    {questions
                      .filter((question) => question.skillId === skillId)
                      .slice(0, 8)
                      .map((question) => (
                        <li key={question.id}>
                          <span className={`question-kind kind-${question.kind}`}>{question.kind}</span>
                          <span className="truncate">{question.prompt.split("\n")[0]}</span>
                          {question.fromPausedTrack ? <Badge tone="warn">paused track</Badge> : null}
                        </li>
                      ))}
                  </ul>
                </div>
              ))}
          </section>
        </>
      )}

      <Modal open={false} title="" onClose={() => undefined}>
        <span />
      </Modal>

      <CustomQuestionModal open={customOpen} onClose={() => setCustomOpen(false)} onCreated={reload} />
    </div>
  );
}
