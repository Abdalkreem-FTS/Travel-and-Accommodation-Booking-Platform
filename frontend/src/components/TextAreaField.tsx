import { useId } from 'react';
import './TextField.css';

interface TextAreaFieldProps {
  label: string;
  name: string;
  value: string;
  onChange: (value: string) => void;
  rows?: number;
  hint?: string;
  errors?: string[];
}

// A labelled multi-line text box. Works like TextField and shares its styles.
export function TextAreaField({ label, name, value, onChange, rows = 5, hint, errors = [] }: TextAreaFieldProps) {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const hasErrors = errors.length > 0;
  const describedBy = [hint ? hintId : null, hasErrors ? errorId : null].filter(Boolean).join(' ') || undefined;

  return (
    <div className="text-field">
      <label htmlFor={id}>{label}</label>
      <textarea
        id={id}
        name={name}
        rows={rows}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        aria-invalid={hasErrors}
        aria-describedby={describedBy}
      />
      {hint && (
        <p id={hintId} className="text-field-hint">
          {hint}
        </p>
      )}
      {hasErrors && (
        <ul id={errorId} className="text-field-errors">
          {errors.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
