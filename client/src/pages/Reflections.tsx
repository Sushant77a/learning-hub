import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { api, errorMessage, formatDate, minutesLabel, startOfWeek } from "../api";
import type { Reflection } from "../api/types";
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
  InfoNote,
  Loading,
  ProgressBar,
  TextArea
} from "../components/ui";

export function ReflectionsPage() {
  const [params, setParams] = useSearchParams();
  const { notify, today } = useApp();
  const currentWeek = startOfWeek(today);
  const week = params.get("week") ?? currentWeek;
  const { data: current, loading, error, reload } = useApi<Reflection>(`/reflections/current?weekStart=${week}`);
  const [search, setSearch] = useState("");
  const { data: history, reload: reloadHistory } = useApi<Reflection[]>(
    `/reflections?${new URLSearchParams(search ? { q: search } : {})}`
  );
  const [wentWell, setWentWell] = useState("");
  const [wasHard, setWasHard] = useState("");
  const [toChange, setToChange] = useState("");
  const [saving, setSaving] = useState(false);
  const [suggestBusy, setSuggestBusy] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    if (!current) return;
    setWentWell(current.wentWell ?? "");
    setWasHard(current.wasHard ?? "");
    setToChange(current.toChange ?? "");
  }, [current?.weekStart, current?.id]);

  const save = async () => {
    setSaving(true);
    setFormError(null);
    try {
      await api.post("/reflections", {
        weekStart: week,
        wentWell,
        wasHard,
        toChange
      });
      notify("Reflection saved.", "success");
      reload();
      reloadHistory();
    } catch (err) {
      setFormError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const suggest = async () => {
    if (!current?.id) {
      setFormError("Save the reflection first, then ask for suggestions.");
      return;
    }
    setSuggestBusy(true);
    setFormError(null);
    try {
      await api.post(`/reflections/${current.id}/ai-suggestions`);
      notify("Plan suggestions added.", "success");
      reload();
    } catch (err) {
      const message = errorMessage(err);
      setFormError(message);
      notify(message, "error");
    } finally {
      setSuggestBusy(false);
    }
  };

  const setSuggestionState = async (id: number, state: "accepted" | "declined") => {
    try {
      await api.patch(`/reflections/suggestions/${id}`, { state });
      reload();
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  if (loading && !current) return <div className="page"><Loading label="Opening the week…" /></div>;
  if (error || !current) return <div className="page"><ErrorNote>{error ?? "The reflection could not be loaded."}</ErrorNote></div>;

  const summary = current.summary;
  const isPast = week < currentWeek;

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <span className="eyebrow">Weekly reflection</span>
          <h1>Week of {formatDate(current.weekStart, { day: "numeric", month: "long" })}</h1>
          <p className="page-subtitle">
            Three prompts, plus the facts. End the week honestly, then decide one small change.
          </p>
        </div>
        <div className="header-actions">
          {week !== currentWeek ? (
            <Button variant="ghost" onClick={() => setParams({})}>
              Back to this week
            </Button>
          ) : null}
          <Badge tone={isPast ? "muted" : "accent"}>{isPast ? "Past" : "This week"}</Badge>
        </div>
      </header>

      <div className="reflection-layout">
        <Card className="reflection-form">
          <Field label="What went well?" htmlFor="went-well" hint="Be specific. A small thing you actually did counts.">
            <TextArea
              id="went-well"
              rows={3}
              value={wentWell}
              onChange={(event) => setWentWell(event.target.value)}
              placeholder="I finished the two lessons I planned, even on the low-energy day."
            />
          </Field>
          <Field label="What was hard?" htmlFor="was-hard" hint="Name the friction, not just the feeling.">
            <TextArea
              id="was-hard"
              rows={3}
              value={wasHard}
              onChange={(event) => setWasHard(event.target.value)}
              placeholder="Nested loop complexity still feels slippery when the code is unfamiliar."
            />
          </Field>
          <Field label="What will you change?" htmlFor="to-change" hint="One adjustment for next week. Keep it small enough to keep.">
            <TextArea
              id="to-change"
              rows={3}
              value={toChange}
              onChange={(event) => setToChange(event.target.value)}
              placeholder="Do the complexity question before reading the explanation."
            />
          </Field>
          {formError ? <ErrorNote>{formError}</ErrorNote> : null}
          <div className="button-row reflection-actions">
            <Button variant="primary" onClick={save} loading={saving}>
              Save reflection
            </Button>
            <Button variant="secondary" icon="sparkles" onClick={suggest} loading={suggestBusy}>
              Suggest plan adjustments
            </Button>
            <span className="muted small">AI suggestions are optional; everything else works without them.</span>
          </div>

          {current.suggestions.length > 0 ? (
            <div className="suggestions">
              <div className="card-heading">
                <h3>Suggested adjustments</h3>
                <span className="muted small">accept or decline each one</span>
              </div>
              {current.suggestions.map((suggestion) => (
                <div className={`suggestion state-${suggestion.state.toLowerCase()}`} key={suggestion.id}>
                  <div>
                    <p>{suggestion.text}</p>
                    {suggestion.rationale ? <span className="muted small">{suggestion.rationale}</span> : null}
                  </div>
                  {suggestion.state === "Pending" ? (
                    <div className="suggestion-actions">
                      <Button size="sm" variant="secondary" onClick={() => setSuggestionState(suggestion.id, "accepted")}>
                        Accept
                      </Button>
                      <Button size="sm" variant="ghost" onClick={() => setSuggestionState(suggestion.id, "declined")}>
                        Decline
                      </Button>
                    </div>
                  ) : (
                    <Badge tone={suggestion.state === "Accepted" ? "success" : "muted"}>
                      {suggestion.state}
                    </Badge>
                  )}
                </div>
              ))}
              <p className="muted small">Accepted suggestions are a record for you; apply plan changes in the Plan screen.</p>
            </div>
          ) : null}
        </Card>

        <Card className="reflection-summary">
          <div className="card-heading">
            <h3>The week, in numbers</h3>
            <span className="muted small">rest days excluded from study days</span>
          </div>
          <div className="reflection-metrics">
            <div>
              <span className="metric-label">Lessons</span>
              <span className="metric-value">{summary.lessonsDone}<small> / {summary.lessonsPlanned} planned</small></span>
              <ProgressBar value={summary.lessonsDone} max={Math.max(1, summary.lessonsPlanned)} small />
            </div>
            <div>
              <span className="metric-label">Time</span>
              <span className="metric-value">{minutesLabel(summary.minutesDone)}<small> / {minutesLabel(summary.minutesPlanned)}</small></span>
              <ProgressBar value={summary.minutesDone} max={Math.max(1, summary.minutesPlanned)} tone="gold" small />
            </div>
            <div className="week-small-stats">
              <span><strong>{summary.studyDays}</strong> study days</span>
              <span><strong>{summary.restDays}</strong> rest days</span>
              <span><strong>{summary.reviewsAnswered}</strong> reviews</span>
              <span><strong>{Math.round(summary.accuracy * 100)}%</strong> accuracy</span>
            </div>
          </div>

          <h4 className="mini-heading">Mistake patterns</h4>
          {summary.mistakePatterns.length === 0 ? (
            <p className="muted small">No mistakes logged this week.</p>
          ) : (
            <div className="pattern-bars compact">
              {summary.mistakePatterns.map((pattern) => (
                <div className="pattern-bar-row" key={`${pattern.reason}-${pattern.skillId}`}>
                  <div>
                    <span>{pattern.reason}</span>
                    {pattern.skillName ? <span className="muted small">{pattern.skillName}</span> : null}
                  </div>
                  <span className="pattern-count">{pattern.count}</span>
                </div>
              ))}
            </div>
          )}

          <h4 className="mini-heading">Confidence movement</h4>
          {summary.confidenceChanges.length === 0 ? (
            <p className="muted small">No confidence history yet.</p>
          ) : (
            <div className="confidence-changes">
              {summary.confidenceChanges.map((change) => (
                <div key={change.skillId}>
                  <span>{change.skillName}</span>
                  <span className={change.change >= 0 ? "positive" : "negative"}>
                    {change.from?.toFixed(1) ?? "—"} → {change.to?.toFixed(1) ?? "—"}
                    <small>{change.change >= 0 ? "+" : ""}{change.change.toFixed(1)}</small>
                  </span>
                </div>
              ))}
            </div>
          )}

          {!current.id ? (
            <InfoNote tone="info">Save the reflection to keep this summary in your searchable history.</InfoNote>
          ) : null}
        </Card>
      </div>

      <section>
        <div className="section-heading">
          <h2>Past reflections</h2>
          <div className="search-input small-search">
            <Icon name="search" size={14} />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search reflections…"
              aria-label="Search past reflections"
            />
          </div>
        </div>
        {!history || history.length === 0 ? (
          <EmptyState title="No reflections found" icon="reflections">
            {search ? "Try a different search." : "Save this week's reflection and it will appear here."}
          </EmptyState>
        ) : (
          <div className="reflection-history">
            {history.map((item) => (
              <button
                key={item.id}
                className={`reflection-history-item ${item.weekStart === week ? "on" : ""}`}
                onClick={() => setParams({ week: item.weekStart })}
              >
                <span className="history-week">
                  {formatDate(item.weekStart, { day: "numeric", month: "long" })}
                  <span className="muted small">{item.summary.lessonsDone} lessons · {Math.round(item.summary.accuracy * 100)}% accuracy</span>
                </span>
                <span className="history-preview">
                  {item.wentWell || item.wasHard || item.toChange || "No prompts answered yet."}
                </span>
                <Icon name="arrowRight" size={15} />
              </button>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
