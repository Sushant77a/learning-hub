export type TrackState = "active" | "paused" | "completed" | "notstarted";
export type LessonState = "notstarted" | "inprogress" | "completed";

export interface AiStatus {
  configured: boolean;
  provider: string;
  model: string;
  baseUrl?: string | null;
  keyStatus: string;
  message: string;
}

export interface Settings {
  dailyMinutes: number;
  restWeekdays: string[];
  startDate: string;
  bugFixFrequency: number;
  aiProvider?: string | null;
  theme: string;
  ai: AiStatus;
  counts: {
    skills: number;
    lessons: number;
    notes: number;
    resources: number;
    mistakes: number;
  };
  activeTracks: number;
  curriculumRoot: string;
  curriculumErrors: string[];
}

export interface Detour {
  timeBoxId: number;
  trackId: number;
  skillId: string;
  skillName: string;
  label: string;
  startDate: string;
  endDate: string;
  dayNumber: number;
  totalDays: number;
  mode: "pause" | "parallel";
  detourPercent: number;
  resumeOn?: string | null;
  decisionDue: boolean;
  pausedSkillNames: string[];
}

export interface TodayCard {
  trackId: number;
  skillId: string;
  skillName: string;
  lessonId?: string | null;
  title?: string | null;
  summary?: string | null;
  minutes: number;
  sharePercent: number;
  estimatedMinutes: number;
  lessonStatus: LessonState;
  currentStep: number;
  isDsa: boolean;
  topic?: string | null;
  isDetour: boolean;
  needsCurriculum: boolean;
  hasTask: boolean;
  learnedNote?: string | null;
}

export interface PausedTrack {
  trackId: number;
  skillId: string;
  skillName: string;
  pausedReason?: string | null;
  resumeOn?: string | null;
  lastPractised?: string | null;
}

export interface Today {
  date: string;
  dateLabel: string;
  isRestDay: boolean;
  restNote?: string | null;
  cards: TodayCard[];
  pausedTracks: PausedTrack[];
  detour?: Detour | null;
  timeBoxDecisionDue: boolean;
  dueReviewCount: number;
  reflectionDue: boolean;
  reflectionWeekStart: string;
  warnings: string[];
  missedDaysShifted: number;
  dailyMinutes: number;
  hasActiveTracks: boolean;
}

export interface PlanItem {
  trackId: number;
  skillId: string;
  skillName: string;
  lessonId: string;
  lessonTitle: string;
  date: string;
  minutes: number;
  status: "planned" | "done" | "missed";
  isDetour: boolean;
}

export interface PlanTrack {
  trackId: number;
  skillId: string;
  skillName: string;
  state: TrackState;
  sharePercent: number;
  dailyMinutes: number;
  lessonsDone: number;
  lessonsTotal: number;
  finishDate?: string | null;
  firstLessonDate?: string | null;
  isDetour: boolean;
  resumeOn?: string | null;
}

export interface PlanSnapshot {
  today: string;
  dailyMinutes: number;
  startDate: string;
  restWeekdays: string[];
  restDates: string[];
  entries: PlanItem[];
  tracks: PlanTrack[];
  detour?: Detour | null;
  timeBoxes: Detour[];
  missedDaysShifted: number;
  changes: string[];
}

export interface WeakTopic {
  name: string;
  accuracy: number;
  attempts: number;
}

export interface SkillCard {
  id: string;
  name: string;
  area: string;
  description: string;
  isCustom: boolean;
  isDsa: boolean;
  trackId?: number | null;
  trackState: TrackState;
  lessonsDone: number;
  lessonsTotal: number;
  lastPractised?: string | null;
  daysSincePractice: number;
  confidence: number;
  selfRating?: number | null;
  accuracy?: number | null;
  taskScore?: number | null;
  needsRefresh: boolean;
  weakSubtopics: WeakTopic[];
  currentDsaTopic?: string | null;
}

export interface SkillLesson {
  id: string;
  order: number;
  title: string;
  summary: string;
  topic?: string | null;
  estimatedMinutes: number;
  status: LessonState;
  confidence?: number | null;
  completedAt?: string | null;
  locked: boolean;
}

export interface DsaTopic {
  name: string;
  completed: boolean;
  lessons: number;
  lessonsDone: number;
}

export interface ConfidencePoint {
  date: string;
  score: number;
}

export interface SkillDetail {
  card: SkillCard;
  lessons: SkillLesson[];
  resources: Resource[];
  notes: Note[];
  confidenceHistory: ConfidencePoint[];
  dsaTopics: DsaTopic[];
}

export interface Question {
  id: number;
  lessonId?: string | null;
  lessonTitle: string;
  skillId: string;
  skillName: string;
  topic?: string | null;
  kind: "check" | "complexity" | "custom";
  prompt: string;
  options: string[];
  code?: string | null;
  fromPausedTrack: boolean;
}

export interface LessonQuestion {
  id: number;
  kind: "check" | "complexity" | "custom";
  prompt: string;
  options: string[];
  code?: string | null;
}

