import { hotelSearchParams, type Stay } from '../../api/hotels';
import { readHotelSearch } from '../search/searchParams';

// The hotel page keeps the stay in its URL, with the same names the search page
// uses: /hotels/42?checkIn=2026-10-01&checkOut=2026-10-03&adults=2&children=0.
// `page` is the page of the room list.

export function readStay(params: URLSearchParams): Stay {
  // The search page already knows how to read these four safely.
  const { checkIn, checkOut, adults, children } = readHotelSearch(params);
  return { checkIn, checkOut, adults, children };
}

export function readRoomsPage(params: URLSearchParams): number {
  const page = Number(params.get('page'));
  return Number.isInteger(page) && page >= 1 ? page : 1;
}

// A link to a hotel's page with this stay (and page of rooms) in it.
export function hotelHref(hotelId: string, stay: Stay = {}, page?: number): string {
  const query = hotelSearchParams({ ...stay, stars: [], page: page === 1 ? undefined : page }).toString();
  return query === '' ? `/hotels/${hotelId}` : `/hotels/${hotelId}?${query}`;
}

// Everything that picks which rooms are shown, ignoring the page. When it
// changes, the stay form starts over from the URL (see RoomsSection).
export function stayKey(stay: Stay): string {
  return hotelSearchParams({ ...stay, stars: [] }).toString();
}
