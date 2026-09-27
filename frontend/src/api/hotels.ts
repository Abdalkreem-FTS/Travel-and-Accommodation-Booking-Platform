import { apiGet, apiRequest, ifMatch } from './client';
import type { PagedList } from './types';

// A hotel card (HotelSummaryDto in v1.json). Hotel search returns the same shape.
export interface HotelSummary {
  id: string;
  name: string;
  cityId: string;
  cityName: string;
  starRating: number;
  thumbnailUrl: string | null;
  // The cheapest room's nightly price.
  fromPrice: number;
  currency: string;
}

// The spec only says "string" for these two. The values come from the backend's
// RoomType and HotelSort enums (the server ignores upper/lower case).
export const ROOM_TYPES = ['Standard', 'Budget', 'Luxury', 'Boutique'] as const;
export type RoomType = (typeof ROOM_TYPES)[number];

export const HOTEL_SORTS = ['price', 'stars', 'name'] as const;
export type HotelSort = (typeof HOTEL_SORTS)[number];

// The filters of GET /api/hotels. Every one is optional; leaving one out means
// "don't filter on it", except `adults`, which the server then takes as 2.
export interface HotelSearch {
  cityId?: string;
  // Dates without a time, e.g. "2026-09-30". Give both or neither.
  checkIn?: string;
  checkOut?: string;
  adults?: number;
  children?: number;
  minPrice?: number;
  maxPrice?: number;
  // Any of 1 to 5. Empty means every rating.
  stars: number[];
  roomType?: RoomType;
  // The server sorts by price when this is left out.
  sort?: HotelSort;
  // Starts at 1.
  page?: number;
}

// The filters as a query string: "cityId=...&stars=4&stars=5". Empty filters are
// left out. The search page uses the same names in its own URL, so this one
// function builds both the page link and the API request.
export function hotelSearchParams(search: HotelSearch): URLSearchParams {
  const params = new URLSearchParams();
  const add = (name: string, value: string | number | undefined) => {
    if (value !== undefined && value !== '') params.append(name, String(value));
  };

  add('cityId', search.cityId);
  add('checkIn', search.checkIn);
  add('checkOut', search.checkOut);
  add('adults', search.adults);
  add('children', search.children);
  add('minPrice', search.minPrice);
  add('maxPrice', search.maxPrice);
  // An array goes in as the same name repeated, which is how ASP.NET reads int[].
  search.stars.forEach((star) => add('stars', star));
  add('roomType', search.roomType);
  add('sort', search.sort);
  add('page', search.page);
  return params;
}

// Hotels per page. The server allows up to 50; 10 keeps a phone's scroll short.
const SEARCH_PAGE_SIZE = 10;

// One page of hotels. No login needed.
// Answers 400 with field errors when a filter is invalid.
export function searchHotels(search: HotelSearch): Promise<PagedList<HotelSummary>> {
  const params = hotelSearchParams(search);
  params.set('pageSize', String(SEARCH_PAGE_SIZE));
  return apiGet<PagedList<HotelSummary>>(`/api/hotels?${params}`);
}

// The last 5 hotels the logged-in user opened, newest first. Needs login.
export function getViewedHotels(): Promise<HotelSummary[]> {
  return apiGet<HotelSummary[]>('/api/viewed-hotels', { auth: true });
}

// --- One hotel -----------------------------------------------------------------

// A photo in the hotel's gallery (HotelImageDto). `position` is the order an
// administrator chose, counted from 0.
export interface HotelImage {
  url: string;
  caption: string | null;
  position: number;
}

// An entry from the shared amenity catalogue (AmenityDto), e.g. "Free Wi-Fi".
export interface Amenity {
  id: string;
  slug: string;
  name: string;
  description: string;
}

// The full hotel (HotelDto). It has the city's id but not its name; see getCity().
export interface HotelDetails {
  id: string;
  cityId: string;
  name: string;
  description: string;
  owner: string;
  starRating: number;
  latitude: number;
  longitude: number;
  thumbnailUrl: string | null;
  images: HotelImage[];
  amenities: Amenity[];
  createdAtUtc: string;
  modifiedAtUtc: string | null;
  // What an admin sends back as If-Match when editing. Not used by guests.
  version: string;
}

