import { apiGet, apiRequest, ifMatch } from './client';
import type { PagedList } from './types';

// One row of GET /api/cities (CitySummaryDto in v1.json).
export interface CitySummary {
  id: string;
  name: string;
  country: string;
  postOffice: string;
  thumbnailUrl: string | null;
  hotelCount: number;
  createdAtUtc: string;
  modifiedAtUtc: string | null;
  version: string;
}

export function getCities(): Promise<PagedList<CitySummary>> {
  return apiGet<PagedList<CitySummary>>('/api/cities');
}

// The API's largest page.
export const MAX_CITY_OPTIONS = 50;

// Cities for a dropdown, sorted by name. At most one page (50) can be loaded;
// compare `totalCount` with `items.length` to know if some were left out.
export function getCityOptions(): Promise<PagedList<CitySummary>> {
  return apiGet<PagedList<CitySummary>>(`/api/cities?sort=name&pageSize=${MAX_CITY_OPTIONS}`);
}

// The most viewed cities first. Only cities someone has visited are included, so
// this can be empty. Answers 503 when the view counters are unreachable.
export function getTrendingCities(pageSize: number): Promise<PagedList<CitySummary>> {
  return apiGet<PagedList<CitySummary>>(`/api/cities?sort=trending&pageSize=${pageSize}`);
}

// One city (CityDto). Same as a CitySummary without the hotel count.
export type City = Omit<CitySummary, 'hotelCount'>;

// No login needed. Answers 404 for an unknown city.
export function getCity(id: string): Promise<City> {
  return apiGet<City>(`/api/cities/${encodeURIComponent(id)}`);
}

// --- Admin ---------------------------------------------------------------------

// Cities per page on the admin list.
const ADMIN_PAGE_SIZE = 20;

// Every city by name, `search` matching anywhere in the name. Each row has the
// hotel count, so the admin can see which cities can't be deleted yet.
export function listCities(search: string, page: number): Promise<PagedList<CitySummary>> {
  const params = new URLSearchParams({ sort: 'name', page: String(page), pageSize: String(ADMIN_PAGE_SIZE) });
  if (search !== '') params.set('search', search);
  return apiGet<PagedList<CitySummary>>(`/api/cities?${params}`);
}

// The body of POST /api/cities and PUT /api/cities/{id} (CreateCityRequest and
// UpdateCityRequest in v1.json, which have the same fields).
export interface CityRequest {
  name: string;
  country: string;
  postOffice: string;
  // An absolute http or https URL, or null for no picture.
  thumbnailUrl: string | null;
}

// Admins only. Answers 400 with field errors and
// 409 City.NameAlreadyUsedInCountry.
export function createCity(request: CityRequest): Promise<City> {
  return apiRequest<City>('POST', '/api/cities', { body: request, auth: true });
}

// Admins only. `version` is the one the city was loaded with. Answers
// 409 Persistence.ConcurrencyConflict when someone saved it since.
export function updateCity(id: string, request: CityRequest, version: string): Promise<City> {
  return apiRequest<City>('PUT', `/api/cities/${encodeURIComponent(id)}`, {
    body: request,
    auth: true,
    headers: ifMatch(version),
  });
}

// Admins only. Answers 409 City.HasHotels while the city still has hotels.
export function deleteCity(id: string, version: string): Promise<void> {
  return apiRequest<void>('DELETE', `/api/cities/${encodeURIComponent(id)}`, {
    auth: true,
    headers: ifMatch(version),
  });
}

// One row of GET /api/cities/{cityId}/hotels (CityHotelDto).
export interface CityHotel {
  id: string;
  name: string;
  starRating: number;
  thumbnailUrl: string | null;
  // 0 means guests can't find the hotel yet: search only shows hotels with a room.
  roomCount: number;
}

// Every hotel in the city by name, including those with no rooms. Admins only.
// Answers 404 for an unknown or deleted city.
export function listCityHotels(cityId: string, search: string, page: number): Promise<PagedList<CityHotel>> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(ADMIN_PAGE_SIZE) });
  if (search !== '') params.set('search', search);
  return apiGet<PagedList<CityHotel>>(`/api/cities/${encodeURIComponent(cityId)}/hotels?${params}`, { auth: true });
}
