import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router';
import { createHotel, deleteHotel, getHotel, updateHotel, type HotelDetails, type HotelRequest } from '../../api/hotels';
import { ApiError } from '../../api/types';
import { serverFieldErrors, type FieldErrors } from '../../auth/formErrors';
import { LoadError } from '../../components/LoadError';
import { SelectField } from '../../components/SelectField';
import { TextAreaField } from '../../components/TextAreaField';
import { TextField } from '../../components/TextField';
import { adminErrorMessage, isStale } from './adminErrors';
import { AmenitiesField } from './hotel/AmenitiesField';
import { CityField } from './hotel/CityField';
import { GalleryField } from './hotel/GalleryField';
import { galleryRowsFrom, type GalleryRow } from './hotel/galleryRows';
import { HotelRooms } from './hotel/HotelRooms';
import './Admin.css';

const FORM_FIELDS = [
  'cityId',
  'name',
  'description',
  'owner',
  'starRating',
  'latitude',
  'longitude',
  'thumbnailUrl',
  'images',
  'amenityIds',
];

// This form's own errorCodes. The shared ones are in adminErrorMessage.
const HOTEL_ERRORS: Record<string, string> = {
  'Hotel.NameAlreadyUsedInCity': 'A hotel with that name already exists in that city.',
  'Hotel.HasRooms': 'This hotel still has rooms. Delete them before deleting the hotel.',
  'Hotel.NotFound': 'This hotel has been deleted.',
  'Hotel.AlreadyDeleted': 'This hotel has been deleted.',
};

// /admin/hotels/new?cityId=... and /admin/hotels/:id
export function AdminHotelFormPage() {
  const { id } = useParams();
  const [searchParams] = useSearchParams();

  if (id === undefined) {
    return <HotelForm hotel={null} newInCity={searchParams.get('cityId') ?? ''} />;
  }
  return <EditHotel id={id} />;
}

function EditHotel({ id }: { id: string }) {
  const { data, isPending, isError, error, refetch } = useQuery({
    // Not ['hotels', id]: that's the guests' hotel page, which also counts a visit.
    queryKey: ['hotels', id, 'edit'],
    queryFn: () => getHotel(id, { countAsView: false }),
    // Always load the latest version before editing.
    staleTime: 0,
  });

  if (isPending) {
    return <p role="status">Loading the hotel…</p>;
  }
  if (isError) {
    if (error instanceof ApiError && error.status === 404) {
      return (
        <section className="admin-page">
          <h1>Hotel not found</h1>
          <p>This hotel doesn't exist or has been deleted.</p>
          <Link to="/admin/cities">Back to cities</Link>
        </section>
      );
    }
    return <LoadError what="the hotel" error={error} onRetry={() => refetch()} />;
  }

  return (
    <>
      <HotelForm hotel={data} newInCity="" reload={async () => (await refetch()).data} />
      <HotelRooms hotelId={data.id} />
    </>
  );
}

// The plain inputs, as typed. Numbers stay strings until the form is sent,
// so a half-typed "31." isn't turned into 31 under the user's fingers.
interface Fields {
  cityId: string;
  name: string;
  description: string;
  owner: string;
  starRating: string;
  latitude: string;
  longitude: string;
  thumbnailUrl: string;
}

function fieldsFrom(hotel: HotelDetails | null, newInCity: string): Fields {
  return {
    cityId: hotel?.cityId ?? newInCity,
    name: hotel?.name ?? '',
    description: hotel?.description ?? '',
    owner: hotel?.owner ?? '',
    starRating: hotel ? String(hotel.starRating) : '',
    latitude: hotel ? String(hotel.latitude) : '',
    longitude: hotel ? String(hotel.longitude) : '',
    thumbnailUrl: hotel?.thumbnailUrl ?? '',
  };
}

interface HotelFormProps {
  // null when adding a new hotel.
  hotel: HotelDetails | null;
  // The city a new hotel starts in (from ?cityId=), or ''.
  newInCity: string;
  // Loads the hotel again from the server. Only given when editing.
  reload?: () => Promise<HotelDetails | undefined>;
}

