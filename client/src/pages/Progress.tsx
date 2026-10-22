import { useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { formatDate, minutesLabel } from "../api";
import type { Mistake, ProgressOverview } from "../api/types";
import { useApi } from "../hooks/useApi";
import { Icon } from "../components/icons";
import {
  Badge,
  Card,
  ConfidenceMeter,
  EmptyState,
  ErrorNote,
  Loading,
  ProgressBar,
  Select,
  Sparkline,
  Tabs
} from "../components/ui";

export function ProgressPage() {
  const [params, setParams] = useSearchParams();
  const tab = params.get("tab") === "mistakes" ? "mistakes" : "overview";
  const [skillFilter, setSkillFilter] = useState(params.get("skill") ?? "");
  const { data, loading, error } = useApi<ProgressOverview>("/progress");
  const { data: mistakes } = useApi<Mistake[]>(
    `/mistakes?${new URLSearchParams(skillFilter ? { skillId: skillFilter } : {})}`
  );

  const skillOptions = data?.skills.filter((skill) => skill.lessonsTotal > 0 || skill.lessonsDone > 0) ?? [];
  const patterns = useMemo(() => {
    const all = data?.mistakePatterns ?? [];
    return skillFilter ? all.filter((pattern) => pattern.skillId === skillFilter) : all;
  }, [data, skillFilter]);

  if (loading && !data) return <div className="page"><Loading label="Collecting your progress…" /></div>;
  if (error || !data) return <div className="page"><ErrorNote>{error ?? "Progress could not be loaded."}</ErrorNote></div>;

  const summary = data.thisWeek;
  const maxMinutes = Math.max(1, ...data.last14Days.map((day) => day.minutes));

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <span className="eyebrow">Progress</span>
          <h1>Quiet evidence of the work</h1>
          <p className="page-subtitle">Streaks exclude rest days. Time spent counts completed lesson slices, not time with the app open.</p>
        </div>
      </header>

      <div className="stat-strip">
        <Card className="stat-card streak-card">
          <span className="stat-icon"><Icon name="flame" size={19} /></span>
          <div>
            <span className="stat-value large">{data.streak.current}</span>
            <span className="stat-label">day streak</span>
          </div>
        </Card>
        <Card className="stat-card">
          <span className="stat-icon"><Icon name="target" size={19} /></span>
          <div>
            <span className="stat-value large">{data.streak.longest}</span>
            <span className="stat-label">longest streak</span>
          </div>
        </Card>
        <Card className="stat-card">
          <span className="stat-icon"><Icon name="book" size={19} /></span>
          <div>
            <span className="stat-value large">{data.totalLessonsDone}</span>
            <span className="stat-label">lessons completed</span>
          </div>
        </Card>
        <Card className="stat-card">
          <span className="stat-icon"><Icon name="review" size={19} /></span>
          <div>
            <span className="stat-value large">{Math.round(data.overallAccuracy * 100)}%</span>
            <span className="stat-label">all-time accuracy</span>
          </div>
        </Card>
      </div>

      <Tabs
        value={tab}
        onChange={(value) => {
          const next = new URLSearchParams(params);
          if (value === "overview") next.delete("tab");
          else next.set("tab", value);
          setParams(next);
        }}
        options={[
          { value: "overview", label: "Overview" },
          { value: "mistakes", label: "Mistake journal", count: data.totalMistakes }
        ]}
      />

      {tab === "overview" ? (
        <>
          <div className="progress-grid">
            <Card>
              <div className="card-heading">
                <h3>This week</h3>
                <span className="muted small">{formatDate(summary.weekStart)} – {formatDate(summary.weekEnd)}</span>
              </div>
              <div className="week-metrics">
                <div>
                  <span className="metric-label">Lessons</span>
                  <span className="metric-value">{summary.lessonsDone}<small> / {summary.lessonsPlanned} planned</small></span>
                  <ProgressBar value={summary.lessonsDone} max={Math.max(1, summary.lessonsPlanned)} small />
                </div>
                <div>
                  <span className="metric-label">Time</span>
                  <span className="metric-value">
                    {minutesLabel(summary.minutesDone)}<small> / {minutesLabel(summary.minutesPlanned)}</small>
                  </span>
                  <ProgressBar value={summary.minutesDone} max={Math.max(1, summary.minutesPlanned)} small tone="gold" />
                </div>
                <div className="week-small-stats">
                  <span><strong>{summary.studyDays}</strong> study days</span>
                  <span><strong>{summary.restDays}</strong> rest days</span>
                  <span><strong>{Math.round(summary.accuracy * 100)}%</strong> accuracy</span>
                  <span><strong>{summary.mistakesLogged}</strong> mistakes logged</span>
                </div>
              </div>
            </Card>

            <Card>
              <div className="card-heading">
                <h3>Last 14 days</h3>
                <span className="muted small">minutes studied</span>
              </div>
              <div className="bar-chart" aria-label="Minutes studied over the last 14 days">
                {data.last14Days.map((day) => (
                  <div className="bar-column" key={day.date} title={`${day.date}: ${day.minutes} min`}>
                    <div className="bar-track">
                      <span style={{ height: `${Math.max(2, (day.minutes / maxMinutes) * 100)}%` }} className={day.minutes > 0 ? "filled" : ""} />
                    </div>
                    <span className="bar-label">{new Date(day.date).toLocaleDateString(undefined, { weekday: "narrow" })}</span>
                  </div>
                ))}
              </div>
            </Card>
          </div>

          <section>
            <div className="section-heading">
              <h2>Confidence by skill</h2>
              <span className="muted small">last 60 days</span>
            </div>
            {data.skills.length === 0 ? (
              <EmptyState title="No confidence history yet">Complete a lesson and rate it to start the trend.</EmptyState>
            ) : (
              <div className="confidence-skill-list">
                {data.skills
                  .filter((skill) => skill.lessonsTotal > 0 || skill.lessonsDone > 0)
                  .map((skill) => (
                    <div className="confidence-skill-row" key={skill.skillId}>
                      <div>
                        <strong>{skill.skillName}</strong>
                        <span className="muted small">{skill.lessonsDone} / {skill.lessonsTotal} lessons</span>
                      </div>
                      <Sparkline
                        values={skill.history.map((point) => point.score)}
                        width={220}
                        height={44}
                        label={`${skill.skillName} confidence trend`}
                      />
                      <ConfidenceMeter score={skill.confidence} compact />
                    </div>
                  ))}
              </div>
            )}
          </section>

          <section>
            <div className="section-heading">
              <h2>Recurring mistakes</h2>
              <span className="muted small">across all skills</span>
            </div>
            <div className="mistake-pattern-list">
              {data.mistakePatterns.length === 0 ? (
                <p className="muted">No mistakes logged yet. That is not the same as none made.</p>
              ) : (
                data.mistakePatterns.map((pattern) => (
                  <div className="pattern-row" key={`${pattern.reason}-${pattern.skillId}`}>
                    <span className="pattern-reason">{pattern.reason}</span>
                    <span className="pattern-skill">{pattern.skillName ?? "—"}</span>
                    <span className="pattern-count">{pattern.count}×</span>
                  </div>
                ))
              )}
            </div>
          </section>
        </>
      ) : (
        <>
          <div className="mistakes-toolbar">
            <Select value={skillFilter} onChange={(event) => setSkillFilter(event.target.value)} aria-label="Filter mistakes by skill">
              <option value="">All skills</option>
              {skillOptions.map((skill) => (
                <option key={skill.skillId} value={skill.skillId}>
                  {skill.skillName}
                </option>
              ))}
            </Select>
            <span className="muted small">{mistakes?.length ?? 0} logged</span>
          </div>

          <div className="mistakes-grid">
            <Card>
              <div className="card-heading">
                <h3>Patterns</h3>
                <span className="muted small">grouped by reason</span>
              </div>
              {patterns.length === 0 ? (
                <p className="muted small">No mistakes match this filter.</p>
              ) : (
                <div className="pattern-bars">
                  {patterns.map((pattern) => (
                    <div key={`${pattern.reason}-${pattern.skillId}`} className="pattern-bar-row">
                      <div>
                        <span>{pattern.reason}</span>
                        {!skillFilter && pattern.skillName ? <span className="muted small">{pattern.skillName}</span> : null}
                      </div>
                      <span className="pattern-count">{pattern.count}</span>
                    </div>
                  ))}
                </div>
              )}
            </Card>

            <Card>
              <div className="card-heading">
                <h3>Journal</h3>
                <span className="muted small">newest first</span>
              </div>
              {!mistakes || mistakes.length === 0 ? (
                <p className="muted small">Nothing logged yet.</p>
              ) : (
                <ul className="mistake-list">
                  {mistakes.map((mistake) => (
                    <li key={mistake.id}>
                      <div className="mistake-head">
                        <Badge tone={mistake.kind === "failedtask" ? "warn" : mistake.kind === "bugfix" ? "accent" : "danger"}>
                          {mistake.kind === "failedtask" ? "task" : mistake.kind === "bugfix" ? "bug" : "answer"}
                        </Badge>
                        <strong>{mistake.reasonTag}</strong>
                        <span className="muted small">{formatDate(mistake.createdAt, { day: "numeric", month: "short" })}</span>
                      </div>
                      <div className="mistake-meta">
                        <span>{mistake.skillName}</span>
                        {mistake.lessonTitle ? <span>· {mistake.lessonTitle}</span> : null}
                      </div>
                      {mistake.note ? <p className="mistake-note">{mistake.note}</p> : null}
                    </li>
                  ))}
                </ul>
              )}
            </Card>
          </div>
        </>
      )}
    </div>
  );
}
