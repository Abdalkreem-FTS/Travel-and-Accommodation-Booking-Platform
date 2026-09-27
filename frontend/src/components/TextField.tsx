import { useId } from 'react';
import './TextField.css';

// Props are a component's parameters, like a C# method's arguments. The parent
// passes them in <TextField label="Email" ... /> and this component only reads them.
interface TextFieldProps {
  label: string;
  name: string;
  value: string;
  onChange: (value: string) => void;
  type?: 'text' | 'email' | 'password' | 'date' | 'number';
  autoComplete?: string;
  // Lowest and highest allowed value, for date and number inputs. Only a hint
  // to the browser's picker; the server still checks the value.
  min?: string;
  max?: string;
  hint?: string;
  errors?: string[];
}

// A labelled input with an optional hint and its error messages underneath.
export function TextField({
  label,
  name,
  value,
  onChange,
  type = 'text',
  autoComplete,
  min,
  max,
  hint,
  errors = [],
}: TextFieldProps) {
  // useId gives each TextField its own id, so the <label> and the error text
  // can point at the right <input> even when there are several on the page.
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const hasErrors = errors.length > 0;

  // aria-describedby makes screen readers read the hint and errors along with the label.
  const describedBy = [hint ? hintId : null, hasErrors ? errorId : null].filter(Boolean).join(' ') || undefined;

  return (
    <div className="text-field">
      <label htmlFor={id}>{label}</label>
      <input
        id={id}
        name={name}
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        autoComplete={autoComplete}
        min={min}
        max={max}
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