function HotelForm({ hotel, newInCity, reload }: HotelFormProps) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [fields, setFields] = useState(() => fieldsFrom(hotel, newInCity));
  const [gallery, setGallery] = useState(() => galleryRowsFrom(hotel?.images ?? []));
  const [amenityIds, setAmenityIds] = useState(() => hotel?.amenities.map((amenity) => amenity.id) ?? []);
  const [clientErrors, setClientErrors] = useState<FieldErrors>({});

  const setField = (name: keyof Fields) => (value: string) => setFields((current) => ({ ...current, [name]: value }));

  const saveMutation = useMutation({
    mutationFn: (request: HotelRequest) =>
      hotel === null ? createHotel(request) : updateHotel(hotel.id, request, hotel.version),
    onSuccess: (saved) => {
      queryClient.setQueryData(['hotels', saved.id, 'edit'], saved);
      // The guests' pages (search, hotel page) and the city's hotel list and
      // counts may all show something else now.
      queryClient.invalidateQueries({ queryKey: ['hotels'] });
      queryClient.invalidateQueries({ queryKey: ['cities'] });
      if (hotel === null) {
        navigate(`/admin/hotels/${saved.id}`, { replace: true });
      }
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteHotel(hotel!.id, hotel!.version),
    onSuccess: () => {
      navigate(`/admin/cities/${hotel!.cityId}`, { replace: true });
      queryClient.removeQueries({ queryKey: ['hotels', hotel!.id] });
      queryClient.invalidateQueries({ queryKey: ['hotels'] });
      queryClient.invalidateQueries({ queryKey: ['cities'] });
    },
  });

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    deleteMutation.reset();

    const errors = checkFields(fields, gallery);
    setClientErrors(errors);
    if (Object.keys(errors).length > 0) {
      saveMutation.reset();
      return;
    }

    const thumbnailUrl = fields.thumbnailUrl.trim();
    saveMutation.mutate({
      cityId: fields.cityId,
      name: fields.name.trim(),
      description: fields.description.trim(),
      owner: fields.owner.trim(),
      starRating: Number(fields.starRating),
      latitude: Number(fields.latitude),
      longitude: Number(fields.longitude),
      thumbnailUrl: thumbnailUrl === '' ? null : thumbnailUrl,
      // Always the full lists: the API replaces both, so anything left out is removed.
      images: gallery.map((row) => ({ url: row.url.trim(), caption: row.caption.trim() || null })),
      amenityIds,
    });
  }

  function handleDelete() {
    if (window.confirm(`Delete ${hotel!.name}? This can't be undone here.`)) {
      saveMutation.reset();
      deleteMutation.mutate();
    }
  }

  // Throws away what was typed and shows the hotel as it is on the server now.
  async function handleReload() {
    const latest = await reload?.();
    if (latest !== undefined) {
      setFields(fieldsFrom(latest, ''));
      setGallery(galleryRowsFrom(latest.images));
      setAmenityIds(latest.amenities.map((amenity) => amenity.id));
      setClientErrors({});
      saveMutation.reset();
      deleteMutation.reset();
    }
  }

  const failed = saveMutation.error ?? deleteMutation.error;
  const formError = failed ? adminErrorMessage(failed, 'hotel', FORM_FIELDS, HOTEL_ERRORS) : null;
  const serverErrors = serverFieldErrors(saveMutation.error);
  const errorsFor = (field: string) => clientErrors[field] ?? serverErrors[field];
  const busy = saveMutation.isPending || deleteMutation.isPending;
  const backTo = hotel ? `/admin/cities/${hotel.cityId}` : fields.cityId ? `/admin/cities/${fields.cityId}` : '/admin/cities';

  return (
    <section className="admin-page admin-form-page">
      <p>
        <Link to={backTo}>← City</Link>
      </p>
      <h1>{hotel === null ? 'Add a hotel' : `Edit ${hotel.name}`}</h1>

      <form className="admin-form" onSubmit={handleSubmit} noValidate>
        {formError && (
          <div role="alert" className="error-box">
            <p>{formError}</p>
            {isStale(failed) && (
              <button type="button" onClick={handleReload}>
                Load the latest version
              </button>
            )}
          </div>
        )}
        {saveMutation.isSuccess && hotel !== null && (
          <p role="status" className="admin-saved">
            Saved.
          </p>
        )}

        <CityField value={fields.cityId} onChange={setField('cityId')} errors={errorsFor('cityId')} />
        <TextField label="Name" name="name" value={fields.name} onChange={setField('name')} errors={errorsFor('name')} />
        <TextAreaField
          label="Description"
          name="description"
          value={fields.description}
          onChange={setField('description')}
          errors={errorsFor('description')}
        />
        <TextField
          label="Owner"
          name="owner"
          value={fields.owner}
          onChange={setField('owner')}
          errors={errorsFor('owner')}
        />
        <SelectField
          label="Stars"
          value={fields.starRating}
          onChange={setField('starRating')}
          errors={errorsFor('starRating')}
        >
          <option value="">Choose</option>
          {[1, 2, 3, 4, 5].map((stars) => (
            <option key={stars} value={stars}>
              {stars === 1 ? '1 star' : `${stars} stars`}
            </option>
          ))}
        </SelectField>
        <div className="admin-pair">
          <TextField
            label="Latitude"
            name="latitude"
            hint="-90 to 90, e.g. 31.9539"
            value={fields.latitude}
            onChange={setField('latitude')}
            errors={errorsFor('latitude')}
          />
          <TextField
            label="Longitude"
            name="longitude"
            hint="-180 to 180, e.g. 35.9106"
            value={fields.longitude}
            onChange={setField('longitude')}
            errors={errorsFor('longitude')}
          />
        </div>
        <TextField
          label="Thumbnail URL"
          name="thumbnailUrl"
          hint="Optional. An https:// link to a picture."
          value={fields.thumbnailUrl}
          onChange={setField('thumbnailUrl')}
          errors={errorsFor('thumbnailUrl')}
        />
        <GalleryField rows={gallery} onChange={setGallery} errors={errorsFor('images')} />
        <AmenitiesField value={amenityIds} onChange={setAmenityIds} errors={errorsFor('amenityIds')} />

        <div className="admin-actions">
          <button type="submit" className="button-primary" disabled={busy}>
            {saveMutation.isPending ? 'Saving…' : hotel === null ? 'Add hotel' : 'Save'}
          </button>
          {hotel !== null && (
            <button type="button" className="button-danger" onClick={handleDelete} disabled={busy}>
              {deleteMutation.isPending ? 'Deleting…' : 'Delete hotel'}
            </button>
          )}
        </div>
      </form>
    </section>
  );
}

// Quick checks before sending. The server checks everything again (lengths,
// ranges, URL format), and its messages show up next to the same fields.
function checkFields(fields: Fields, gallery: GalleryRow[]): FieldErrors {
  const errors: FieldErrors = {};
  if (fields.cityId === '') errors.cityId = ['Choose a city.'];
  if (fields.name.trim() === '') errors.name = ['Name is required.'];
  if (fields.description.trim() === '') errors.description = ['Description is required.'];
  if (fields.owner.trim() === '') errors.owner = ['Owner is required.'];
  if (fields.starRating === '') errors.starRating = ['Choose a star rating.'];
  if (!isNumber(fields.latitude)) errors.latitude = ['Latitude must be a number.'];
  if (!isNumber(fields.longitude)) errors.longitude = ['Longitude must be a number.'];
  if (gallery.some((row) => row.url.trim() === '')) {
    errors.images = ['Every photo needs a URL. Remove the empty ones.'];
  }
  return errors;
}

// Number('') is 0, so an empty box has to be caught first.
function isNumber(text: string): boolean {
  return text.trim() !== '' && Number.isFinite(Number(text));
}
