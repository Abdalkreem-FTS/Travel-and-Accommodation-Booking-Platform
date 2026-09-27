import { useQuery } from '@tanstack/react-query';
import { getCityOptions } from '../../../api/cities';
import { SelectField } from '../../../components/SelectField';

interface CityFieldProps {
  value: string;
  onChange: (cityId: string) => void;
  errors?: string[];
}

// The hotel's city. Choosing another one moves the hotel there.
export function CityField({ value, onChange, errors }: CityFieldProps) {
  // Same list and key as the search page's city dropdown, so they share one copy.
  const { data, isPending, isError, refetch } = useQuery({
    queryKey: ['cities', 'options'],
    queryFn: getCityOptions,
    staleTime: 5 * 60_000,
  });

  const hint = isPending
    ? 'Loading cities…'
    : isError
      ? "We couldn't load the cities."
      : data.totalCount > data.items.length
        ? `Only the first ${data.items.length} of ${data.totalCount} cities are listed.`
        : undefined;

  // The hotel's city may be missing from the list (still loading, or past the
  // first 50). A <select> whose value matches no option shows the first
  // option instead, which would quietly move the hotel on save. So the
  // current city always gets an option.
  const unlisted = value !== '' && !data?.items.some((city) => city.id === value);

  return (
    <div>
      <SelectField label="City" value={value} onChange={onChange} hint={hint} errors={errors}>
        <option value="">Choose a city</option>
        {unlisted && <option value={value}>The current city</option>}
        {data?.items.map((city) => (
          <option key={city.id} value={city.id}>
            {city.name}, {city.country}
          </option>
        ))}
      </SelectField>
      {isError && (
        <button type="button" onClick={() => refetch()}>
          Try again
        </button>
      )}
    </div>
  );
}
