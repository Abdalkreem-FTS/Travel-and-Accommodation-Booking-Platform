import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { createCity, deleteCity, getCity, updateCity, type City, type CityRequest } from '../../api/cities';
import { ApiError } from '../../api/types';
import { serverFieldErrors, type FieldErrors } from '../../auth/formErrors';
import { LoadError } from '../../components/LoadError';
import { TextField } from '../../components/TextField';
import { adminErrorMessage, isStale } from './adminErrors';
import { CityHotels } from './city/CityHotels';
import './Admin.css';

const FORM_FIELDS = ['name', 'country', 'postOffice', 'thumbnailUrl'];

// /admin/cities/new and /admin/cities/:id
//
// One form for both: empty to add a city, filled in to edit one.
export function AdminCityFormPage() {
  const { id } = useParams();
  return id === undefined ? <CityForm city={null} /> : <EditCity id={id} />;
}

function EditCity({ id }: { id: string }) {
  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: ['cities', id],
    queryFn: () => getCity(id),
    // Always load the latest version before editing. An older copy from the
    // cache would make the save fail with "someone else changed it".
    staleTime: 0,
  });

  if (isPending) {
    return <p role="status">Loading the city…</p>;
  }
  if (isError) {
    if (error instanceof ApiError && error.status === 404) {
      return (
        <section className="admin-page">
          <h1>City not found</h1>
          <p>This city doesn't exist or has been deleted.</p>
          <Link to="/admin/cities">Back to cities</Link>
        </section>
      );
    }
    return <LoadError what="the city" error={error} onRetry={() => refetch()} />;
  }

  // refetch() resolves to the query's new state; the form only needs the city.
  return (
    <>
      <CityForm city={data} reload={async () => (await refetch()).data} />
      <CityHotels cityId={data.id} />
    </>
  );
}

// The form's inputs. Everything is a string while typing; the thumbnail is
// turned into null when left empty.
type Fields = Record<keyof CityRequest, string>;

function fieldsFrom(city: City | null): Fields {
  return {
    name: city?.name ?? '',
    country: city?.country ?? '',
    postOffice: city?.postOffice ?? '',
    thumbnailUrl: city?.thumbnailUrl ?? '',
  };
}

interface CityFormProps {
  // null when adding a new city.
  city: City | null;
  // Loads the city again from the server. Only given when editing.
  reload?: () => Promise<City | undefined>;
}

function CityForm({ city, reload }: CityFormProps) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [fields, setFields] = useState(() => fieldsFrom(city));
  const [clientErrors, setClientErrors] = useState<FieldErrors>({});

  // One onChange for every input: copy the object with one field replaced.
  // State is never changed in place, so React sees a new object and re-renders.
  const setField = (name: keyof Fields) => (value: string) => setFields((current) => ({ ...current, [name]: value }));

  const saveMutation = useMutation({
    mutationFn: (request: CityRequest) =>
      city === null ? createCity(request) : updateCity(city.id, request, city.version),
    onSuccess: (saved) => {
      // Put the saved city (with its new version) straight into the cache, so
      // the next save sends the right If-Match without loading it again.
      queryClient.setQueryData(['cities', saved.id], saved);
      // Lists that show this city (admin list, dropdowns, trending) are now out of date.
      queryClient.invalidateQueries({ queryKey: ['cities'], predicate: (query) => query.queryKey[1] !== saved.id });
      if (city === null) {
        navigate(`/admin/cities/${saved.id}`, { replace: true });
      }
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteCity(city!.id, city!.version),
    onSuccess: () => {
      navigate('/admin/cities', { replace: true });
      queryClient.removeQueries({ queryKey: ['cities', city!.id], exact: true });
      queryClient.invalidateQueries({ queryKey: ['cities'] });
    },
  });

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    deleteMutation.reset();

    const errors: FieldErrors = {};
    if (fields.name.trim() === '') errors.name = ['Name is required.'];
    if (fields.country.trim() === '') errors.country = ['Country is required.'];
    if (fields.postOffice.trim() === '') errors.postOffice = ['Post office is required.'];
    setClientErrors(errors);

    if (Object.keys(errors).length > 0) {
      saveMutation.reset();
      return;
    }
    const thumbnailUrl = fields.thumbnailUrl.trim();
    saveMutation.mutate({
      name: fields.name.trim(),
      country: fields.country.trim(),
      postOffice: fields.postOffice.trim(),
      thumbnailUrl: thumbnailUrl === '' ? null : thumbnailUrl,
    });
  }

  function handleDelete() {
    if (window.confirm(`Delete ${city!.name}? This can't be undone here.`)) {
      saveMutation.reset();
      deleteMutation.mutate();
    }
  }

  // Throws away what was typed and shows the city as it is on the server now.
  async function handleReload() {
    const latest = await reload?.();
    if (latest !== undefined) {
      setFields(fieldsFrom(latest));
      setClientErrors({});
      saveMutation.reset();
      deleteMutation.reset();
    }
  }

  const failed = saveMutation.error ?? deleteMutation.error;
  const formError = failed ? adminErrorMessage(failed, 'city', FORM_FIELDS, CITY_ERRORS) : null;
  const serverErrors = serverFieldErrors(saveMutation.error);
  const stale = isStale(failed);
  const busy = saveMutation.isPending || deleteMutation.isPending;

  return (
    <section className="admin-page admin-form-page">
      <p>
        <Link to="/admin/cities">← Cities</Link>
      </p>
      <h1>{city === null ? 'Add a city' : `Edit ${city.name}`}</h1>

      <form className="admin-form" onSubmit={handleSubmit} noValidate>
        {formError && (
          <div role="alert" className="error-box">
            <p>{formError}</p>
            {stale && (
              <button type="button" onClick={handleReload}>
                Load the latest version
              </button>
            )}
          </div>
        )}
        {saveMutation.isSuccess && city !== null && (
          <p role="status" className="admin-saved">
            Saved.
          </p>
        )}

        <TextField
          label="Name"
          name="name"
          value={fields.name}
          onChange={setField('name')}
          errors={clientErrors.name ?? serverErrors.name}
        />
        <TextField
          label="Country code"
          name="country"
          hint="Two letters, for example JO."
          value={fields.country}
          onChange={setField('country')}
          errors={clientErrors.country ?? serverErrors.country}
        />
        <TextField
          label="Post office"
          name="postOffice"
          value={fields.postOffice}
          onChange={setField('postOffice')}
          errors={clientErrors.postOffice ?? serverErrors.postOffice}
        />
        <TextField
          label="Thumbnail URL"
          name="thumbnailUrl"
          hint="Optional. An https:// link to a picture."
          value={fields.thumbnailUrl}
          onChange={setField('thumbnailUrl')}
          errors={serverErrors.thumbnailUrl}
        />

        <div className="admin-actions">
          <button type="submit" className="button-primary" disabled={busy}>
            {saveMutation.isPending ? 'Saving…' : city === null ? 'Add city' : 'Save'}
          </button>
          {city !== null && (
            <button type="button" className="button-danger" onClick={handleDelete} disabled={busy}>
              {deleteMutation.isPending ? 'Deleting…' : 'Delete city'}
            </button>
          )}
        </div>
      </form>
    </section>
  );
}

// This form's own errorCodes. The shared ones are in adminErrorMessage.
const CITY_ERRORS: Record<string, string> = {
  'City.NameAlreadyUsedInCountry': 'A city with that name already exists in that country.',
  'City.HasHotels': 'This city still has hotels. Delete or move them before deleting the city.',
  'City.NotFound': 'This city has been deleted.',
  'City.AlreadyDeleted': 'This city has been deleted.',
};
