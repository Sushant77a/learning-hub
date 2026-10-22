import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { addDays, api, errorMessage, formatDate, parseDate, startOfWeek } from "../api";
import type { GoogleStatus, PlanSnapshot } from "../api/types";
import { useApp } from "../context/AppContext";
import { useApi } from "../hooks/useApi";
import { Icon } from "../components/icons";
import { ReflowModal } from "../components/ReflowModal";
import { Badge, Button, Card, ErrorNote, Field, InfoNote, Loading, TextInput } from "../components/ui";

const weekdayLabels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

export function SchedulePage() {
  const { data: plan, loading, error, reload } = useApi<PlanSnapshot>("/plan");
  const { data: google, reload: reloadGoogle } = useApi<GoogleStatus>("/calendar/google/status");
  const { notify, bump, today } = useApp();
  const [offset, setOffset] = useState(0);
  const [restDate, setRestDate] = useState(today);
  const [pending, setPending] = useState<PlanSnapshot | null>(null);
  const [busy, setBusy] = useState(false);
  const [pushBusy, setPushBusy] = useState(false);

  const weekStart = useMemo(() => addDays(startOfWeek(today), offset * 7), [today, offset]);
  const weekEnd = addDays(weekStart, 6);
  const days = useMemo(() => Array.from({ length: 7 }, (_, index) => addDays(weekStart, index)), [weekStart]);

  if (loading && !plan) return <div className="page"><Loading label="Opening the schedule…" /></div>;
  if (error || !plan) return <div className="page"><ErrorNote>{error ?? "The schedule could not be loaded."}</ErrorNote></div>;

  const entriesFor = (date: string) => plan.entries.filter((entry) => entry.date === date);
  const isRestDay = (date: string) => {
    const day = parseDate(date);
    const name = day?.toLocaleDateString("en-US", { weekday: "long" }) ?? "";
    return plan.restWeekdays.includes(name) || plan.restDates.includes(date);
  };

  const previewRestDay = async () => {
    if (!restDate) return;
    try {
      const after = await api.post<PlanSnapshot>("/plan/preview", { restDates: [restDate] });
      setPending(after);
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  const applyRestDay = async () => {
    if (!pending) return;
    setBusy(true);
    try {
      await api.post("/plan/apply", { restDates: [restDate] });
      notify(`${formatDate(restDate, { weekday: "long", day: "numeric", month: "short" })} marked as rest. Schedule shifted forward.`, "success");
      setPending(null);
      reload();
      bump();
    } catch (err) {
      notify(errorMessage(err), "error");
    } finally {
      setBusy(false);
    }
  };

  const removeRestDay = async (date: string) => {
    try {
      await api.del(`/plan/rest-days/${date}`);
      notify("Rest day removed and the plan reflowed.", "success");
      reload();
      bump();
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  const pushGoogle = async () => {
    setPushBusy(true);
    try {
      const result = await api.post<{ message: string }>("/calendar/google/push");
      notify(result.message, "success");
    } catch (err) {
      notify(errorMessage(err), "error");
    } finally {
      setPushBusy(false);
    }
  };

  const disconnectGoogle = async () => {
    try {
      await api.post("/calendar/google/disconnect");
      notify("Google Calendar disconnected. The app does not need it.", "success");
      reloadGoogle();
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  return (
    <div className="page">
      <header className="page-header">
        <div>
          <span className="eyebrow">Calendar</span>
          <h1>Schedule</h1>
          <p className="page-subtitle">
            Lessons, rest days, and detours in one place. Missed days have already moved forward — nothing is overdue.
          </p>
        </div>
        <a
          className="btn btn-secondary btn-md"
          href={`/api/calendar/ics?from=${weekStart}&to=${weekEnd}`}
          download="learning-hub-schedule.ics"
        >
          <Icon name="calendar" size={16} /> Export .ics
        </a>
      </header>

      {plan.detour ? (
        <InfoNote tone="info">
          <strong>{plan.detour.skillName} detour</strong> · {formatDate(plan.detour.startDate)} to{" "}
          {formatDate(plan.detour.endDate)}. {plan.detour.pausedSkillNames.join(", ")} resumes{" "}
          {formatDate(plan.detour.resumeOn)}.
        </InfoNote>
      ) : null}

      <div className="schedule-toolbar">
        <div className="week-nav">
          <button className="icon-btn" onClick={() => setOffset((value) => value - 1)} aria-label="Previous week">
            <Icon name="arrowLeft" />
          </button>
          <button className="week-label" onClick={() => setOffset(0)} type="button">
            {formatDate(weekStart, { day: "numeric", month: "long" })} –{" "}
            {formatDate(weekEnd, { day: "numeric", month: "long", year: "numeric" })}
          </button>
          <button className="icon-btn" onClick={() => setOffset((value) => value + 1)} aria-label="Next week">
            <Icon name="arrowRight" />
          </button>
          {offset !== 0 ? (
            <Button variant="ghost" size="sm" onClick={() => setOffset(0)}>
              This week
            </Button>
          ) : null}
        </div>
        <div className="rest-form">
          <TextInput
            type="date"
            value={restDate}
            onChange={(event) => setRestDate(event.target.value)}
            aria-label="Date to mark as rest"
            className="date-compact"
          />
          <Button variant="ghost" size="sm" onClick={previewRestDay} disabled={!restDate}>
            Mark as rest
          </Button>
        </div>
      </div>

      <div className="week-grid" role="grid" aria-label="Week schedule">
        {days.map((date, index) => {
          const rest = isRestDay(date);
          const entries = entriesFor(date);
          const isToday = date === today;
          const inDetour =
            plan.detour && date >= plan.detour.startDate && date <= plan.detour.endDate;
          return (
            <div
              className={`day-column ${rest ? "rest" : ""} ${isToday ? "is-today" : ""}`}
              key={date}
              role="gridcell"
            >
              <header className="day-head">
                <span>{weekdayLabels[index]}</span>
                <strong>{formatDate(date, { day: "numeric" })}</strong>
                {isToday ? <Badge tone="accent">Today</Badge> : null}
                {rest ? <Badge tone="muted">Rest</Badge> : null}
              </header>
              <div className="day-body">
                {rest ? (
                  <div className="day-rest">
                    <Icon name="moon" size={17} />
                    <span>Rest</span>
                  </div>
                ) : entries.length === 0 ? (
                  <div className="day-empty">{inDetour ? "Detour day" : "Free"}</div>
                ) : (
                  entries.map((entry) => (
                    <Link
                      to={`/lesson/${entry.lessonId}`}
                      className={`schedule-item ${entry.status === "done" ? "done" : ""} ${entry.isDetour ? "detour" : ""}`}
                      key={`${entry.trackId}-${entry.lessonId}-${entry.date}`}
                    >
                      <span className="schedule-item-skill">{entry.skillName}</span>
                      <span className="schedule-item-title">{entry.lessonTitle}</span>
                      <span className="schedule-item-meta">
                        {entry.minutes} min {entry.status === "done" ? "· done" : ""}
                      </span>
                    </Link>
                  ))
                )}
              </div>
            </div>
          );
        })}
      </div>

      <div className="schedule-legend">
        <span><i className="legend-dot planned" /> Planned</span>
        <span><i className="legend-dot done" /> Done</span>
        <span><i className="legend-dot detour" /> Detour</span>
        <span><i className="legend-dot rest" /> Rest day</span>
      </div>

      <div className="schedule-bottom">
        <Card>
          <div className="card-heading">
            <h3>Rest days</h3>
            <span className="muted small">recurring {plan.restWeekdays.join(" & ")}</span>
          </div>
          <div className="chip-row">
            {plan.restDates.length === 0 ? (
              <span className="muted small">No one-off rest days in this plan.</span>
            ) : (
              plan.restDates.map((date) => (
                <span className="chip static" key={date}>
                  {formatDate(date, { weekday: "short", day: "numeric", month: "short" })}
                  <button
                    type="button"
                    className="chip-remove"
                    onClick={() => removeRestDay(date)}
                    aria-label={`Remove rest day ${date}`}
                  >
                    <Icon name="close" size={12} />
                  </button>
                </span>
              ))
            )}
          </div>
        </Card>

        <Card className="google-card">
          <div className="card-heading">
            <h3>Google Calendar</h3>
            <Badge tone={google?.configured ? "accent" : "muted"}>
              {google?.configured ? "optional" : "off"}
            </Badge>
          </div>
          <p className="muted small">{google?.message ?? "Optional and off by default."}</p>
          <div className="button-row">
            {google?.connected ? (
              <>
                <Button size="sm" variant="secondary" onClick={pushGoogle} loading={pushBusy}>
                  Push schedule
                </Button>
                <Button size="sm" variant="ghost" onClick={disconnectGoogle}>
                  Disconnect
                </Button>
              </>
            ) : google?.configured ? (
              <Button
                size="sm"
                variant="secondary"
                onClick={() => {
                  window.location.href = "/api/calendar/google/connect";
                }}
              >
                Connect Google Calendar
              </Button>
            ) : (
              <span className="muted small">
                Set <code>GOOGLE_CLIENT_ID</code> and <code>GOOGLE_CLIENT_SECRET</code> to enable this.
              </span>
            )}
          </div>
          {google?.connected ? (
            <p className="muted small">Connected{google.email ? ` as ${google.email}` : ""}. Pushing updates existing events.</p>
          ) : null}
        </Card>
      </div>

      <ReflowModal
        open={Boolean(pending)}
        title="Mark this day as rest?"
        description={`${formatDate(restDate, { weekday: "long", day: "numeric", month: "long" })} will not count as missed and the plan will shift forward.`}
        before={plan}
        after={pending}
        onCancel={() => setPending(null)}
        onConfirm={applyRestDay}
        busy={busy}
        confirmLabel="Mark rest and reflow"
      />
    </div>
  );
}
