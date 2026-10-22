import { Link } from "react-router-dom";
import { EmptyState } from "../components/ui";

export function NotFoundPage() {
  return (
    <div className="page">
      <EmptyState
        title="That page is not here"
        icon="search"
        action={<Link to="/" className="btn btn-primary btn-md">Back to Today</Link>}
      >
        The link may be old, or the lesson may have been renamed. Try the search shortcut with Ctrl / ⌘ + K.
      </EmptyState>
    </div>
  );
}
