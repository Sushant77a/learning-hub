import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api, errorMessage, formatDate, minutesLabel } from "../api";
import type { PlanSnapshot, Today } from "../api/types";
import { useApp } from "../context/AppContext";
import { useApi } from "../hooks/useApi";
import { Icon } from "../components/icons";
import { ReflowModal } from "../components/ReflowModal";
import { Button, buttonVariants } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Progress } from "@/components/ui/progress";
import { Separator } from "@/components/ui/separator";
import { EmptyState, ErrorNote, InfoNote, Loading, Modal, TextInput } from "../components/ui";

export function TodayPage() {
  const { data, loading, error, reload } = useApi<Today>("/today");
  const { bump, notify, settings, today } = useApp();
  const navigate = useNavigate();
  const [reflow, setReflow] = useState<{
    before: PlanSnapshot;
    after: PlanSnapshot;
    trackId: number;
  } | null>(null);
  const [reflowBusy, setReflowBusy] = useState(false);
  const [decision, setDecision] = useState<"resume" | "keepBoth" | "extend" | null>(null);
  const [extendTo, setExtendTo] = useState("");
  const [generating, setGenerating] = useState<string | null>(null);
  const [generateError, setGenerateError] = useState<string | null>(null);

  const refreshAll = () => {
    reload();
    bump();
  };

  const openResume = async (trackId: number) => {
    try {
      const before = await api.get<PlanSnapshot>("/plan");
      const after = await api.post<PlanSnapshot>(`/tracks/${trackId}/state/preview`, { state: "active" });
      setReflow({ before, after, trackId });
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  const applyResume = async () => {
    if (!reflow) return;
    setReflowBusy(true);
    try {
      await api.post(`/tracks/${reflow.trackId}/state`, { state: "active" });
      notify("Track resumed. The plan has been reflowed.", "success");
      setReflow(null);
      refreshAll();
    } catch (err) {
      notify(errorMessage(err), "error");
    } finally {
      setReflowBusy(false);
    }
  };

  const generateCurriculum = async (skillId: string) => {
    setGenerating(skillId);
    setGenerateError(null);
    try {
      await api.post(`/skills/${skillId}/generate-curriculum`, { days: 5 });
      notify("Mini-curriculum generated and saved to the curriculum folder.", "success");
      refreshAll();
    } catch (err) {
      setGenerateError(errorMessage(err));
    } finally {
      setGenerating(null);
    }
  };

  const finishDetour = async (choice: "resume" | "keepBoth" | "extend") => {
    if (!data?.detour) return;
    try {
      await api.post(`/timeboxes/${data.detour.timeBoxId}/end`, {
        choice,
        extendTo: choice === "extend" ? extendTo : null
      });
      notify(
        choice === "resume"
          ? "Detour ended. Your previous track is active again."
          : choice === "keepBoth"
            ? "Both tracks are now active with an equal split."
            : "Detour extended.",
        "success"
      );
      setDecision(null);
      refreshAll();
    } catch (err) {
      notify(errorMessage(err), "error");
    }
  };

  if (loading && !data) {
    return (
      <div className="page today-v2">
        <Loading label="Preparing today's lessons…" />
      </div>
    );
  }
  if (error || !data) {
    return (
      <div className="page today-v2">
        <ErrorNote>{error ?? "Could not load today."}</ErrorNote>
      </div>
    );
  }

  const hour = new Date().getHours();
  const greeting = hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
  const activeCards = data.cards;
  const plannedMinutes = activeCards.reduce((sum, card) => sum + card.minutes, 0);
  const completedCount = activeCards.filter((card) => card.lessonStatus === "completed").length;
  const donePercent = activeCards.length === 0 ? 0 : Math.round((completedCount / activeCards.length) * 100);
  const nextCard = activeCards.find((card) => card.lessonStatus !== "completed") ?? null;
  const otherCards = activeCards.filter((card) => card !== nextCard);

  return (
    <div className="page today-v2">
      <section className="relative overflow-hidden rounded-[24px] border border-border bg-card p-6 shadow-sm sm:p-8">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -right-28 -top-28 size-72 rounded-full bg-primary/15 blur-3xl"
        />
        <div className="relative flex flex-wrap items-start justify-between gap-6">
          <div className="min-w-0 max-w-2xl">
            <span className="text-[0.68rem] font-semibold uppercase tracking-[0.2em] text-muted-foreground">
              {data.dateLabel}
            </span>
            <h1 className="mt-2 text-3xl font-semibold tracking-tight text-foreground sm:text-[2.4rem]">
              {greeting}.
            </h1>
            <p className="mt-1 text-base text-muted-foreground sm:text-lg">One clear step at a time.</p>

            <div className="mt-5 flex flex-wrap items-center gap-2">
              <Badge>{activeCards.length} {activeCards.length === 1 ? "track" : "tracks"}</Badge>
              <Badge variant="outline">{minutesLabel(plannedMinutes)} planned</Badge>
              {data.dueReviewCount > 0 ? (
                <Badge variant="outline">{data.dueReviewCount} to review</Badge>
              ) : null}
            </div>
          </div>

          <Link to="/plan#switch-focus" className={buttonVariants({ variant: "outline" })} data-slot="button">
            <Icon name="target" size={15} />
            Switch focus
          </Link>
        </div>

        {activeCards.length > 0 ? (
          <div className="relative mt-7">
            <div className="mb-2 flex items-center justify-between text-[0.7rem] font-medium text-muted-foreground">
              <span>Today's progress</span>
              <span>
                {completedCount}/{activeCards.length} lessons
              </span>
            </div>
            <Progress value={donePercent} />
          </div>
        ) : null}
      </section>

      <div className="mt-5 space-y-3">
        {data.missedDaysShifted > 0 ? (
          <InfoNote tone="info">
            Your plan shifted forward because {data.missedDaysShifted} study{" "}
            {data.missedDaysShifted === 1 ? "day passed" : "days passed"} unfinished. There is nothing overdue to catch up on.
          </InfoNote>
        ) : null}

        {data.warnings.map((warning) => (
          <InfoNote tone="warn" key={warning}>
            <span>{warning}</span>
            <Link to="/plan" className="text-link">
              Manage tracks
            </Link>
          </InfoNote>
        ))}
      </div>

      {!data.isRestDay && nextCard ? (
        <section className="mt-5 overflow-hidden rounded-[24px] border border-border bg-card">
          <div className="flex flex-wrap items-start justify-between gap-5 p-6">
            <div className="min-w-0 max-w-2xl">
              <span className="text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-primary">
                Up next
              </span>
              <h2 className="mt-2 text-2xl font-semibold tracking-tight text-foreground">
                {nextCard.title || nextCard.skillName}
              </h2>
              <p className="mt-1.5 text-sm leading-relaxed text-muted-foreground">{nextCard.summary}</p>
              <div className="mt-3 flex flex-wrap items-center gap-2">
                <Badge variant="outline">{nextCard.skillName}</Badge>
                <Badge variant="outline">{minutesLabel(nextCard.minutes)}</Badge>
                {nextCard.lessonStatus === "inprogress" ? <Badge>In progress</Badge> : null}
              </div>
            </div>
            <div className="flex w-full flex-col gap-2 sm:w-auto sm:items-end">
              {nextCard.lessonId ? (
                <Link
                  to={`/lesson/${nextCard.lessonId}`}
                  className={buttonVariants({ size: "lg" })}
                  data-slot="button"
                >
                  {nextCard.lessonStatus === "inprogress" ? "Continue lesson" : "Start lesson"}
                  <Icon name="arrowRight" size={15} />
                </Link>
              ) : (
                <Button onClick={() => generateCurriculum(nextCard.skillId)} disabled={generating === nextCard.skillId}>
                  {generating === nextCard.skillId ? "Generating…" : "Generate lessons"}
                </Button>
              )}
              <Link
                to={`/skills/${nextCard.skillId}`}
                className={buttonVariants({ variant: "ghost", size: "sm" })}
                data-slot="button"
              >
                Open skill
              </Link>
            </div>
          </div>
        </section>
      ) : null}

      {!data.isRestDay && activeCards.length > 0 && !nextCard ? (
        <section className="mt-5 flex flex-wrap items-center justify-between gap-4 rounded-[24px] border border-border bg-[var(--success-soft)] p-6">
          <div>
            <h2 className="text-lg font-semibold text-foreground">Everything planned for today is done.</h2>
            <p className="mt-1 text-sm text-muted-foreground">
              {data.dueReviewCount > 0
                ? `${data.dueReviewCount} recall question${data.dueReviewCount === 1 ? "" : "s"} are waiting.`
                : "Come back tomorrow, or do a light review."}
            </p>
          </div>
          <Link to="/review" className={buttonVariants()} data-slot="button">
            Review now
          </Link>
        </section>
      ) : null}

      {data.detour ? (
        <section
          className={`mt-5 rounded-[20px] border p-5 ${
            data.timeBoxDecisionDue
              ? "border-[var(--gold)]/50 bg-[var(--gold-soft)]"
              : "border-border bg-card"
          }`}
        >
          <div className="flex flex-wrap items-start gap-4">
            <div className="flex size-11 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary">
              <Icon name="target" size={20} />
            </div>
            <div className="min-w-0 flex-1">
              <strong className="block text-sm font-semibold text-foreground">
                {data.detour.skillName} detour: day {data.detour.dayNumber} of {data.detour.totalDays}
              </strong>
              <span className="mt-0.5 block text-xs text-muted-foreground">
                {data.detour.pausedSkillNames.length > 0
                  ? `${data.detour.pausedSkillNames.join(", ")} resumes on ${formatDate(data.detour.resumeOn, {
                      day: "numeric",
                      month: "short"
                    })}`
                  : `Running at ${data.detour.detourPercent}% of daily time`}
                {data.detour.mode === "parallel"
                  ? ` · parallel ${data.detour.detourPercent}/${100 - data.detour.detourPercent}`
                  : ""}
              </span>
            </div>
            {data.timeBoxDecisionDue ? (
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-xs text-muted-foreground">Time box ended. What next?</span>
                <Button size="sm" onClick={() => setDecision("resume")}>
                  Resume paused
                </Button>
                <Button size="sm" variant="outline" onClick={() => setDecision("keepBoth")}>
                  Keep both
                </Button>
                <Button size="sm" variant="ghost" onClick={() => setDecision("extend")}>
                  Extend
                </Button>
              </div>
            ) : (
              <Link to="/plan" className={buttonVariants({ variant: "ghost", size: "sm" })} data-slot="button">
                Manage <Icon name="arrowRight" size={13} />
              </Link>
            )}
          </div>
        </section>
      ) : null}

      {data.reflectionDue ? (
        <Link
          to={`/reflections?week=${data.reflectionWeekStart}`}
          className="mt-5 flex items-center gap-4 rounded-[20px] border border-border bg-card p-4 transition-colors hover:bg-secondary/60"
        >
          <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary">
            <Icon name="reflections" size={19} />
          </span>
          <span className="min-w-0 flex-1">
            <strong className="block text-sm font-semibold text-foreground">Close the week</strong>
            <span className="block text-xs text-muted-foreground">
              Three quiet prompts and a summary of what actually happened.
            </span>
          </span>
          <Icon name="arrowRight" size={16} />
        </Link>
      ) : null}

      {data.isRestDay ? (
        <Card className="mt-5 [--card-spacing:0px] p-5">
          <div className="flex items-start gap-4">
          <div className="flex size-11 shrink-0 items-center justify-center rounded-full bg-secondary text-foreground">
            <Icon name="moon" size={22} />
          </div>
          <div className="min-w-0">
            <h2 className="text-sm font-semibold text-foreground">Rest day</h2>
            <p className="mt-1 text-xs text-muted-foreground">
              {data.restNote ??
                "No lessons today. Rest days never count as missed and they do not break your streak."}
            </p>
            <div className="mt-4 flex flex-wrap gap-2">
              <Link to="/review" className={buttonVariants({ variant: "outline", size: "sm" })} data-slot="button">
                Light review ({data.dueReviewCount})
              </Link>
              <Link to="/notes" className={buttonVariants({ variant: "ghost", size: "sm" })} data-slot="button">
                Read your notes
              </Link>
            </div>
          </div>
          </div>
        </Card>
      ) : null}

      {!data.isRestDay && activeCards.length === 0 ? (
        <EmptyState
          title={data.hasActiveTracks ? "No lesson scheduled today" : "Choose your first skill"}
          icon="book"
          action={
            <Link to="/skills" className={buttonVariants()} data-slot="button">
              Browse skills
            </Link>
          }
        >
          {data.hasActiveTracks
            ? "Your active tracks have no lessons available yet. Add lessons to the curriculum folder or generate a starter set with AI."
            : "Start a track for C#, DSA, or anything else you want to learn."}
        </EmptyState>
      ) : null}

      {!data.isRestDay && otherCards.length > 0 ? (
        <section className="mt-6" aria-label="Also today">
          <h2 className="mb-3 text-[0.68rem] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
            Also today
          </h2>
          <div className="grid gap-4 lg:grid-cols-2">
          {otherCards.map((card) => {
            const isDone = card.lessonStatus === "completed";
            return (
              <Card
                key={card.trackId}
                className={`gap-0 [--card-spacing:0px] ${isDone ? "border-l-4 border-l-[var(--success)]" : ""}`}
              >
                <div className="flex items-start justify-between gap-3 p-5 pb-3">
                  <div className="flex min-w-0 items-center gap-3">
                    <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-[0.7rem] font-bold uppercase tracking-wide text-primary">
                      {card.skillName.slice(0, 2)}
                    </span>
                    <div className="min-w-0">
                      <span className="block truncate text-xs font-medium text-foreground">{card.skillName}</span>
                      <span className="block text-[0.7rem] text-muted-foreground">
                        {minutesLabel(card.minutes)} slice · {card.sharePercent}% of today
                      </span>
                    </div>
                  </div>
                  <div className="flex shrink-0 flex-wrap justify-end gap-1.5">
                    {card.isDetour ? (
                      <Badge variant="outline" className="bg-[var(--gold-soft)] text-[var(--gold)]">
                        Detour
                      </Badge>
                    ) : null}
                    {isDone ? (
                      <Badge variant="outline" className="bg-[var(--success-soft)] text-[var(--success)]">
                        Done today
                      </Badge>
                    ) : card.lessonStatus === "inprogress" ? (
                      <Badge>In progress</Badge>
                    ) : (
                      <Badge variant="outline">Up next</Badge>
                    )}
                  </div>
                </div>

                <Separator />

                <div className="p-5">
                  {card.needsCurriculum ? (
                    <div>
                      <h2 className="text-sm font-semibold text-foreground">No lessons yet</h2>
                      <p className="mt-1 text-xs text-muted-foreground">
                        This skill has a track but no lessons. Generate a mini-curriculum sized to your next five days, then
                        edit the files in <code>curriculum/</code> whenever you like.
                      </p>
                      <div className="mt-4 flex flex-wrap items-center gap-2">
                        <Button
                          size="sm"
                          disabled={generating === card.skillId}
                          onClick={() => generateCurriculum(card.skillId)}
                        >
                          <Icon name="sparkles" size={14} />
                          {generating === card.skillId ? "Generating…" : "Generate mini-curriculum"}
                        </Button>
                        <Link
                          to={`/skills/${card.skillId}`}
                          className={buttonVariants({ variant: "ghost", size: "sm" })}
                          data-slot="button"
                        >
                          Add lessons by hand
                        </Link>
                      </div>
                      {generateError ? <ErrorNote>{generateError}</ErrorNote> : null}
                    </div>
                  ) : (
                    <>
                      {card.topic ? (
                        <span className="mb-1.5 inline-block rounded-full bg-secondary px-2 py-0.5 text-[0.65rem] font-medium uppercase tracking-wide text-muted-foreground">
                          {card.topic.replace(/-/g, " ")}
                        </span>
                      ) : null}
                      <h2 className="text-base font-semibold leading-snug text-foreground">{card.title}</h2>
                      <p className="mt-1.5 text-xs leading-relaxed text-muted-foreground">{card.summary}</p>
                      {isDone && card.learnedNote ? (
                        <blockquote className="mt-3 rounded-r-lg border-l-2 border-l-primary bg-secondary/60 py-2 pl-3 text-xs italic text-foreground/80">
                          “{card.learnedNote}”
                        </blockquote>
                      ) : null}
                      <div className="mt-4 flex items-center justify-between gap-3">
                        <span className="text-[0.7rem] text-muted-foreground">
                          {card.estimatedMinutes} min · {card.isDsa ? "DSA" : "theory first"}
                        </span>
                        {card.lessonId ? (
                          <Link to={`/lesson/${card.lessonId}`} className={buttonVariants({ size: "sm" })} data-slot="button">
                            {isDone ? "Revisit lesson" : card.lessonStatus === "inprogress" ? "Continue" : "Open lesson"}
                            <Icon name="arrowRight" size={13} />
                          </Link>
                        ) : null}
                      </div>
                    </>
                  )}
                </div>
              </Card>
            );
          })}
          </div>
        </section>
      ) : null}

      <div className="mt-6 grid gap-4 lg:grid-cols-2">
        <Card className="[--card-spacing:0px] p-5">
          <div className="flex items-center gap-4">
            <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Icon name="review" size={19} />
            </div>
            <div className="min-w-0 flex-1">
              <h3 className="text-sm font-semibold text-foreground">Recall</h3>
              <p className="mt-0.5 text-xs text-muted-foreground">
                {data.dueReviewCount > 0
                  ? `${data.dueReviewCount} question${data.dueReviewCount === 1 ? "" : "s"} ready to revisit.`
                  : "Warm-ups bring questions back when they are due."}
              </p>
            </div>
            <Link to="/review" className={buttonVariants({ variant: "outline", size: "sm" })} data-slot="button">
              {data.dueReviewCount > 0 ? "Review now" : "Open review"}
            </Link>
          </div>
        </Card>

        {data.pausedTracks.length > 0 ? (
          <Card className="[--card-spacing:0px] p-5">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold text-foreground">Paused tracks</h3>
              <Badge variant="outline">{data.pausedTracks.length}</Badge>
            </div>
            <ul className="mt-3 space-y-2">
              {data.pausedTracks.map((track) => (
                <li key={track.trackId} className="flex items-center justify-between gap-3">
                  <div className="min-w-0">
                    <strong className="block truncate text-xs font-medium text-foreground">{track.skillName}</strong>
                    <span className="block text-[0.7rem] text-muted-foreground">
                      {track.pausedReason ?? "Paused"}
                      {track.resumeOn ? ` · projected resume ${formatDate(track.resumeOn)}` : ""}
                    </span>
                  </div>
                  <Button variant="ghost" size="sm" onClick={() => openResume(track.trackId)}>
                    Resume
                  </Button>
                </li>
              ))}
            </ul>
          </Card>
        ) : null}
      </div>

      <footer className="mt-6 flex flex-wrap items-center justify-between gap-2 text-[0.72rem] text-muted-foreground">
        <span>
          {data.dailyMinutes} minutes a day · {settings?.restWeekdays.join(" & ") || "weekends"} rest ·{" "}
          <Link to="/plan" className="text-link">
            adjust plan
          </Link>
        </span>
      </footer>

      <ReflowModal
        open={Boolean(reflow)}
        title="Resume this track?"
        description="Resuming reflows the schedule. Nothing changes until you confirm."
        before={reflow?.before}
        after={reflow?.after ?? null}
        busy={reflowBusy}
        onCancel={() => setReflow(null)}
        onConfirm={applyResume}
        confirmLabel="Resume and reflow"
      />

      <Modal
        open={decision !== null}
        title={
          decision === "resume"
            ? "Resume the paused track"
            : decision === "keepBoth"
              ? "Keep both tracks running"
              : "Extend the detour"
        }
        onClose={() => setDecision(null)}
        footer={
          <>
            <Button variant="ghost" onClick={() => setDecision(null)}>
              Cancel
            </Button>
            <Button
              onClick={() => decision && finishDetour(decision)}
              disabled={decision === "extend" && !extendTo}
            >
              {decision === "extend" ? "Extend detour" : "Confirm"}
            </Button>
          </>
        }
      >
        {decision === "resume" ? (
          <p>
            The {data.detour?.skillName} detour will end and its track will pause.{" "}
            {data.detour?.pausedSkillNames.join(", ")} will become active again, and their lessons will shift to start now.
          </p>
        ) : decision === "keepBoth" ? (
          <p>
            {data.detour?.skillName} stays active alongside {data.detour?.pausedSkillNames.join(", ")}. Your daily time is
            split equally between them.
          </p>
        ) : (
          <div className="field">
            <label htmlFor="extend-date">New end date</label>
            <TextInput
              id="extend-date"
              type="date"
              value={extendTo}
              min={today}
              onChange={(event) => setExtendTo(event.target.value)}
            />
            <div className="field-hint">The time box can be extended by up to 60 days total.</div>
          </div>
        )}
      </Modal>
    </div>
  );
}
