import type { HotelImage } from '../../../api/hotels';

// One row of the gallery editor. `key` is only for React (see below); it is
// never sent to the API.
export interface GalleryRow {
  key: number;
  url: string;
  caption: string;
}

// React needs a key that stays with a row when rows are moved or removed. The
// row's position would change on every move and the URL can be typed twice,
// so each row gets a number of its own from this counter.
let nextKey = 0;

export function newGalleryRow(url = '', caption = ''): GalleryRow {
  return { key: nextKey++, url, caption };
}

// The hotel's images as editor rows, in the order the admin chose.
export function galleryRowsFrom(images: HotelImage[]): GalleryRow[] {
  return [...images]
    .sort((a, b) => a.position - b.position)
    .map((image) => newGalleryRow(image.url, image.caption ?? ''));
}
