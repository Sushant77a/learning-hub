export { api, ApiError, errorMessage } from "./client";

export function todayIso(): string {
  const now = new Date();
  const offset = now.getTimezoneOffset();
  return new Date(now.getTime() - offset * 60_000).toISOString().slice(0, 10);
}

export function parseDate(value: string | null | undefined): Date | null {
  if (!value) return null;
  const [year, month, day] = value.slice(0, 10).split("-").map(Number);
  if (!year || !month || !day) return null;
  return new Date(year, month - 1, day, 12);
}

export function formatDate(value: string | null | undefined, options?: Intl.DateTimeFormatOptions): string {
  const date = parseDate(value);
  if (!date) return "—";
  return date.toLocaleDateString(undefined, options ?? { day: "numeric", month: "short" });
}

export function formatLongDate(value: string | null | undefined): string {
  return formatDate(value, { weekday: "long", day: "numeric", month: "long" });
}

export function addDays(value: string, days: number): string {
  const date = parseDate(value);
  if (!date) return value;
  date.setDate(date.getDate() + days);
  const offset = date.getTimezoneOffset();
  return new Date(date.getTime() - offset * 60_000).toISOString().slice(0, 10);
}

export function startOfWeek(value: string): string {
  const date = parseDate(value) ?? new Date();
  const day = (date.getDay() + 6) % 7;
  date.setDate(date.getDate() - day);
  const offset = date.getTimezoneOffset();
  return new Date(date.getTime() - offset * 60_000).toISOString().slice(0, 10);
}

export function weekdaysShort(): { key: string; label: string }[] {
  return [
    { key: "Monday", label: "Mon" },
    { key: "Tuesday", label: "Tue" },
    { key: "Wednesday", label: "Wed" },
    { key: "Thursday", label: "Thu" },
    { key: "Friday", label: "Fri" },
    { key: "Saturday", label: "Sat" },
    { key: "Sunday", label: "Sun" }
  ];
}

export function minutesLabel(minutes: number): string {
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest === 0 ? `${hours}h` : `${hours}h ${rest}m`;
}

export function dayName(value: string): string {
  return parseDate(value)?.toLocaleDateString(undefined, { weekday: "short" }) ?? "";
}
