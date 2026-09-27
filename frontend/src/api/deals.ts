import { apiGet, apiRequest, ifMatch } from './client';
import type { PagedList } from './types';

// One card of GET /api/deals (FeaturedDealDto in v1.json).
export interface FeaturedDeal {
  id: string;
  hotelId: string;
  hotelName: string;
  cityId: string;
  cityName: string;
  thumbnailUrl: string | null;
  starRating: number;
  roomId: string;
  roomType: string;
  originalPrice: number;
  discountedPrice: number;
  discountPercentage: number;
  currency: string;
  // A date without a time, e.g. "2026-09-30". The deal is over on this day.
  endsOn: string;
}

// The running featured deals, deepest discount first. The API allows 1 to 10.
// Returns a plain array, not a PagedList.
export function getFeaturedDeals(limit: number): Promise<FeaturedDeal[]> {
  return apiGet<FeaturedDeal[]>(`/api/deals?limit=${limit}`);
}

// --- Admin ---------------------------------------------------------------------

// One deal as an admin sees it (DealDto): a percentage, not a price.
export interface Deal {
  id: string;
  hotelId: string;
  roomId: string;
  // 1 to 90.
  discountPercentage: number;
  // Dates without a time. The window is half-open: `startsOn` is the first
  // discounted night, and on `endsOn` the deal no longer applies.
  startsOn: string;
  endsOn: string;
  // Shown on the home page while it runs.
  isFeatured: boolean;
  createdAtUtc: string;
  modifiedAtUtc: string | null;
  version: string;
}

// Deals per page on the admin room page.
const ADMIN_PAGE_SIZE = 20;

// Every deal on the room (ended, running, upcoming), latest start first.
// Admins only. Answers 404 for an unknown or deleted room.
export function listRoomDeals(roomId: string, page: number): Promise<PagedList<Deal>> {
  return apiGet<PagedList<Deal>>(
    `/api/rooms/${encodeURIComponent(roomId)}/deals?page=${page}&pageSize=${ADMIN_PAGE_SIZE}`,
    { auth: true },
  );
}

// Admins only. Answers 404 for an unknown or deleted deal.
export function getDeal(id: string): Promise<Deal> {
  return apiGet<Deal>(`/api/deals/${encodeURIComponent(id)}`, { auth: true });
}

// The body of PUT /api/deals/{id} (UpdateDealRequest). The room can't change:
// a discount on another room is another deal.
export interface DealRequest {
  discountPercentage: number;
  startsOn: string;
  endsOn: string;
  isFeatured: boolean;
}

// Admins only. Answers 400 with field errors and 409 Deal.OverlapsExisting
// when another deal already discounts one of those nights.
export function createDeal(roomId: string, request: DealRequest): Promise<Deal> {
  return apiRequest<Deal>('POST', '/api/deals', { body: { roomId, ...request }, auth: true });
}

// Admins only. Can also answer 409 Deal.OverlapsExisting.
export function updateDeal(id: string, request: DealRequest, version: string): Promise<Deal> {
  return apiRequest<Deal>('PUT', `/api/deals/${encodeURIComponent(id)}`, {
    body: request,
    auth: true,
    headers: ifMatch(version),
  });
}

// Admins only. Nothing blocks it: bookings keep the price they were charged.
export function deleteDeal(id: string, version: string): Promise<void> {
  return apiRequest<void>('DELETE', `/api/deals/${encodeURIComponent(id)}`, {
    auth: true,
    headers: ifMatch(version),
  });
}
