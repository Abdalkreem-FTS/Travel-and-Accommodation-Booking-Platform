import { TextField } from '../../../components/TextField';
import { newGalleryRow, type GalleryRow } from './galleryRows';

// Same limit as the backend (Hotel.MaxImages).
const MAX_IMAGES = 20;

interface GalleryFieldProps {
  rows: GalleryRow[];
  onChange: (rows: GalleryRow[]) => void;
  errors?: string[];
}

// The hotel's photos, in the order guests will see them.
export function GalleryField({ rows, onChange, errors = [] }: GalleryFieldProps) {
  const update = (key: number, change: Partial<GalleryRow>) =>
    onChange(rows.map((row) => (row.key === key ? { ...row, ...change } : row)));

  const remove = (key: number) => onChange(rows.filter((row) => row.key !== key));

  // Swaps a row with its neighbour. `by` is -1 for up, +1 for down.
  const move = (index: number, by: number) => {
    const moved = [...rows];
    [moved[index], moved[index + by]] = [moved[index + by], moved[index]];
    onChange(moved);
  };

  const add = () => onChange([...rows, newGalleryRow()]);

  return (
    <fieldset className="admin-fieldset">
      <legend>Gallery</legend>
      {rows.length === 0 && <p className="muted">No photos yet.</p>}

      <ol className="admin-gallery">
        {rows.map((row, index) => (
          <li key={row.key} className="admin-gallery-row">
            <TextField
              label={`Photo ${index + 1} URL`}
              name={`image-${index}-url`}
              value={row.url}
              onChange={(url) => update(row.key, { url })}
            />
            <TextField
              label={`Photo ${index + 1} caption`}
              name={`image-${index}-caption`}
              hint="Optional."
              value={row.caption}
              onChange={(caption) => update(row.key, { caption })}
            />
            <div className="admin-gallery-buttons">
              <button type="button" onClick={() => move(index, -1)} disabled={index === 0}>
                Move up
              </button>
              <button type="button" onClick={() => move(index, 1)} disabled={index === rows.length - 1}>
                Move down
              </button>
              <button type="button" onClick={() => remove(row.key)}>
                Remove
              </button>
            </div>
          </li>
        ))}
      </ol>

      <button type="button" onClick={add} disabled={rows.length >= MAX_IMAGES}>
        Add a photo
      </button>
      {rows.length >= MAX_IMAGES && <p className="text-field-hint">A gallery holds at most {MAX_IMAGES} photos.</p>}

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
