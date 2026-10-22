import { useEffect, useRef, useState } from "react";
import { api, errorMessage, minutesLabel, todayIso, weekdaysShort } from "../api";
import type { GoogleStatus, PlanSnapshot } from "../api/types";
import { useApp } from "../context/AppContext";
import { useApi } from "../hooks/useApi";
import { Icon } from "../components/icons";
import { ReflowModal } from "../components/ReflowModal";
import {
  Badge,
  Button,
  Card,
  ErrorNote,
  Field,
  InfoNote,
  Loading,
  Select,
  TextInput
} from "../components/ui";

export function SettingsPage() {
  const {
    settings,
    settingsLoading,
    settingsError,
    refreshSettings,
    bump,
    notify,
    setThemePreference,
    themePreference,
    today
  } = useApp();
  const { data: plan, reload: reloadPlan } = useApi<PlanSnapshot>("/plan");
  const { data: google, reload: reloadGoogle } = useApi<GoogleStatus>("/calendar/google/status");
  const [dailyMinutes, setDailyMinutes] = useState(90);
  const [restWeekdays, setRestWeekdays] = useState<string[]>([]);
  const [startDate, setStartDate] = useState(todayIso());
  const [pending, setPending] = useState<PlanSnapshot | null>(null);
  const [pendingBusy, setPendingBusy] = useState(false);
  const [bugfix, setBugfix] = useState(25);
  const [provider, setProvider] = useState("");
  const [importing, setImporting] = useState(false);
  const [importResult, setImportResult] = useState<string | null>(null);
  const [reloading, setReloading] = useState(false);
  const fileRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!settings) return;
    setDailyMinutes(settings.dailyMinutes);
    setRestWeekdays(settings.restWeekdays);
    setStartDate(settings.startDate);
    setBugfix(settings.bugFixFrequency);
    setProvider(settings.aiProvider ?? "");
  }, [settings]);

  useEffect(() => {
    if (new URLSearchParams(window.location.search).get("calendar") === "connected") {
      notify("Google Calendar connected.", "success");
      reloadGoogle();
    }
    const error = new URLSearchParams(window.location.search).get("calendar");
    if (error === "error") {
      notify(new URLSearchParams(window.location.search).get("message") ?? "Google connection failed.", "error");
    }
  }, [notify, reloadGoogle]);

  if (settingsLoading && !settings) return <div className="page"><Loading label="Loading settings…" /></div>;
  if (settingsError && !settings) return <div className="page"><ErrorNote>{settingsError}</ErrorNote></div>;
  if (!settings) return null;

  const previewPlan = async () => {
    try {
      const after = await api.post<PlanSnapshot>("/plan/preview", {
        dailyMinutes,
        restWeekdays: restWeekdays.join(","),
        startDate
      });
      setPending(after);
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  const applyPlan = async () => {
    setPendingBusy(true);
    try {
      await api.post("/plan/apply", { dailyMinutes, restWeekdays: restWeekdays.join(","), startDate });
      notify("Plan settings applied.", "success");
      setPending(null);
      refreshSettings();
      reloadPlan();
      bump();
    } catch (err) {
      notify(errorMessage(err), "error");
    } finally {
      setPendingBusy(false);
    }
  };

  const patchSimple = async (body: Record<string, unknown>, message: string) => {
    try {
      await api.patch("/settings", body);
      notify(message, "success");
      refreshSettings();
      bump();
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  const importBackup = async (file: File) => {
    setImporting(true);
    setImportResult(null);
    try {
      const result = await api.upload<{ skills: number; lessons: number; planEntries: number; message: string }>("/import", file);
      setImportResult(result.message);
      notify("Backup restored.", "success");
      refreshSettings();
      bump();
    } catch (err) {
      notify(errorMessage(err), "error");
    } finally {
      setImporting(false);
      if (fileRef.current) fileRef.current.value = "";
    }
  };

  const reloadCurriculum = async () => {
    setReloading(true);
    try {
      const result = await api.post<{ sync: { skills: number; lessons: number; errors: string[] } }>("/settings/reload-curriculum");
      notify(`Curriculum reloaded: ${result.sync.lessons} lessons, ${result.sync.errors.length} issues.`, "success");
      refreshSettings();
      bump();
    } catch (err) {
      notify(errorMessage(err), "error");
    } finally {
      setReloading(false);
    }
  };

  const planDirty =
    dailyMinutes !== settings.dailyMinutes ||
    restWeekdays.slice().sort().join(",") !== settings.restWeekdays.slice().sort().join(",") ||
    startDate !== settings.startDate;

  return (
    <div className="page settings-page">
      <header className="page-header">
        <div>
          <span className="eyebrow">Settings</span>
          <h1>The local setup</h1>
          <p className="page-subtitle">Everything is stored on this machine. AI is optional, called only on demand, and its key never reaches the browser.</p>
        </div>
      </header>

      <div className="settings-grid">
        <Card>
          <div className="card-heading">
            <h2>Plan defaults</h2>
            {planDirty ? <Badge tone="accent">Unsaved</Badge> : <span className="muted small">in effect</span>}
          </div>
          <Field label="Daily study time" htmlFor="set-minutes">
            <div className="minutes-row">
              <input
                id="set-minutes"
                type="range"
                className="range"
                min={15}
                max={360}
                step={15}
                value={dailyMinutes}
                onChange={(event) => setDailyMinutes(Number(event.target.value))}
              />
              <span className="minutes-value">{minutesLabel(dailyMinutes)}</span>
            </div>
          </Field>
          <div className="field">
            <span className="field-label">Rest weekdays</span>
            <div className="weekday-row">
              {weekdaysShort().map((day) => (
                <button
                  key={day.key}
                  type="button"
                  className={`weekday ${restWeekdays.includes(day.key) ? "on" : ""}`}
                  onClick={() =>
                    setRestWeekdays((current) =>
                      current.includes(day.key) ? current.filter((value) => value !== day.key) : [...current, day.key]
                    )
                  }
                >
                  {day.label}
                </button>
              ))}
            </div>
          </div>
          <Field label="Plan start" htmlFor="set-start">
            <TextInput id="set-start" type="date" value={startDate} onChange={(event) => setStartDate(event.target.value)} />
          </Field>
          <Button variant="primary" onClick={previewPlan} disabled={!planDirty}>
            Preview and apply
          </Button>
        </Card>

        <Card>
          <div className="card-heading">
            <h2>AI on demand</h2>
            <Badge tone={settings.ai.configured ? "success" : "muted"}>
              {settings.ai.configured ? "configured" : "off"}
            </Badge>
          </div>
          <div className="ai-status-card">
            <div>
              <span className="metric-label">Provider</span>
              <strong>{settings.ai.provider}</strong>
            </div>
            <div>
              <span className="metric-label">Model</span>
              <strong>{settings.ai.model}</strong>
            </div>
            <div>
              <span className="metric-label">API key</span>
              <strong>{settings.ai.keyStatus}</strong>
            </div>
          </div>
          <p className="muted small">{settings.ai.message}</p>

          <Field
            label="Provider override"
            htmlFor="ai-provider"
            hint="The key is read from AI_API_KEY on the server. Use Ollama for a fully local model."
          >
            <Select
              id="ai-provider"
              value={provider}
              onChange={(event) => {
                setProvider(event.target.value);
                void patchSimple({ aiProvider: event.target.value }, "AI provider updated. Restart is not required.");
              }}
            >
              <option value="">Use environment default</option>
              <option value="openai">OpenAI</option>
              <option value="anthropic">Anthropic</option>
              <option value="ollama">Ollama (local)</option>
              <option value="openai-compatible">OpenAI-compatible endpoint</option>
            </Select>
          </Field>
          <InfoNote tone="info">
            AI calls go through the local backend. If the key or provider is unavailable, every AI button shows a clear error and the
            rest of the app keeps working.
          </InfoNote>
        </Card>

        <Card>
          <div className="card-heading">
            <h2>Lesson behaviour</h2>
          </div>
          <Field
            label={`Fix-the-bug frequency: ${bugfix}%`}
            htmlFor="bugfix-frequency"
            hint="Roughly 1 in 4 lessons by default. It is never forced; every bug-fix step can be skipped."
          >
            <input
              id="bugfix-frequency"
              type="range"
              className="range"
              min={0}
              max={100}
              step={5}
              value={bugfix}
              onChange={(event) => setBugfix(Number(event.target.value))}
              onPointerUp={() => void patchSimple({ bugFixFrequency: bugfix }, "Fix-the-bug frequency updated.")}
              onKeyUp={() => void patchSimple({ bugFixFrequency: bugfix }, "Fix-the-bug frequency updated.")}
            />
            <div className="range-ends">
              <span>never</span>
              <span>every lesson</span>
            </div>
          </Field>

          <Field label="Theme" htmlFor="theme">
            <Select
              id="theme"
              value={themePreference}
              onChange={(event) => setThemePreference(event.target.value as "light" | "dark" | "system")}
            >
              <option value="system">Follow the system</option>
              <option value="light">Light</option>
              <option value="dark">Dark</option>
            </Select>
          </Field>
          <InfoNote tone="info">
            Press <kbd>D</kbd> anywhere to toggle light and dark. Press <kbd>?</kbd> for all shortcuts.
          </InfoNote>
        </Card>

        <Card>
          <div className="card-heading">
            <h2>Backup and data</h2>
            <span className="muted small">everything stored locally</span>
          </div>
          <p className="muted small">
            Export a complete JSON backup before experimenting. Import replaces the current database with the file's contents.
          </p>
          <div className="button-row">
            <a className="btn btn-secondary btn-md" href="/api/export" download={`learning-hub-backup-${today}.json`}>
              <Icon name="resource" size={16} /> Export backup
            </a>
            <Button variant="secondary" onClick={() => fileRef.current?.click()} loading={importing}>
              Import backup
            </Button>
            <input
              ref={fileRef}
              type="file"
              accept="application/json,.json"
              className="visually-hidden"
              onChange={(event) => {
                const file = event.target.files?.[0];
                if (file) void importBackup(file);
              }}
            />
          </div>
          {importResult ? <InfoNote tone="success">{importResult}</InfoNote> : null}
          <div className="data-counts">
            <span>{settings.counts.skills} skills</span>
            <span>{settings.counts.lessons} lessons</span>
            <span>{settings.counts.notes} notes</span>
            <span>{settings.counts.resources} resources</span>
            <span>{settings.counts.mistakes} mistakes</span>
          </div>
        </Card>

        <Card>
          <div className="card-heading">
            <h2>Curriculum folder</h2>
            <Button variant="ghost" size="sm" icon="refresh" onClick={reloadCurriculum} loading={reloading}>
              Reload files
            </Button>
          </div>
          <p className="muted small">
            Lessons are read from this folder on startup. Edit or add JSON files, then reload without restarting.
          </p>
          <code className="path-box">{settings.curriculumRoot}</code>
          {settings.curriculumErrors.length > 0 ? (
            <div className="curriculum-errors">
              <ErrorNote>{settings.curriculumErrors.length} curriculum file issue(s). See the server log for details.</ErrorNote>
              <ul>
                {settings.curriculumErrors.slice(0, 5).map((issue) => (
                  <li key={issue}>{issue}</li>
                ))}
              </ul>
            </div>
          ) : (
            <InfoNote tone="success">All curriculum files parsed cleanly.</InfoNote>
          )}
          <a className="text-link" href="/docs/lesson-format.md" target="_blank" rel="noreferrer">
            Lesson file format <Icon name="external" size={13} />
          </a>
        </Card>

        <Card>
          <div className="card-heading">
            <h2>Google Calendar</h2>
            <Badge tone={google?.configured ? "accent" : "muted"}>{google?.configured ? "optional" : "off"}</Badge>
          </div>
          <p className="muted small">{google?.message ?? "Optional and off by default. The app works fully without it."}</p>
          <div className="button-row">
            {google?.connected ? (
              <Button
                variant="ghost"
                size="sm"
                onClick={() =>
                  api
                    .post("/calendar/google/disconnect")
                    .then(() => {
                      notify("Google Calendar disconnected.", "success");
                      reloadGoogle();
                    })
                    .catch((err) => notify(errorMessage(err), "error"))
                }
              >
                Disconnect
              </Button>
            ) : google?.configured ? (
              <Button
                variant="secondary"
                size="sm"
                onClick={() => {
                  window.location.href = "/api/calendar/google/connect";
                }}
              >
                Connect
              </Button>
            ) : (
              <span className="muted small">
                Requires <code>GOOGLE_CLIENT_ID</code> and <code>GOOGLE_CLIENT_SECRET</code>.
              </span>
            )}
          </div>
        </Card>
      </div>

      <ReflowModal
        open={Boolean(pending)}
        title="Apply plan defaults?"
        description="These defaults affect every active track. Review the projected finish dates before applying."
        before={plan}
        after={pending}
        onCancel={() => setPending(null)}
        onConfirm={applyPlan}
        busy={pendingBusy}
        confirmLabel="Apply defaults"
      />
    </div>
  );
}
