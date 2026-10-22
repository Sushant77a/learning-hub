const paths: Record<string, string> = {
  today: "M12 3v2m0 14v2M3 12h2m14 0h2M5.6 5.6l1.4 1.4m10 10 1.4 1.4m0-12.8-1.4 1.4m-10 10L5.6 18.4M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0Z",
  skills: "M12 3 3 7.5l9 4.5 9-4.5L12 3Zm-9 9 9 4.5 9-4.5M3 16.5 12 21l9-4.5",
  plan: "M7 3v3m10-3v3M4 8h16M5 5h14a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1Zm3.5 9 1.8 1.8 3.8-3.8",
  schedule: "M7 3v3m10-3v3M4 8h16M5 5h14a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1Zm3 7h3v3H8v-3Zm6 0h3v3h-3v-3Z",
  review: "M4 5v5h5M20 19v-5h-5M5.6 14.4A7 7 0 0 0 18 16M18.4 9.6A7 7 0 0 0 6 8",
  notes: "M6 3h8l4 4v14H6V3Zm8 0v5h4M9 12h6M9 16h4",
  progress: "M4 20V10m5 10V4m5 16v-7m5 7V8",
  reflections: "M20 4c-8 0-13 4-13 10a5 5 0 0 0 5 5c6 0 8-6 8-15ZM4 21c1-5 4-8 8-10",
  settings: "M12 15.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7Zm8-3.5a8.3 8.3 0 0 0-.1-1.2l2-1.6-2-3.4-2.4 1a8 8 0 0 0-2-1.2L15 3h-4l-.5 2.6a8 8 0 0 0-2 1.2l-2.4-1-2 3.4 2 1.6a8.3 8.3 0 0 0 0 2.4l-2 1.6 2 3.4 2.4-1a8 8 0 0 0 2 1.2L11 21h4l.5-2.6a8 8 0 0 0 2-1.2l2.4 1 2-3.4-2-1.6c.1-.4.1-.8.1-1.2Z",
  search: "M10.5 18a7.5 7.5 0 1 0 0-15 7.5 7.5 0 0 0 0 15Zm5.3-2.2L21 21",
  sun: "M12 4V2m0 20v-2m8-8h2M2 12h2m13.7-5.7 1.4-1.4M5 19l1.4-1.4m11.2 0L19 19M5 5l1.4 1.4M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0Z",
  moon: "M20 14.5A8.5 8.5 0 0 1 9.5 4 8.5 8.5 0 1 0 20 14.5Z",
  book: "M4 5.5A2.5 2.5 0 0 1 6.5 3H20v16H6.5A2.5 2.5 0 0 0 4 21.5v-16Zm2.5-2.5A2.5 2.5 0 0 1 9 5.5V19",
  close: "M6 6l12 12M18 6 6 18",
  plus: "M12 5v14M5 12h14",
  check: "M5 12.5 9.5 17 19 7",
  arrowRight: "M5 12h14m-6-6 6 6-6 6",
  arrowLeft: "M19 12H5m6 6-6-6 6-6",
  play: "M8 5v14l11-7L8 5Z",
  pause: "M8 5v14M16 5v14",
  sparkles: "m12 3 1.8 4.2L18 9l-4.2 1.8L12 15l-1.8-4.2L6 9l4.2-1.8L12 3Zm7 11 .9 2.1 2.1.9-2.1.9L19 20l-.9-2.1-2.1-.9 2.1-.9L19 14Z",
  clock: "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Zm0-13v5l3 2",
  external: "M14 4h6v6m0-6-9 9M19 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1h5",
  trash: "M4 7h16M9 7V4h6v3m-8 0 1 13h8l1-13M10 11v5m4-5v5",
  edit: "m4 20 4.5-1L20 7.5 16.5 4 5 15.5 4 20Z",
  chevronDown: "m6 9 6 6 6-6",
  resource: "M5 4h9l5 5v11H5V4Zm9 0v5h5",
  warning: "M12 3 2.5 20h19L12 3Zm0 6v5m0 3v.5",
  refresh: "M20 11a8 8 0 1 0-2.3 6M20 4v7h-7",
  flame: "M12 21c4 0 6-2.8 6-6 0-4-3-6-3-9-3 1.5-4 4-4 6-1-.5-1.5-1.5-2-1.5C7.5 12 6 13.5 6 15c0 3.2 2 6 6 6Z",
  calendar: "M7 3v3m10-3v3M4 8h16M5 5h14a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1Z",
  target: "M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Zm0-5a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm0-3.5a.5.5 0 1 0 0-1 .5.5 0 0 0 0 1Z",
  command: "M8 8V5.5A2.5 2.5 0 1 0 5.5 8H8Zm0 0v8m0-8h8M8 16v2.5A2.5 2.5 0 1 1 5.5 16H8Zm8-8V5.5A2.5 2.5 0 1 1 18.5 8H16Zm0 0v8m0 0v2.5a2.5 2.5 0 1 0 2.5-2.5H16Z",
  menu: "M4 7h16M4 12h16M4 17h16",
  bulb: "M9 18h6m-5 3h4M8 14a5 5 0 1 1 8 0c-1 1-1 1.5-1 2H9c0-.5 0-1-1-2Z"
};

export function Icon({
  name,
  size = 18,
  className
}: {
  name: keyof typeof paths | string;
  size?: number;
  className?: string;
}) {
  const path = paths[name] ?? paths.book;
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.6"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      <path d={path} />
    </svg>
  );
}
