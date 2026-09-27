import { HOTEL_SORTS, ROOM_TYPES, hotelSearchParams, type HotelSearch } from '../../api/hotels';

// The search page keeps its filters in the URL (/hotels?cityId=...&stars=4).
// This file reads them back out. The other direction is hotelSearchParams() in
// api/hotels.ts, because the API request uses exactly the same names.
//
// Anyone can type anything into a URL. A value that can't be read (page=abc,
// roomType=Castle) is dropped, as if it wasn't there. Values that can be read but
// break a rule (checkIn in the past, maxPrice below minPrice) are kept: the
// server checks those and the page shows its message next to the field.

export function readHotelSearch(params: URLSearchParams): HotelSearch {
  return {
    cityId: text(params.get('cityId')),
    checkIn: text(params.get('checkIn')),
    checkOut: text(params.get('checkOut')),
    adults: whole(params.get('adults')),
    children: whole(params.get('children')),
    minPrice: decimal(params.get('minPrice')),
    maxPrice: decimal(params.get('maxPrice')),
    stars: params
      .getAll('stars')
      .map(Number)
      .filter((star) => Number.isInteger(star) && star >= 1 && star <= 5),
    roomType: oneOf(ROOM_TYPES, params.get('roomType')),
    sort: oneOf(HOTEL_SORTS, params.get('sort')),
    page: whole(params.get('page')),
  };
}

// The same search, with other changes applied, as a link to the search page.
export function searchHref(search: HotelSearch, changes: Partial<HotelSearch> = {}): string {
  const query = hotelSearchParams({ ...search, ...changes }).toString();
  return query === '' ? '/hotels' : `/hotels?${query}`;
}

// Everything the filter form controls, ignoring page and sort. When this changes,
// the form starts over from the URL (see SearchPage).
export function filtersKey(search: HotelSearch): string {
  return hotelSearchParams({ ...search, page: undefined, sort: undefined }).toString();
}

function text(value: string | null): string | undefined {
  return value === null || value.trim() === '' ? undefined : value.trim();
}

function whole(value: string | null): number | undefined {
  const number = Number(text(value));
  return Number.isInteger(number) ? number : undefined;
}

function decimal(value: string | null): number | undefined {
  const number = Number(text(value));
  return Number.isFinite(number) ? number : undefined;
}

// Accepts only one of the listed values, ignoring case like the server does.
// The result is spelled as in the list ("LUXURY" -> "Luxury").
function oneOf<T extends string>(allowed: readonly T[], value: string | null): T | undefined {
  return allowed.find((item) => item.toLowerCase() === value?.toLowerCase());
}