// One hotel. No login needed, but a logged-in user's token is sent so the
// server can add the hotel to their "Recently viewed". Pass countAsView: false
// when the hotel is only named on another page (the cart, a booking), so that
// doesn't count as a visit. Answers 404 for an unknown hotel.
export function getHotel(id: string, { countAsView = true } = {}): Promise<HotelDetails> {
  return apiGet<HotelDetails>(`/api/hotels/${encodeURIComponent(id)}`, {
    auth: countAsView ? 'if-logged-in' : false,
  });
}

// --- A hotel's rooms -----------------------------------------------------------

// The dates and guests of a stay. Same names as the search filters, so a stay
// chosen on the search page can be carried over to a hotel page.
export type Stay = Pick<HotelSearch, 'checkIn' | 'checkOut' | 'adults' | 'children'>;

// A room that can host the party (AvailableRoomDto). `nights` and `total` are
// only there when the request gave dates; without dates there is no stay to price.
export interface AvailableRoom {
  id: string;
  number: string;
  type: string;
  // How many guests the room takes.
  adults: number;
  children: number;
  nightlyRate: number;
  currency: string;
  nights: number | null;
  total: number | null;
}

// Rooms per page. Hotels rarely have more free rooms than this for one stay.
const ROOMS_PAGE_SIZE = 10;

// The hotel's rooms that fit the party, cheapest first. With both dates, only
// rooms free for the whole stay. No login needed.
// Answers 400 with field errors for bad dates (half a stay, a past check-in,
// more than 30 nights) and 404 for an unknown hotel.
export function getHotelRooms(hotelId: string, stay: Stay, page: number): Promise<PagedList<AvailableRoom>> {
  const params = hotelSearchParams({ ...stay, stars: [], page });
  params.set('pageSize', String(ROOMS_PAGE_SIZE));
  return apiGet<PagedList<AvailableRoom>>(`/api/hotels/${encodeURIComponent(hotelId)}/rooms?${params}`);
}

// --- Admin ---------------------------------------------------------------------

// The shared amenity catalogue, for the hotel form's checkboxes. No login needed.
export function getAmenities(): Promise<Amenity[]> {
  return apiGet<Amenity[]>('/api/amenities');
}

// The body of POST /api/hotels and PUT /api/hotels/{id} (CreateHotelRequest and
// UpdateHotelRequest, which have the same fields).
//
// A PUT *replaces* the gallery and the amenities: leaving one out, or sending
// it empty, clears it. So always send the full lists.
export interface HotelRequest {
  cityId: string;
  name: string;
  description: string;
  owner: string;
  // 1 to 5.
  starRating: number;
  latitude: number;
  longitude: number;
  thumbnailUrl: string | null;
  // In the order the guests will see them.
  images: { url: string; caption: string | null }[];
  amenityIds: string[];
}

// Admins only. Answers 400 with field errors (an unknown city is a cityId
// error) and 409 Hotel.NameAlreadyUsedInCity.
export function createHotel(request: HotelRequest): Promise<HotelDetails> {
  return apiRequest<HotelDetails>('POST', '/api/hotels', { body: request, auth: true });
}

// Admins only. Changing cityId moves the hotel to another city.
export function updateHotel(id: string, request: HotelRequest, version: string): Promise<HotelDetails> {
  return apiRequest<HotelDetails>('PUT', `/api/hotels/${encodeURIComponent(id)}`, {
    body: request,
    auth: true,
    headers: ifMatch(version),
  });
}

// Admins only. Answers 409 Hotel.HasRooms while the hotel still has rooms.
export function deleteHotel(id: string, version: string): Promise<void> {
  return apiRequest<void>('DELETE', `/api/hotels/${encodeURIComponent(id)}`, {
    auth: true,
    headers: ifMatch(version),
  });
}

// Rooms per page on the admin hotel page.
const ADMIN_ROOMS_PAGE_SIZE = 20;

// Every room of the hotel, cheapest first, for the admin hotel page.
//
// It's the guests' room list without dates, asking for rooms that take 1 adult
// and no children. Every room takes at least one adult, so that is every room.
export function listHotelRooms(hotelId: string, page: number): Promise<PagedList<AvailableRoom>> {
  const params = hotelSearchParams({ adults: 1, children: 0, stars: [], page });
  params.set('pageSize', String(ADMIN_ROOMS_PAGE_SIZE));
  return apiGet<PagedList<AvailableRoom>>(`/api/hotels/${encodeURIComponent(hotelId)}/rooms?${params}`);
}
