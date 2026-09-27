import { useQuery } from '@tanstack/react-query';
import { getAmenities } from '../../../api/hotels';

// Same limit as the backend (Hotel.MaxAmenities).
const MAX_AMENITIES = 30;

interface AmenitiesFieldProps {
  // The ids of the ticked amenities.
  value: string[];
  onChange: (ids: string[]) => void;
  errors?: string[];
}

// A checkbox per amenity in the shared catalogue.
export function AmenitiesField({ value, onChange, errors = [] }: AmenitiesFieldProps) {
  const { data, isPending, isError, refetch } = useQuery({
    queryKey: ['amenities'],
    queryFn: getAmenities,
    // The catalogue only changes with a backend release.
    staleTime: Infinity,
  });

  const toggle = (id: string, checked: boolean) => {
    onChange(checked ? [...value, id] : value.filter((ticked) => ticked !== id));
  };

  return (
    // A fieldset groups the checkboxes, and its legend is read as their shared label.
    <fieldset className="admin-fieldset">
      <legend>Amenities</legend>
      {isPending ? (
        <p role="status">Loading amenities…</p>
      ) : isError ? (
        <div role="alert" className="error-box">
          <p>We couldn't load the amenities.</p>
          <button type="button" onClick={() => refetch()}>
            Try again
          </button>
        </div>
      ) : (
        <>
          <p className="text-field-hint">
            {value.length} of at most {MAX_AMENITIES} chosen.
          </p>
          <div className="admin-checkboxes">
            {data.map((amenity) => (
              <label key={amenity.id} title={amenity.description}>
                <input
                  type="checkbox"
                  checked={value.includes(amenity.id)}
                  onChange={(event) => toggle(amenity.id, event.target.checked)}
                />{' '}
                {amenity.name}
              </label>
            ))}
          </div>
        </>
      )}
      {errors.length > 0 && (
        <ul className="text-field-errors">
          {errors.map((message) => (
            <li key={message}>{message}</li>
          ))}
        </ul>
      )}
    </fieldset>
  );
}
