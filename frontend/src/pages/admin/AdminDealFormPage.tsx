import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { createDeal, deleteDeal, getDeal, updateDeal, type Deal, type DealRequest } from '../../api/deals';
import { ApiError } from '../../api/types';
import { serverFieldErrors, type FieldErrors } from '../../auth/formErrors';
import { LoadError } from '../../components/LoadError';
import { TextField } from '../../components/TextField';
import { adminErrorMessage, isStale } from './adminErrors';
import './Admin.css';

// Same limits as the backend (DiscountPercentage).
const MIN_DISCOUNT = 1;
const MAX_DISCOUNT = 90;

const FORM_FIELDS = ['discountPercentage', 'startsOn', 'endsOn', 'isFeatured', 'roomId'];

// This form's own errorCodes. The shared ones are in adminErrorMessage.
const DEAL_ERRORS: Record<string, string> = {
  'Deal.OverlapsExisting':
    'Another deal already discounts this room on one of those nights. Change the dates, or shorten or delete the other deal first.',
  'Deal.NotFound': 'This deal has been deleted.',
  'Deal.AlreadyDeleted': 'This deal has been deleted.',
};

// /admin/rooms/:roomId/deals/new and /admin/deals/:id
export function AdminDealFormPage() {
  const { id, roomId } = useParams();

  if (id === undefined) {
    return <DealForm deal={null} roomId={roomId!} />;
  }
  return <EditDeal id={id} />;
}

function EditDeal({ id }: { id: string }) {
  const { data, isPending, isError, error, refetch } = useQuery({
    queryKey: ['deals', id],
    queryFn: () => getDeal(id),
    // Always load the latest version before editing.
    staleTime: 0,
  });

  if (isPending) {
    return <p role="status">Loading the deal…</p>;
  }
  if (isError) {
    if (error instanceof ApiError && error.status === 404) {
      return (
        <section className="admin-page">
          <h1>Deal not found</h1>
          <p>This deal doesn't exist or has been deleted.</p>
          <Link to="/admin/cities">Back to cities</Link>
        </section>
      );
    }
    return <LoadError what="the deal" error={error} onRetry={() => refetch()} />;
  }

  return <DealForm deal={data} roomId={data.roomId} reload={async () => (await refetch()).data} />;
}

interface Fields {
  discountPercentage: string;
  startsOn: string;
  endsOn: string;
  isFeatured: boolean;
}

function fieldsFrom(deal: Deal | null): Fields {
  return {
    discountPercentage: deal ? String(deal.discountPercentage) : '',
    startsOn: deal?.startsOn ?? '',
    endsOn: deal?.endsOn ?? '',
    isFeatured: deal?.isFeatured ?? false,
  };
}

interface DealFormProps {
  // null when adding a new deal.
  deal: Deal | null;
  roomId: string;
  // Loads the deal again from the server. Only given when editing.
  reload?: () => Promise<Deal | undefined>;
}

