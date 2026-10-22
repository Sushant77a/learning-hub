import type { PlanSnapshot } from "../api/types";
import { formatDate, minutesLabel } from "../api";
import { Icon } from "./icons";
import { Button, InfoNote, Modal } from "./ui";

export function ReflowModal({
  open,
  title = "Confirm the new schedule",
  description,
  before,
  after,
  onCancel,
  onConfirm,
  busy,
  confirmLabel = "Apply new dates"
}: {
  open: boolean;
  title?: string;
  description?: string;
  before?: PlanSnapshot | null;
  after: PlanSnapshot | null;
  onCancel: () => void;
  onConfirm: () => void;
  busy?: boolean;
  confirmLabel?: string;
}) {
  if (!after) return null;

  const beforeByTrack = new Map((before?.tracks ?? []).map((track) => [track.trackId, track]));
  const changed =
    before?.dailyMinutes !== after.dailyMinutes ||
    before?.restWeekdays.join(",") !== after.restWeekdays.join(",") ||
    before?.tracks.some((track) => {
      const next = after.tracks.find((item) => item.trackId === track.trackId);
      return !next || next.finishDate !== track.finishDate || next.state !== track.state;
    });

  const firstEntries = after.entries
    .filter((entry) => entry.status !== "done" && entry.status !== "missed")
    .slice(0, 4);

  return (
    <Modal
      open={open}
      title={title}
      onClose={onCancel}
      wide
      footer={
        <>
          <Button variant="ghost" onClick={onCancel}>
            Keep current plan
          </Button>
          <Button variant="primary" onClick={onConfirm} loading={busy}>
            {confirmLabel}
          </Button>
        </>
      }
    >
      {description ? <p className="modal-lead">{description}</p> : null}

      {after.changes.length > 0 ? (
        <ul className="change-list">
          {after.changes.map((change) => (
            <li key={change}>
              <Icon name="arrowRight" size={14} /> {change}
            </li>
          ))}
        </ul>
      ) : null}

      {after.detour ? (
        <InfoNote tone="info">
          <strong>{after.detour.skillName} detour</strong> · {formatDate(after.detour.startDate)} to{" "}
          {formatDate(after.detour.endDate)} ·{" "}
          {after.detour.mode === "pause"
            ? `${after.detour.pausedSkillNames.join(", ") || "Other tracks"} paused, resuming ${formatDate(after.detour.resumeOn)}`
            : `${after.detour.detourPercent}% of daily time in parallel`}
        </InfoNote>
      ) : null}

      <div className="table-wrap">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Track</th>
              <th scope="col">Daily time</th>
              <th scope="col">Projected finish</th>
              <th scope="col">Change</th>
            </tr>
          </thead>
          <tbody>
            {after.tracks
              .filter((track) => track.lessonsTotal > 0 || track.state !== "notstarted")
              .map((track) => {
                const previous = beforeByTrack.get(track.trackId);
                const beforeFinish = previous?.finishDate ?? null;
                const afterFinish = track.finishDate ?? null;
                const dateChanged = before && beforeFinish !== afterFinish;
                return (
                  <tr key={track.trackId}>
                    <td>
                      <span className="table-title">{track.skillName}</span>
                      <span className={`state-dot state-${track.state}`}>{track.state}</span>
                    </td>
                    <td>
                      {track.dailyMinutes > 0 ? `${minutesLabel(track.dailyMinutes)} (${track.sharePercent}%)` : "—"}
                    </td>
                    <td>
                      {afterFinish ? formatDate(afterFinish, { day: "numeric", month: "short", year: "numeric" }) : "—"}
                    </td>
                    <td>
                      {dateChanged ? (
                        <span className="date-change">
                          <span className="muted">{beforeFinish ? formatDate(beforeFinish) : "—"}</span>
                          <Icon name="arrowRight" size={13} />
                          <strong>{afterFinish ? formatDate(afterFinish) : "paused"}</strong>
                        </span>
                      ) : (
                        <span className="muted">no change</span>
                      )}
                    </td>
                  </tr>
                );
              })}
          </tbody>
        </table>
      </div>

      {firstEntries.length > 0 ? (
        <div className="preview-next">
          <h3>Next up</h3>
          {firstEntries.map((entry) => (
            <div className="preview-entry" key={`${entry.trackId}-${entry.lessonId}-${entry.date}`}>
              <span>{formatDate(entry.date, { weekday: "short", day: "numeric", month: "short" })}</span>
              <span className="truncate">{entry.lessonTitle}</span>
              <span className="muted">{minutesLabel(entry.minutes)}</span>
            </div>
          ))}
        </div>
      ) : null}

      <p className="muted small">
        {changed || !before
          ? "Nothing is applied until you confirm. Completed lessons stay completed."
          : "The projected dates look the same, but you can still apply the change."}
      </p>
    </Modal>
  );
}
