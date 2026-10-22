# Lesson file format

Curriculum lives in the `curriculum/` folder at the repository root.

```
curriculum/
├── manifest.json          areas, skills, and the DSA topic order
├── csharp/
│   ├── 01-csharp-01-dotnet-csharp-runtime.json
│   └── ...
└── dsa/
    ├── 01-dsa-01-big-o.json
    └── ...
```

Each skill gets its own folder. You can add subfolders (the DSA folder is flat in the starter set, but the loader scans recursively). The app reads every `*.json` file except `manifest.json` on startup, and whenever you press **Reload files** in Settings.

## manifest.json

```json
{
  "areas": [{ "name": "Languages", "order": 1 }],
  "skills": [
    {
      "id": "csharp",
      "name": "C#",
      "area": "Languages",
      "description": "The language you will use every day…",
      "dsa": false,
      "dsaTopics": []
    },
    {
      "id": "dsa",
      "name": "Data Structures & Algorithms",
      "area": "Computer Science",
      "description": "…",
      "dsa": true,
      "dsaTopics": ["big-o", "arrays", "strings"]
    }
  ]
}
```

- `id` is the stable identifier used by lessons, progress, notes, and resources. Do not change it after you start studying.
- `area` groups skills on the Skills screen.
- `dsa: true` enables the strict topic sequence and the Practice section. `dsaTopics` is the canonical order. A topic is considered complete when every lesson with that `topic` is completed (or when you use the explicit skip action).
- Skills created inside the app are stored in the database and are not added to this manifest automatically.

## A lesson file

```json
{
  "id": "csharp-02-values-and-references",
  "skill": "csharp",
  "topic": "fundamentals",
  "order": 2,
  "title": "Values, references, and what assignment copies",
  "summary": "One sentence shown on the Today card.",
  "estimatedMinutes": 35,
  "theory": "Markdown. Use fenced code blocks with a language, for example ```csharp.",
  "workedExample": {
    "title": "Predict the output",
    "markdown": "Markdown introducing the example.",
    "steps": ["First step…", "Second step…"]
  },
  "checkQuestions": [
    {
      "prompt": "What is copied when a reference-type variable is assigned?",
      "options": ["The object", "The reference", "A deep clone"],
      "correctIndex": 1,
      "explanation": "Assignment copies the reference; both variables point at the same object.",
      "optionExplanations": [
        "Objects are not copied on assignment.",
        "Correct.",
        "Deep cloning requires an explicit operation."
      ]
    }
  ],
  "explainPrompt": "Explain the idea in your own words.",
  "task": null,
  "bugFix": null,
  "recallQuestions": [],
  "dsa": null
}
```

### Required fields

| Field | Notes |
|---|---|
| `id` | Globally unique. Keep it stable. |
| `skill` | Must match a `skills[].id` in the manifest. |
| `order` | Positive integer. This is the study order within the skill. |
| `title`, `summary` | Plain strings. |
| `theory` | Markdown. The main reading step. |
| `checkQuestions[].options` | At least two. `correctIndex` is zero-based. |
| `checkQuestions[].explanation` | Shown after answering. |
| `checkQuestions[].optionExplanations` | Optional, but recommended: explain every option, including wrong ones. |

### Optional pieces

- **`workedExample`** adds the worked-example step.
- **`task`** adds a task step. Only add one when the lesson needs practice; most theory lessons should not have it.
  ```json
  {
    "title": "Implement TryParsePositive",
    "prompt": "Markdown prompt.",
    "language": "csharp",
    "starterCode": "…",
    "solution": "…",
    "requirements": ["Done means…"]
  }
  ```
  The solution is kept on the server and is not shown in the lesson UI.
- **`bugFix`** enables the occasional fix-the-bug step. Visibility is controlled by Settings → fix-the-bug frequency, deterministic per lesson, and always skippable.
  ```json
  {
    "title": "The missing null check",
    "prompt": "Markdown prompt.",
    "language": "csharp",
    "brokenCode": "…",
    "explanation": "What is wrong and why."
  }
  ```
- **`recallQuestions`** add extra seeded questions to the warm-up pool. Same shape as check questions.
- **`dsa`** is required for DSA lessons:
  ```json
  {
    "topic": "big-o",
    "complexityQuestions": [
      {
        "prompt": "State the time and space complexity.",
        "code": "int Sum(int[] values) { … }",
        "language": "csharp",
        "answer": "Time: O(n), Space: O(1)",
        "explanation": "Why."
      }
    ],
    "practice": [
      {
        "title": "Contains a duplicate",
        "prompt": "Markdown problem statement.",
        "hints": ["First hint.", "Second hint."],
        "solution": "Markdown solution."
      }
    ]
  }
  ```
  Practice problems appear only in the optional Practice section of DSA lessons. Hints are revealed one at a time.

## Markdown

Theory, worked examples, prompts, and solutions are rendered as GitHub-flavoured Markdown. Supported code fence languages include `csharp`, `typescript`, `javascript`, `bash`, `json`, `sql`, `xml`, and `html`.

## Validation

Invalid files are skipped and reported in the server log and in Settings → Curriculum folder. A lesson is rejected when:

- JSON cannot be parsed;
- `id`, `skill`, `title`, `theory`, or `order` is missing;
- `skill` is not declared in the manifest;
- there are no check questions, options are missing, or `correctIndex` is out of range;
- the same `id` appears twice.

## How scheduling uses the files

- Each track studies its lessons in `order`.
- One lesson per track is scheduled per study day; the estimate sets the time slice.
- DSA lessons are ordered by topic and then by `order`. Later topics are reachable only after every earlier topic's lessons are complete, or after an explicit topic override.
- Completed lessons are excluded from future scheduling. Deleting a lesson file does not delete its progress history.