export interface WorkedExample {
  title: string;
  markdown: string;
  steps: string[];
}

export interface LessonTask {
  title: string;
  prompt: string;
  language: string;
  starterCode: string;
  requirements: string[];
}

export interface BugFix {
  title: string;
  prompt: string;
  language: string;
  brokenCode: string;
}

export interface PracticeProblem {
  title: string;
  prompt: string;
  hints: string[];
  solution: string;
}

export interface LessonDetail {
  id: string;
  skillId: string;
  skillName: string;
  topic?: string | null;
  order: number;
  title: string;
  summary: string;
  estimatedMinutes: number;
  isDsa: boolean;
  theory: string;
  workedExample?: WorkedExample | null;
  explainPrompt: string;
  task?: LessonTask | null;
  bugFix?: BugFix | null;
  practice: PracticeProblem[];
  questions: LessonQuestion[];
  warmup: Question[];
  track: {
    trackId: number;
    skillId: string;
    skillName: string;
    state: string;
  };
  progress: {
    status: LessonState;
    currentStep: number;
    learnedNote?: string | null;
    confidenceRating?: number | null;
    explainBack?: string | null;
    explainGrade?: string | null;
    taskDone: boolean;
    bugFixDone: boolean;
  };
  includeBugFix: boolean;
  notes: Note[];
  resources: Resource[];
  previousLessonId?: string | null;
  nextLessonId?: string | null;
}

export interface AnswerResult {
  correct: boolean;
  correctIndex: number;
  explanation: string;
  optionExplanations: string[];
  answer?: string | null;
  mistakeId?: number | null;
  nextReviewDate: string;
  intervalDays: number;
  ease: number;
}

export interface Note {
  id: number;
  skillId?: string | null;
  lessonId?: string | null;
  title: string;
  body: string;
  createdAt: string;
  updatedAt: string;
}

export interface Resource {
  id: number;
  skillId?: string | null;
  skillName?: string | null;
  lessonId?: string | null;
  lessonTitle?: string | null;
  title: string;
  url: string;
  type: "article" | "video" | "docs" | "other";
  notes?: string | null;
  status: "unread" | "reading" | "read" | "watched";
  updatedAt: string;
}

export interface Mistake {
  id: number;
  skillId: string;
  skillName: string;
  lessonId?: string | null;
  lessonTitle?: string | null;
  kind: "wronganswer" | "failedtask" | "bugfix";
  reasonTag: string;
  note?: string | null;
  createdAt: string;
}

export interface MistakePattern {
  reason: string;
  count: number;
  skillId?: string | null;
  skillName?: string | null;
}

export interface WeeklySummary {
  weekStart: string;
  weekEnd: string;
  lessonsDone: number;
  lessonsPlanned: number;
  minutesDone: number;
  minutesPlanned: number;
  studyDays: number;
  restDays: number;
  reviewsAnswered: number;
  accuracy: number;
  mistakesLogged: number;
  mistakePatterns: MistakePattern[];
  confidenceChanges: {
    skillId: string;
    skillName: string;
    from?: number | null;
    to?: number | null;
    change: number;
  }[];
}

export interface ProgressOverview {
  streak: { current: number; longest: number; studyDates: string[] };
  thisWeek: WeeklySummary;
  totalLessonsDone: number;
  totalMinutes: number;
  overallAccuracy: number;
  totalMistakes: number;
  mistakePatterns: MistakePattern[];
  skills: {
    skillId: string;
    skillName: string;
    lessonsDone: number;
    lessonsTotal: number;
    confidence: number;
    history: ConfidencePoint[];
  }[];
  last14Days: { date: string; minutes: number }[];
}

export interface ReflectionSuggestion {
  id: number;
  reflectionId: number;
  text: string;
  rationale?: string | null;
  state: "Pending" | "Accepted" | "Declined";
}

export interface Reflection {
  id: number;
  weekStart: string;
  weekEnd: string;
  wentWell: string;
  wasHard: string;
  toChange: string;
  createdAt: string;
  suggestions: ReflectionSuggestion[];
  summary: WeeklySummary;
}

export interface AiGrade {
  score: number;
  verdict: string;
  strengths: string[];
  gaps: string[];
  review: string[];
  raw?: string | null;
}

export interface SearchHit {
  type: string;
  title: string;
  snippet: string;
  meta: string;
  route: string;
}

export interface SearchResponse {
  query: string;
  total: number;
  groups: { type: string; label: string; results: SearchHit[] }[];
}

export interface TimeBox {
  id: number;
  skillId: string;
  skillName: string;
  trackId: number;
  trackState?: string;
  startDate: string;
  endDate: string;
  mode: "pause" | "parallel";
  detourPercent: number;
  status: string;
  label: string;
  pausedTrackIds: number[];
}

export interface GoogleStatus {
  configured: boolean;
  connected: boolean;
  email?: string | null;
  message: string;
}
