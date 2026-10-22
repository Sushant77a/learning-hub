import {
  useEffect,
  useRef,
  type ButtonHTMLAttributes,
  type InputHTMLAttributes,
  type ReactNode,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes
} from "react";
import { Icon } from "./icons";

type ButtonVariant = "primary" | "secondary" | "ghost" | "danger" | "quiet";
type ButtonSize = "sm" | "md" | "lg";

export function Button({
  variant = "secondary",
  size = "md",
  icon,
  loading,
  className = "",
  children,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
  size?: ButtonSize;
  icon?: string;
  loading?: boolean;
}) {
  return (
    <button
      className={`btn btn-${variant} btn-${size} ${className}`.trim()}
      {...props}
      disabled={props.disabled || loading}
      aria-busy={loading || undefined}
    >
      {loading ? <span className="spinner spinner-sm" aria-hidden="true" /> : icon ? <Icon name={icon} size={size === "sm" ? 15 : 17} /> : null}
      {children ? <span>{children}</span> : null}
    </button>
  );
}

export function Card({
  children,
  className = "",
  as = "div",
  ...rest
}: {
  children: ReactNode;
  className?: string;
  as?: "div" | "section" | "article" | "li";
  [key: string]: unknown;
}) {
  const Element = as as "div";
  return (
    <Element className={`card ${className}`.trim()} {...rest}>
      {children}
    </Element>
  );
}

export function Badge({
  children,
  tone = "neutral",
  className = ""
}: {
  children: ReactNode;
  tone?: "neutral" | "accent" | "warn" | "danger" | "success" | "muted";
  className?: string;
}) {
  return <span className={`badge badge-${tone} ${className}`.trim()}>{children}</span>;
}

export function ProgressBar({
  value,
  max = 100,
  label,
  tone = "accent",
  small
}: {
  value: number;
  max?: number;
  label?: string;
  tone?: "accent" | "gold" | "danger";
  small?: boolean;
}) {
  const percent = max <= 0 ? 0 : Math.min(100, Math.max(0, (value / max) * 100));
  return (
    <div
      className={`progress-wrap ${small ? "progress-small" : ""}`.trim()}
      role="progressbar"
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={Math.round(percent)}
      aria-valuetext={label ? `${label}: ${Math.round(percent)}%` : `${Math.round(percent)}%`}
    >
      <div className={`progress progress-${tone}`}>
        <span style={{ width: `${percent}%` }} />
      </div>
      {label ? <span className="progress-label">{label}</span> : null}
    </div>
  );
}

export function ConfidenceMeter({ score, compact = false }: { score: number; compact?: boolean }) {
  const rounded = Math.round(score * 10) / 10;
  return (
    <div className={`confidence ${compact ? "confidence-compact" : ""}`.trim()} title={`Confidence ${rounded} of 5`}>
      <div className="confidence-track">
        {[1, 2, 3, 4, 5].map((step) => (
          <span
            key={step}
            className={`confidence-seg ${score >= step - 0.5 ? "on" : score >= step - 1 ? "half" : ""}`.trim()}
          />
        ))}
      </div>
      <span className="confidence-value">{score > 0 ? rounded.toFixed(1) : "—"}</span>
    </div>
  );
}