function DealForm({ deal, roomId, reload }: DealFormProps) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [fields, setFields] = useState(() => fieldsFrom(deal));
  const [clientErrors, setClientErrors] = useState<FieldErrors>({});

  const setField = (name: 'discountPercentage' | 'startsOn' | 'endsOn') => (value: string) =>
    setFields((current) => ({ ...current, [name]: value }));

  // A deal changes the room's list of deals, the home page's featured deals,
  // and what the guests' pages charge for a stay.
  const refreshLists = () => {
    queryClient.invalidateQueries({ queryKey: ['rooms', roomId, 'deals'] });
    queryClient.invalidateQueries({ queryKey: ['deals'] });
    queryClient.invalidateQueries({ queryKey: ['hotels'] });
  };

  const saveMutation = useMutation({
    mutationFn: (request: DealRequest) =>
      deal === null ? createDeal(roomId, request) : updateDeal(deal.id, request, deal.version),
    onSuccess: (saved) => {
      queryClient.setQueryData(['deals', saved.id], saved);
      refreshLists();
      if (deal === null) {
        navigate(`/admin/deals/${saved.id}`, { replace: true });
      }
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => deleteDeal(deal!.id, deal!.version),
    onSuccess: () => {
      navigate(`/admin/rooms/${roomId}`, { replace: true });
      queryClient.removeQueries({ queryKey: ['deals', deal!.id] });
      refreshLists();
    },
  });

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    deleteMutation.reset();

    const errors = checkFields(fields);
    setClientErrors(errors);
    if (Object.keys(errors).length > 0) {
      saveMutation.reset();
      return;
    }

    saveMutation.mutate({
      discountPercentage: Number(fields.discountPercentage),
      startsOn: fields.startsOn,
      endsOn: fields.endsOn,
      isFeatured: fields.isFeatured,
    });
  }

  function handleDelete() {
    if (window.confirm('Delete this deal? Bookings already made keep the price they were charged.')) {
      saveMutation.reset();
      deleteMutation.mutate();
    }
  }

  // Throws away what was typed and shows the deal as it is on the server now.
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
  const formError = failed ? adminErrorMessage(failed, 'deal', FORM_FIELDS, DEAL_ERRORS) : null;
  const serverErrors = serverFieldErrors(saveMutation.error);
  const errorsFor = (field: string) => clientErrors[field] ?? serverErrors[field];
  const busy = saveMutation.isPending || deleteMutation.isPending;

  return (
    <section className="admin-page admin-form-page">
      <p>
        <Link to={`/admin/rooms/${roomId}`}>← Room</Link>
      </p>
      <h1>{deal === null ? 'Add a deal' : 'Edit deal'}</h1>
      {deal !== null && (
        <p className="muted">
          A deal stays on its room. To discount another room, add a deal there and delete this one.
        </p>
      )}

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
        {saveMutation.isSuccess && deal !== null && (
          <p role="status" className="admin-saved">
            Saved.
          </p>
        )}

        <TextField
          label="Discount (%)"
          name="discountPercentage"
          type="number"
          min={String(MIN_DISCOUNT)}
          max={String(MAX_DISCOUNT)}
          hint={`${MIN_DISCOUNT} to ${MAX_DISCOUNT}.`}
          value={fields.discountPercentage}
          onChange={setField('discountPercentage')}
          errors={errorsFor('discountPercentage')}
        />
        <div className="admin-pair">
          <TextField
            label="Starts on"
            name="startsOn"
            type="date"
            hint="The first discounted night."
            value={fields.startsOn}
            onChange={setField('startsOn')}
            errors={errorsFor('startsOn')}
          />
          <TextField
            label="Ends on"
            name="endsOn"
            type="date"
            min={fields.startsOn || undefined}
            hint="The deal no longer applies on this day."
            value={fields.endsOn}
            onChange={setField('endsOn')}
            errors={errorsFor('endsOn')}
          />
        </div>
        <label className="admin-checkbox">
          <input
            type="checkbox"
            checked={fields.isFeatured}
            onChange={(event) => setFields((current) => ({ ...current, isFeatured: event.target.checked }))}
          />
          Featured on the home page
        </label>

        <div className="admin-actions">
          <button type="submit" className="button-primary" disabled={busy}>
            {saveMutation.isPending ? 'Saving…' : deal === null ? 'Add deal' : 'Save'}
          </button>
          {deal !== null && (
            <button type="button" className="button-danger" onClick={handleDelete} disabled={busy}>
              {deleteMutation.isPending ? 'Deleting…' : 'Delete deal'}
            </button>
          )}
        </div>
      </form>
    </section>
  );
}

// Quick checks before sending. The server checks everything again (the
// 365-night limit, overlaps with other deals), and its messages show next to
// the same fields.
function checkFields(fields: Fields): FieldErrors {
  const errors: FieldErrors = {};
  const discount = Number(fields.discountPercentage);
  if (
    fields.discountPercentage.trim() === '' ||
    !Number.isInteger(discount) ||
    discount < MIN_DISCOUNT ||
    discount > MAX_DISCOUNT
  ) {
    errors.discountPercentage = [`Discount must be a whole number from ${MIN_DISCOUNT} to ${MAX_DISCOUNT}.`];
  }
  if (fields.startsOn === '') errors.startsOn = ['Choose the first day.'];
  if (fields.endsOn === '') {
    errors.endsOn = ['Choose the end day.'];
  } else if (fields.startsOn !== '' && fields.endsOn <= fields.startsOn) {
    // "2026-10-01" style dates compare as text in date order.
    errors.endsOn = ['The end day must be after the first day, or the deal never runs.'];
  }
  return errors;
}
