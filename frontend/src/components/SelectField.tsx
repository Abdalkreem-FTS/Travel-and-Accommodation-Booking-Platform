import { useId, type ReactNode } from 'react';
import './TextField.css';

interface SelectFieldProps {
  label: string;
  value: string;
  onChange: (value: string) => void;
  hint?: string;
  errors?: string[];
  // The <option> elements. `children` is whatever is written between the
  // opening and closing tags: <SelectField ...>here</SelectField>.
  children: ReactNode;
}

// A labelled dropdown with an optional hint and its error messages underneath.
// Works like TextField and shares its styles.
export function SelectField({ label, value, onChange, hint, errors = [], children }: SelectFieldProps) {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const hasErrors = errors.length > 0;
  const describedBy = [hint ? hintId : null, hasErrors ? errorId : null].filter(Boolean).join(' ') || undefined;

  return (
    <div className="text-field">
      <label htmlFor={id}>{label}</label>
      <select
        id={id}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        aria-invalid={hasErrors}
        aria-describedby={describedBy}
      >
        {children}
      </select>
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