export function Sparkline({
  values,
  width = 180,
  height = 44,
  label
}: {
  values: number[];
  width?: number;
  height?: number;
  label?: string;
}) {
  if (values.length === 0) return <div className="muted small">No history yet.</div>;
  const max = 5;
  const min = 0;
  const step = values.length > 1 ? width / (values.length - 1) : width;
  const points = values
    .map((value, index) => {
      const x = index * step;
      const y = height - ((value - min) / (max - min)) * (height - 6) - 3;
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(" ");
  return (
    <svg className="sparkline" viewBox={`0 0 ${width} ${height}`} role="img" aria-label={label ?? "Confidence trend"}>
      <line x1="0" y1={height - 3} x2={width} y2={height - 3} className="sparkline-base" />
      <line x1="0" y1={height / 2} x2={width} y2={height / 2} className="sparkline-mid" />
      <polyline points={points} fill="none" className="sparkline-line" />
      {values.length > 0 ? (
        <circle
          cx={(values.length - 1) * step}
          cy={height - (values[values.length - 1] / max) * (height - 6) - 3}
          r="2.6"
          className="sparkline-dot"
        />
      ) : null}
    </svg>
  );
}

export function Field({
  label,
  hint,
  htmlFor,
  children,
  className = ""
}: {
  label: string;
  hint?: ReactNode;
  htmlFor?: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div className={`field ${className}`.trim()}>
      <label htmlFor={htmlFor}>{label}</label>
      {children}
      {hint ? <div className="field-hint">{hint}</div> : null}
    </div>
  );
}

export function TextInput(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} className={`input ${props.className ?? ""}`.trim()} />;
}

export function TextArea(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea {...props} className={`textarea ${props.className ?? ""}`.trim()} />;
}

export function Select(props: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select {...props} className={`input select ${props.className ?? ""}`.trim()} />;
}

export function EmptyState({
  title,
  children,
  action,
  icon = "book"
}: {
  title: string;
  children?: ReactNode;
  action?: ReactNode;
  icon?: string;
}) {
  return (
    <div className="empty">
      <div className="empty-icon">
        <Icon name={icon} size={22} />
      </div>
      <h3>{title}</h3>
      {children ? <div className="empty-copy">{children}</div> : null}
      {action ? <div className="empty-action">{action}</div> : null}
    </div>
  );
}

export function Loading({ label = "Loading…" }: { label?: string }) {
  return (
    <div className="loading">
      <span className="spinner" />
      <span>{label}</span>
    </div>
  );
}

export function ErrorNote({ children }: { children: ReactNode }) {
  return (
    <div className="error-note" role="alert">
      <Icon name="warning" size={17} />
      <span>{children}</span>
    </div>
  );
}

export function InfoNote({ children, tone = "info" }: { children: ReactNode; tone?: "info" | "warn" | "success" }) {
  return <div className={`info-note info-${tone}`}>{children}</div>;
}

export function Modal({
  open,
  title,
  onClose,
  children,
  footer,
  wide
}: {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  wide?: boolean;
}) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    document.addEventListener("keydown", onKey);
    document.body.classList.add("modal-open");
    window.setTimeout(() => {
      const focusable = ref.current?.querySelector<HTMLElement>(
        "button, [href], input, select, textarea, [tabindex]:not([tabindex='-1'])"
      );
      focusable?.focus();
    }, 20);
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.classList.remove("modal-open");
    };
  }, [open, onClose]);

  if (!open) return null;
  return (
    <div className="modal-backdrop" onMouseDown={(event) => event.target === event.currentTarget && onClose()}>
      <div className={`modal ${wide ? "modal-wide" : ""}`.trim()} role="dialog" aria-modal="true" aria-label={title} ref={ref}>
        <div className="modal-head">
          <h2>{title}</h2>
          <button className="icon-btn" onClick={onClose} aria-label="Close">
            <Icon name="close" />
          </button>
        </div>
        <div className="modal-body">{children}</div>
        {footer ? <div className="modal-foot">{footer}</div> : null}
      </div>
    </div>
  );
}

export function Segmented<T extends string>({
  value,
  options,
  onChange,
  ariaLabel
}: {
  value: T;
  options: { value: T; label: string }[];
  onChange: (value: T) => void;
  ariaLabel?: string;
}) {
  return (
    <div className="segmented" role="group" aria-label={ariaLabel}>
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          className={value === option.value ? "on" : ""}
          aria-pressed={value === option.value}
          onClick={() => onChange(option.value)}
        >
          {option.label}
        </button>
      ))}
    </div>
  );
}

export function Tabs<T extends string>({
  value,
  options,
  onChange
}: {
  value: T;
  options: { value: T; label: string; count?: number }[];
  onChange: (value: T) => void;
}) {
  return (
    <div className="tabs" role="tablist">
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          role="tab"
          aria-selected={value === option.value}
          className={value === option.value ? "on" : ""}
          onClick={() => onChange(option.value)}
        >
          {option.label}
          {option.count !== undefined ? <span className="tab-count">{option.count}</span> : null}
        </button>
      ))}
    </div>
  );
}

export function ListSkeleton({ rows = 3 }: { rows?: number }) {
  return (
    <div className="skeleton-list" aria-hidden="true">
      {Array.from({ length: rows }).map((_, index) => (
        <div className="skeleton" key={index} />
      ))}
    </div>
  );
}
