import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { ROOM_TYPES, type RoomType } from '../../api/hotels';
import { createRoom, deleteRoom, getRoom, updateRoom, type Room, type RoomRequest } from '../../api/rooms';
import { ApiError } from '../../api/types';
import { serverFieldErrors, type FieldErrors } from '../../auth/formErrors';
import { LoadError } from '../../components/LoadError';
import { SelectField } from '../../components/SelectField';
import { TextField } from '../../components/TextField';
import { adminErrorMessage, isStale } from './adminErrors';
import { RoomDeals } from './room/RoomDeals';
import './Admin.css';

const FORM_FIELDS = ['number', 'type', 'adults', 'children', 'basePrice', 'currency'];

// This form's own errorCodes. The shared ones are in adminErrorMessage.
const ROOM_ERRORS: Record<string, string> = {
  'Room.NumberAlreadyUsedInHotel': 'This hotel already has a room with that number.',
  'Room.HasFutureBookings':
    'Guests have booked this room for today or later. Cancel those bookings before deleting it.',
  'Room.NotFound': 'This room has been deleted.',
  'Room.AlreadyDeleted': 'This room has been deleted.',
  'Hotel.NotFound': 'The hotel has been deleted, so no room can be added to it.',
};

// /admin/hotels/:hotelId/rooms/new and /admin/rooms/:id
export function AdminRoomFormPage() {
  const { id, hotelId } = useParams();

  if (id === undefined) {
    return <RoomForm room={null} hotelId={hotelId!} />;
  }
  return <EditRoom id={id} />;
}

function EditRoom({ id }: { id: string }) {
  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: ['rooms', id],
    queryFn: () => getRoom(id),
    // Always load the latest version before editing.
    staleTime: 0,
  });

  if (isPending) {
    return <p role="status">Loading the room…</p>;
  }
  if (isError) {
    if (error instanceof ApiError && error.status === 404) {
      return (
        <section className="admin-page">
          <h1>Room not found</h1>
          <p>This room doesn't exist or has been deleted.</p>
          <Link to="/admin/cities">Back to cities</Link>
        </section>
      );
    }
    return <LoadError what="the room" error={error} onRetry={() => refetch()} />;
  }

  return (
    <>
      <RoomForm room={data} hotelId={data.hotelId} reload={async () => (await refetch()).data} />
      <RoomDeals roomId={data.id} />
    </>
  );
}

// The inputs as typed. Numbers stay strings until the form is sent.
interface Fields {
  number: string;
  type: RoomType | '';
  adults: string;
  children: string;
  basePrice: string;
  currency: string;
}

function fieldsFrom(room: Room | null): Fields {
  return {
    number: room?.number ?? '',
    type: (room?.type as RoomType | undefined) ?? '',
    adults: room ? String(room.adults) : '',
    children: room ? String(room.children) : '',
    basePrice: room ? String(room.basePrice) : '',
    currency: room?.currency ?? '',
  };
}

interface RoomFormProps {
  // null when adding a new room.
  room: Room | null;
  hotelId: string;
  // Loads the room again from the server. Only given when editing.
  reload?: () => Promise<Room | undefined>;
}

