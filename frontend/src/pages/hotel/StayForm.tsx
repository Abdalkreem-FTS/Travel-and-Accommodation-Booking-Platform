import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import type { Stay } from '../../api/hotels';
import type { FieldErrors } from '../../auth/formErrors';
import { TextField } from '../../components/TextField';
import { todayIso } from '../../format';

interface StayFormProps {
  // The stay currently in the URL. Only read once, to fill the form.
  initial: Stay;
  // Called with the new stay when the user clicks "Check availability".
  onSubmit: (stay: Stay) => void;
  // Where "Clear dates" goes: this hotel with no stay in the URL.
  clearHref: string;
  // Validation messages from the last room request, by field name.
  errors: FieldErrors;
}

// What the inputs hold while the user types (inputs always give text).
interface Draft {
  checkIn: string;
  checkOut: string;
  adults: string;
  children: string;
}

// The server's guest defaults, shown in the form so the user can see them.
const DEFAULT_ADULTS = 2;
const DEFAULT_CHILDREN = 0;

// Dates and guests for the room list. Works like the search page's filter form:
// it keeps its own draft, and only the button sends it up to the page.
export function StayForm({ initial, onSubmit, clearHref, errors }: StayFormProps) {
  const [draft, setDraft] = useState<Draft>(() => toDraft(initial));

  function change(field: keyof Draft) {
    return (value: string) => setDraft((old) => ({ ...old, [field]: value }));
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onSubmit(fromDraft(draft));
  }

  return (
    <form className="stay-form" onSubmit={handleSubmit} noValidate aria-label="Your stay">
      <TextField
        label="Check-in"
        name="checkIn"
        type="date"
        min={todayIso()}
        value={draft.checkIn}
        onChange={change('checkIn')}
        errors={errors.checkIn}
      />
      <TextField
        label="Check-out"
        name="checkOut"
        type="date"
        min={draft.checkIn || todayIso()}
        value={draft.checkOut}
        onChange={change('checkOut')}
        errors={errors.checkOut}
      />
      <TextField
        label="Adults"
        name="adults"
        type="number"
        min="1"
        value={draft.adults}
        onChange={change('adults')}
        errors={errors.adults}
      />
      <TextField
        label="Children"
        name="children"
        type="number"
        min="0"
        value={draft.children}
        onChange={change('children')}
        errors={errors.children}
      />
      <div className="stay-form-actions">
        <button type="submit" className="button-primary">
          Check availability
        </button>
        <Link to={clearHref}>Clear dates</Link>
      </div>
    </form>
  );
}

function toDraft(stay: Stay): Draft {
  return {
    checkIn: stay.checkIn ?? '',
    checkOut: stay.checkOut ?? '',
    adults: String(stay.adults ?? DEFAULT_ADULTS),
    children: String(stay.children ?? DEFAULT_CHILDREN),
  };
}

function fromDraft(draft: Draft): Stay {
  const number = (value: string) => (value.trim() === '' ? undefined : Number(value));
  const text = (value: string) => (value === '' ? undefined : value);

  return {
    checkIn: text(draft.checkIn),
    checkOut: text(draft.checkOut),
    adults: number(draft.adults),
    children: number(draft.children),
  };
}
