import { Route, Routes } from "react-router-dom";
import { AppProvider } from "./context/AppContext";
import { Layout } from "./components/Layout";
import { TodayPage } from "./pages/Today";
import { LessonPage } from "./pages/Lesson";
import { SkillsPage } from "./pages/Skills";
import { SkillDetailPage } from "./pages/SkillDetail";
import { PlanPage } from "./pages/Plan";
import { SchedulePage } from "./pages/Schedule";
import { ReviewPage } from "./pages/Review";
import { NotesPage } from "./pages/Notes";
import { ProgressPage } from "./pages/Progress";
import { ReflectionsPage } from "./pages/Reflections";
import { SettingsPage } from "./pages/Settings";
import { ResourcesPage } from "./pages/Resources";
import { NotFoundPage } from "./pages/NotFound";

export default function App() {
  return (
    <AppProvider>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<TodayPage />} />
          <Route path="lesson/:id" element={<LessonPage />} />
          <Route path="skills" element={<SkillsPage />} />
          <Route path="skills/:id" element={<SkillDetailPage />} />
          <Route path="plan" element={<PlanPage />} />
          <Route path="schedule" element={<SchedulePage />} />
          <Route path="review" element={<ReviewPage />} />
          <Route path="notes" element={<NotesPage />} />
          <Route path="progress" element={<ProgressPage />} />
          <Route path="reflections" element={<ReflectionsPage />} />
          <Route path="settings" element={<SettingsPage />} />
          <Route path="resources" element={<ResourcesPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </AppProvider>
  );
}
