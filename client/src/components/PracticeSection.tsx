import { useState } from "react";
import type { PracticeProblem } from "../api/types";
import { Markdown, CodeBlock } from "./Markdown";
import { Badge, Button } from "./ui";
import { Icon } from "./icons";

export function PracticeSection({ problems, open, onToggle }: { problems: PracticeProblem[]; open: boolean; onToggle: () => void }) {
  if (problems.length === 0) return null;

  return (
    <section className="practice">
      <button className="practice-toggle" onClick={onToggle} aria-expanded={open}>
        <span className="practice-title">
          <Icon name="target" size={18} />
          <span>
            Practice problems
            <span className="muted small">{problems.length} to try, hints only if you need them</span>
          </span>
        </span>
        <span className="practice-count">{open ? "Hide" : "Open"}</span>
      </button>
      {open ? (
        <div className="practice-list">
          <p className="muted small practice-note">
            Practice is optional and separate from the lesson flow. Try before you reveal. The solution is for checking your thinking,
            not for skipping the work.
          </p>
          {problems.map((problem, problemIndex) => (
            <PracticeCard key={problem.title} problem={problem} number={problemIndex + 1} />
          ))}
        </div>
      ) : null}
    </section>
  );
}

function PracticeCard({ problem, number }: { problem: PracticeProblem; number: number }) {
  const [hintsShown, setHintsShown] = useState(0);
  const [solutionShown, setSolutionShown] = useState(false);

  return (
    <article className="practice-card">
      <header>
        <Badge tone="accent">Practice {number}</Badge>
        <h3>{problem.title}</h3>
      </header>
      <Markdown>{problem.prompt}</Markdown>

      {hintsShown > 0 ? (
        <div className="hint-stack">
          {problem.hints.slice(0, hintsShown).map((hint, index) => (
            <div className="hint" key={index}>
              <span className="hint-label">Hint {index + 1}</span>
              <Markdown>{hint}</Markdown>
            </div>
          ))}
        </div>
      ) : null}

      {solutionShown ? (
        <div className="solution">
          <div className="solution-head">
            <span>Solution</span>
            <span className="muted small">Check your reasoning, not just the code</span>
          </div>
          <Markdown>{problem.solution}</Markdown>
        </div>
      ) : null}

      <div className="button-row practice-actions">
        {hintsShown < problem.hints.length ? (
          <Button variant="secondary" size="sm" icon="bulb" onClick={() => setHintsShown((count) => count + 1)}>
            {hintsShown === 0 ? "Reveal hint 1" : `Reveal hint ${hintsShown + 1}`}
          </Button>
        ) : null}
        {!solutionShown ? (
          <Button variant="ghost" size="sm" onClick={() => setSolutionShown(true)}>
            Show solution
          </Button>
        ) : null}
        {solutionShown ? (
          <Button variant="ghost" size="sm" onClick={() => setSolutionShown(false)}>
            Hide solution
          </Button>
        ) : null}
      </div>
      {solutionShown ? <CodeBlock code={problem.solution} language="csharp" compact /> : null}
    </article>
  );
}
