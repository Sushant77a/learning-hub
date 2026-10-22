import { useState } from "react";
import { api, errorMessage } from "../api";
import type { AnswerResult, LessonQuestion, Question } from "../api/types";
import { Markdown, CodeBlock } from "./Markdown";
import { Badge, Button, InfoNote, TextInput } from "./ui";
import { Icon } from "./icons";

const reasonChips = [
  "Forgot a null check",
  "Confused ref and out",
  "Off by one",
  "Missed an edge case",
  "Rushed the reading",
  "Concept gap"
];

export function QuestionCard({
  question,
  trackId,
  index,
  total,
  onAnswered,
  revealOnly = false
}: {
  question: Question | LessonQuestion;
  trackId?: number | null;
  index?: number;
  total?: number;
  onAnswered?: (correct: boolean) => void;
  revealOnly?: boolean;
}) {
  const [selected, setSelected] = useState<number | null>(null);
  const [result, setResult] = useState<AnswerResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [customReason, setCustomReason] = useState("");
  const [reasonSaved, setReasonSaved] = useState(false);

  const isComplexity = question.kind === "complexity";
  const answered = result !== null;

  const submit = async (selectedIndex: number | null, correct?: boolean) => {
    setBusy(true);
    setError(null);
    try {
      const response = await api.post<AnswerResult>(`/questions/${question.id}/answer`, {
        selectedIndex,
        correct: correct ?? null,
        trackId: trackId ?? null
      });
      setResult(response);
      setSelected(selectedIndex);
      onAnswered?.(response.correct);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const saveReason = async (value: string) => {
    if (!result?.mistakeId) return;
    setReason(value);
    try {
      await api.patch(`/mistakes/${result.mistakeId}`, { reasonTag: value, note: null });
      setReasonSaved(true);
    } catch (err) {
      setError(errorMessage(err));
    }
  };

  const prompt = (
    <div className="question-prompt">
      <Markdown>{question.prompt}</Markdown>
      {"code" in question && question.code ? <CodeBlock code={question.code} language="csharp" /> : null}
    </div>
  );

  if (isComplexity) {
    return (
      <article className={`question-card ${answered ? (result.correct ? "correct" : "wrong") : ""}`}>
        <header className="question-head">
          <Badge tone="accent">Complexity</Badge>
          {index !== undefined && total ? <span className="muted small">{index + 1} / {total}</span> : null}
        </header>
        {prompt}
        {!answered ? (
          <div className="self-grade">
            <p className="muted small">Work it out first, then grade yourself honestly.</p>
            <div className="button-row">
              <Button variant="secondary" disabled={busy} onClick={() => submit(null, true)}>
                I got it
              </Button>
              <Button variant="ghost" disabled={busy} onClick={() => submit(null, false)}>
                I missed it
              </Button>
            </div>
          </div>
        ) : (
          <div className="answer-panel">
            <InfoNote tone={result.correct ? "success" : "warn"}>
              <strong>Answer:</strong> {result.answer ?? result.explanation}
            </InfoNote>
            {result.answer && result.explanation ? <p className="explanation">{result.explanation}</p> : null}
            {result.correct ? null : (
              <MistakeReason
                reason={reason}
                customReason={customReason}
                saved={reasonSaved}
                onPick={saveReason}
                onCustomChange={setCustomReason}
              />
            )}
          </div>
        )}
        {error ? <div className="inline-error">{error}</div> : null}
      </article>
    );
  }

  return (
    <article className={`question-card ${answered ? (result.correct ? "correct" : "wrong") : ""}`}>
      <header className="question-head">
        <Badge tone={question.kind === "custom" ? "neutral" : "accent"}>
          {question.kind === "custom" ? "Custom check" : "Check"}
        </Badge>
        {"fromPausedTrack" in question && question.fromPausedTrack ? <Badge tone="warn">From a paused track</Badge> : null}
        {index !== undefined && total ? <span className="muted small">{index + 1} / {total}</span> : null}
      </header>
      {prompt}

      <div className="options" role={answered ? "list" : "radiogroup"} aria-label="Answer options">
        {question.options.map((option, optionIndex) => {
          const isCorrect = answered && result.correctIndex === optionIndex;
          const isChosen = answered && selected === optionIndex;
          return (
            <label
              key={`${optionIndex}-${option.slice(0, 12)}`}
              className={`option ${answered ? "answered" : ""} ${isCorrect ? "is-correct" : ""} ${
                isChosen && !isCorrect ? "is-wrong" : ""
              }`}
            >
              <input
                type="radio"
                name={`question-${question.id}`}
                checked={selected === optionIndex}
                disabled={answered || busy || revealOnly}
                onChange={() => submit(optionIndex)}
              />
              <span className="option-letter">{String.fromCharCode(65 + optionIndex)}</span>
              <span className="option-copy">
                <span>{option}</span>
                {answered && result.optionExplanations[optionIndex] ? (
                  <span className="option-why">{result.optionExplanations[optionIndex]}</span>
                ) : null}
              </span>
              {isCorrect ? <Icon name="check" size={17} className="option-mark" /> : null}
            </label>
          );
        })}
      </div>

      {answered ? (
        <div className="answer-panel">
          <div className={`answer-verdict ${result.correct ? "good" : "bad"}`}>
            <Icon name={result.correct ? "check" : "close"} size={16} />
            {result.correct ? "Correct." : "Not quite."}
          </div>
          <Markdown>{result.explanation}</Markdown>
          {result.correct ? null : (
            <MistakeReason
              reason={reason}
              customReason={customReason}
              saved={reasonSaved}
              onPick={saveReason}
              onCustomChange={setCustomReason}
            />
          )}
        </div>
      ) : null}
      {error ? <div className="inline-error">{error}</div> : null}
    </article>
  );
}

function MistakeReason({
  reason,
  customReason,
  saved,
  onPick,
  onCustomChange
}: {
  reason: string;
  customReason: string;
  saved: boolean;
  onPick: (value: string) => void;
  onCustomChange: (value: string) => void;
}) {
  return (
    <div className="mistake-reason">
      <div className="mistake-reason-head">
        <span>What tripped you up?</span>
        {saved ? <Badge tone="success">Saved to mistakes</Badge> : <span className="muted small">optional, useful later</span>}
      </div>
      <div className="chip-row">
        {reasonChips.map((chip) => (
          <button
            key={chip}
            type="button"
            className={`chip ${reason === chip ? "on" : ""}`}
            onClick={() => onPick(chip)}
          >
            {chip}
          </button>
        ))}
      </div>
      <div className="row-gap">
        <TextInput
          value={customReason}
          onChange={(event) => onCustomChange(event.target.value)}
          placeholder="Or type your own reason"
          aria-label="Custom mistake reason"
        />
        <Button
          size="sm"
          variant="secondary"
          disabled={!customReason.trim()}
          onClick={() => onPick(customReason.trim())}
        >
          Log reason
        </Button>
      </div>
    </div>
  );
}