function RoomForm({ room, hotelId, reload }: RoomFormProps) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [fields, setFields] = useState(() => fieldsFrom(room));
  const [clientErrors, setClientErrors] = useState<FieldErrors>({});

  const setField = (name: keyof Fields) => (value: string) => setFields((current) => ({ ...current, [name]: value }));

  // A room's price shows up on the guests' search, hotel page, deals and cart
  // names, and its count on the admin city page. Mark all of it out of date.
  const refreshLists = () => {
    queryClient.invalidateQueries({ queryKey: ['hotels'] });
    queryClient.invalidateQueries({ queryKey: ['cities'] });
    queryClient.invalidateQueries({ queryKey: ['deals'] });
  };

  const saveMutation = useMutation({
    mutationFn: (request: RoomRequest) =>
      room === null ? createRoom(hotelId, request) : updateRoom(room.id, request, room.version),
    onSuccess: (saved) => {
      queryClient.setQueryData(['rooms', saved.id], saved);
      refreshLists();
      if (room === null) {
        navigate(`/admin/rooms/${saved.id}`, { replace: true });
      }
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteRoom(room!.id, room!.version),
    onSuccess: () => {
      navigate(`/admin/hotels/${hotelId}`, { replace: true });
      queryClient.removeQueries({ queryKey: ['rooms', room!.id] });
      refreshLists();
    },
  });

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    deleteMutation.reset();

    const errors = checkFields(fields);
    setClientErrors(errors);
    // `fields.type === ''` is already an error above; checking it again tells
    // TypeScript that below this line `type` is a real RoomType.
    if (Object.keys(errors).length > 0 || fields.type === '') {
      saveMutation.reset();
      return;
    }

    saveMutation.mutate({
      number: fields.number.trim(),
      type: fields.type,
      adults: Number(fields.adults),
      children: Number(fields.children),
      basePrice: Number(fields.basePrice),
      currency: fields.currency.trim().toUpperCase(),
    });
  }

  function handleDelete() {
    if (window.confirm(`Delete room ${room!.number}? This can't be undone here.`)) {
      saveMutation.reset();
      deleteMutation.mutate();
    }
  }

  // Throws away what was typed and shows the room as it is on the server now.
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
  const formError = failed ? adminErrorMessage(failed, 'room', FORM_FIELDS, ROOM_ERRORS) : null;
  const serverErrors = serverFieldErrors(saveMutation.error);
  const errorsFor = (field: string) => clientErrors[field] ?? serverErrors[field];
  const busy = saveMutation.isPending || deleteMutation.isPending;

  return (
    <section className="admin-page admin-form-page">
      <p>
        <Link to={`/admin/hotels/${hotelId}`}>← Hotel</Link>
      </p>
      <h1>{room === null ? 'Add a room' : `Edit room ${room.number}`}</h1>

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
        {saveMutation.isSuccess && room !== null && (
          <p role="status" className="admin-saved">
            Saved.
          </p>
        )}

        <TextField
          label="Room number"
          name="number"
          hint="Unique within the hotel, e.g. 101."
          value={fields.number}
          onChange={setField('number')}
          errors={errorsFor('number')}
        />
        <SelectField label="Type" value={fields.type} onChange={setField('type')} errors={errorsFor('type')}>
          <option value="">Choose</option>
          {ROOM_TYPES.map((type) => (
            <option key={type} value={type}>
              {type}
            </option>
          ))}
        </SelectField>
        <div className="admin-pair">
          <TextField
            label="Adults"
            name="adults"
            type="number"
            min="1"
            value={fields.adults}
            onChange={setField('adults')}
            errors={errorsFor('adults')}
          />
          <TextField
            label="Children"
            name="children"
            type="number"
            min="0"
            value={fields.children}
            onChange={setField('children')}
            errors={errorsFor('children')}
          />
        </div>
        <div className="admin-pair">
          <TextField
            label="Price per night"
            name="basePrice"
            hint="Before any deal."
            value={fields.basePrice}
            onChange={setField('basePrice')}
            errors={errorsFor('basePrice')}
          />
          <TextField
            label="Currency"
            name="currency"
            hint="Three letters, e.g. USD."
            value={fields.currency}
            onChange={setField('currency')}
            errors={errorsFor('currency')}
          />
        </div>

        <div className="admin-actions">
          <button type="submit" className="button-primary" disabled={busy}>
            {saveMutation.isPending ? 'Saving…' : room === null ? 'Add room' : 'Save'}
          </button>
          {room !== null && (
            <button type="button" className="button-danger" onClick={handleDelete} disabled={busy}>
              {deleteMutation.isPending ? 'Deleting…' : 'Delete room'}
            </button>
          )}
        </div>
      </form>
    </section>
  );
}

// Quick checks before sending. The server checks everything again (ranges,
// the guest limit, decimal places), and its messages show next to the same fields.
function checkFields(fields: Fields): FieldErrors {
  const errors: FieldErrors = {};
  if (fields.number.trim() === '') errors.number = ['Room number is required.'];
  if (fields.type === '') errors.type = ['Choose a room type.'];
  if (!isWholeNumber(fields.adults)) errors.adults = ['Adults must be a whole number.'];
  if (!isWholeNumber(fields.children)) errors.children = ['Children must be a whole number.'];
  if (fields.basePrice.trim() === '' || !Number.isFinite(Number(fields.basePrice))) {
    errors.basePrice = ['Price must be a number.'];
  }
  if (fields.currency.trim() === '') errors.currency = ['Currency is required.'];
  return errors;
}

function isWholeNumber(text: string): boolean {
  return text.trim() !== '' && Number.isInteger(Number(text));
}
