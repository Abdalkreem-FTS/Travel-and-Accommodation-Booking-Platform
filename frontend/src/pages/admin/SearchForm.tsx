import { useState, type FormEvent } from 'react';
import { TextField } from '../../components/TextField';

interface SearchFormProps {
  initial: string;
  onSearch: (text: string) => void;
}

// The name search box of the admin lists. It searches when submitted (Enter or
// the button), not on every key, so typing "Amman" doesn't send five requests.
//
// Give it key={search}: when the URL's search changes (Back, a link), React
// then builds a new box with the new text instead of keeping what was typed.
export function SearchForm({ initial, onSearch }: SearchFormProps) {
  const [text, setText] = useState(initial);

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    onSearch(text.trim());
  };

  return (
    <form className="admin-search" role="search" onSubmit={handleSubmit}>
      <TextField label="Search by name" name="search" value={text} onChange={setText} />
      <button type="submit">Search</button>
    </form>
  );
}
