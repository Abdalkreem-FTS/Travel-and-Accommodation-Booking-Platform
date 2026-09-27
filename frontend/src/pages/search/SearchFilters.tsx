import { useQuery } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';
import { getCityOptions } from '../../api/cities';
import { ROOM_TYPES, type HotelSearch, type RoomType } from '../../api/hotels';
import type { FieldErrors } from '../../auth/formErrors';
import { SelectField } from '../../components/SelectField';
import { TextField } from '../../components/TextField';
import { todayIso } from '../../format';

interface SearchFiltersProps {
  // The filters currently in the URL. Only read once, to fill the form.
  initial: HotelSearch;
  // Called with the new filters when the user clicks Search.
  onSearch: (search: HotelSearch) => void;
  // Validation messages from the last search, by field name.
  errors: FieldErrors;
}

// What the inputs hold while the user types. Inputs always give text, so numbers
// are kept as text too and only turned into numbers on Search.
interface Draft {
  cityId: string;
  checkIn: string;
  checkOut: string;
  adults: string;
  children: string;
  minPrice: string;
  maxPrice: string;
  stars: number[];
  roomType: string;
}

// The server's guest defaults, shown in the form so the user can see them.
const DEFAULT_ADULTS = 2;
const DEFAULT_CHILDREN = 0;

// The filter form. It keeps its own draft and doesn't search while the user
// types; only Search sends the draft up to the page, which puts it in the URL.
export function SearchFilters({ initial, onSearch, errors }: SearchFiltersProps) {
  // useState(() => ...) runs the function once, on the first render only. Later
  // renders keep whatever the user has typed since.
  const [draft, setDraft] = useState<Draft>(() => toDraft(initial));

  // One change handler per field: update that field, keep the rest. State is
  // never changed in place; we always make a new object ({ ...old, field: value }).
  function change(field: keyof Omit<Draft, 'stars'>) {
    return (value: string) => setDraft((old) => ({ ...old, [field]: value }));
  }

  function toggleStar(star: number) {
    setDraft((old) => ({
      ...old,
      stars: old.stars.includes(star) ? old.stars.filter((s) => s !== star) : [...old.stars, star].sort((a, b) => a - b),
    }));
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    onSearch(fromDraft(draft));
  }

  return (
    <form className="search-filters" onSubmit={handleSubmit} noValidate aria-label="Filters">
      <CitySelect value={draft.cityId} onChange={change('cityId')} />

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
      <TextField
        label="Min price / night"
        name="minPrice"
        type="number"
        min="0"
        value={draft.minPrice}
        onChange={change('minPrice')}
        errors={errors.minPrice}
      />
      <TextField
        label="Max price / night"
        name="maxPrice"
        type="number"
        min="0"
        value={draft.maxPrice}
        onChange={change('maxPrice')}
        errors={errors.maxPrice}
      />

      <SelectField label="Room type" value={draft.roomType} onChange={change('roomType')} errors={errors.roomType}>
        <option value="">Any room type</option>
        {ROOM_TYPES.map((type) => (
          <option key={type} value={type}>
            {type}
          </option>
        ))}
      </SelectField>

      {/* A fieldset groups the checkboxes; the legend is read as their shared label. */}
      <fieldset className="search-stars">
        <legend>Stars</legend>
        {[1, 2, 3, 4, 5].map((star) => (
          <label key={star}>
            <input type="checkbox" checked={draft.stars.includes(star)} onChange={() => toggleStar(star)} />
            {star}★
          </label>
        ))}
      </fieldset>

      <div className="search-actions">
        <button type="submit" className="button-primary">
          Search
        </button>
        <Link to="/hotels">Clear filters</Link>
      </div>
    </form>
  );
}

// --- The city dropdown ---------------------------------------------------------

interface CitySelectProps {
  value: string;
  onChange: (value: string) => void;
}

// Loads its own list of cities. If that fails, the rest of the form still works.
function CitySelect({ value, onChange }: CitySelectProps) {
  const { data, isPending, isError, refetch } = useQuery({
    queryKey: ['cities', 'options'],
    queryFn: getCityOptions,
    // Cities rarely change; one load per visit is plenty.
    staleTime: 5 * 60_000,
  });

  const hint = isPending
    ? 'Loading cities…'
    : isError
      ? "We couldn't load the cities."
      : data.totalCount > data.items.length
        ? `Only the first ${data.items.length} of ${data.totalCount} cities are listed.`
        : undefined;

  // The URL may name a city that isn't in the list (still loading, failed to
  // load, or past the first 50). A <select> whose value matches no option shows
  // the first option instead, which would read "Any city" while the results are
  // filtered. So that city gets an option of its own.
  const unlisted = value !== '' && !data?.items.some((city) => city.id === value);

  return (
    <div>
      <SelectField label="City" value={value} onChange={onChange} hint={hint}>
        <option value="">Any city</option>
        {unlisted && <option value={value}>The chosen city</option>}
        {data?.items.map((city) => (
          <option key={city.id} value={city.id}>
            {city.name}, {city.country}
          </option>
        ))}
      </SelectField>
      {isError && (
        <button type="button" className="search-retry" onClick={() => refetch()}>
          Try again
        </button>
      )}
    </div>
  );
}

// --- Draft <-> search ------------------------------------------------------------

function toDraft(search: HotelSearch): Draft {
  return {
    cityId: search.cityId ?? '',
    checkIn: search.checkIn ?? '',
    checkOut: search.checkOut ?? '',
    adults: String(search.adults ?? DEFAULT_ADULTS),
    children: String(search.children ?? DEFAULT_CHILDREN),
    minPrice: search.minPrice?.toString() ?? '',
    maxPrice: search.maxPrice?.toString() ?? '',
    stars: search.stars,
    roomType: search.roomType ?? '',
  };
}

// An empty box means "no filter". A number input gives '' when what's typed
// isn't a number, so every number here is a real one.
function fromDraft(draft: Draft): HotelSearch {
  const number = (value: string) => (value.trim() === '' ? undefined : Number(value));
  const text = (value: string) => (value === '' ? undefined : value);

  return {
    cityId: text(draft.cityId),
    checkIn: text(draft.checkIn),
    checkOut: text(draft.checkOut),
    adults: number(draft.adults),
    children: number(draft.children),
    minPrice: number(draft.minPrice),
    maxPrice: number(draft.maxPrice),
    stars: draft.stars,
    roomType: text(draft.roomType) as RoomType | undefined,
  };
}
