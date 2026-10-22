import { useEffect, useState } from "react";
import { api, errorMessage } from "../api";
import type { SkillCard } from "../api/types";
import { useApp } from "../context/AppContext";
import { Button, ErrorNote, Field, Modal, Select, TextArea, TextInput } from "./ui";

export function CustomQuestionModal({
  open,
  onClose,
  defaultSkillId,
  onCreated
}: {
  open: boolean;
  onClose: () => void;
  defaultSkillId?: string;
  onCreated?: () => void;
}) {
  const { notify } = useApp();
  const [skills, setSkills] = useState<SkillCard[]>([]);
  const [skillId, setSkillId] = useState(defaultSkillId ?? "");
  const [prompt, setPrompt] = useState("");
  const [options, setOptions] = useState(["", "", "", ""]);
  const [correctIndex, setCorrectIndex] = useState(0);
  const [explanation, setExplanation] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    api
      .get<SkillCard[]>("/skills")
      .then((result) => {
        setSkills(result);
        if (!defaultSkillId && result.length > 0) setSkillId(result[0].id);
      })
      .catch(() => undefined);
    setPrompt("");
    setOptions(["", "", "", ""]);
    setCorrectIndex(0);
    setExplanation("");
    setError(null);
    setSkillId(defaultSkillId ?? "");
  }, [open, defaultSkillId]);

  const save = async () => {
    const cleaned = options.map((option) => option.trim()).filter(Boolean);
    if (!skillId) return setError("Choose a skill.");
    if (!prompt.trim()) return setError("Write the question.");
    if (cleaned.length < 2) return setError("At least two options are needed.");
    if (correctIndex >= cleaned.length) return setError("Choose a valid correct option.");
    setSaving(true);
    setError(null);
    try {
      await api.post("/questions", {
        skillId,
        lessonId: null,
        prompt: prompt.trim(),
        options: cleaned,
        correctIndex,
        explanation: explanation.trim() || "Review the related lesson for the reasoning.",
        optionExplanations: []
      });
      notify("Custom question added.", "success");
      onCreated?.();
      onClose();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      open={open}
      title="Add a custom question"
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button variant="primary" onClick={save} loading={saving}>
            Save question
          </Button>
        </>
      }
    >
      <Field label="Skill" htmlFor="cq-skill">
        <Select id="cq-skill" value={skillId} onChange={(event) => setSkillId(event.target.value)}>
          <option value="">Choose a skill…</option>
          {skills.map((skill) => (
            <option key={skill.id} value={skill.id}>
              {skill.name}
            </option>
          ))}
        </Select>
      </Field>
      <Field label="Question" htmlFor="cq-prompt">
        <TextArea
          id="cq-prompt"
          rows={3}
          value={prompt}
          onChange={(event) => setPrompt(event.target.value)}
          placeholder="What is the difference between …?"
        />
      </Field>
      <div className="field">
        <span className="field-label">Options</span>
        {options.map((option, index) => (
          <div className="option-editor" key={index}>
            <input
              type="radio"
              name="cq-correct"
              checked={correctIndex === index}
              onChange={() => setCorrectIndex(index)}
              aria-label={`Mark option ${index + 1} correct`}
            />
            <TextInput
              value={option}
              onChange={(event) => {
                const next = [...options];
                next[index] = event.target.value;
                setOptions(next);
              }}
              placeholder={`Option ${String.fromCharCode(65 + index)}`}
            />
          </div>
        ))}
        <div className="field-hint">Select the radio beside the correct answer.</div>
      </div>
      <Field label="Explanation" htmlFor="cq-explanation" hint="Shown after answering. Explain why the other options are wrong.">
        <TextArea
          id="cq-explanation"
          rows={3}
          value={explanation}
          onChange={(event) => setExplanation(event.target.value)}
        />
      </Field>
      {error ? <ErrorNote>{error}</ErrorNote> : null}
    </Modal>
  );
}
