import { apiGet, apiRequest, ifMatch } from './client';
import { ROOM_TYPES, type RoomType } from './hotels';

// One room (RoomDto). Anyone may read it; `version` and the timestamps are only
// used by the admin pages.
export interface Room {
  id: string;
  hotelId: string;
  number: string;
  // One of ROOM_TYPES, e.g. "Luxury".
  type: string;
  adults: number;
  children: number;
  basePrice: number;
  currency: string;
  createdAtUtc: string;
  modifiedAtUtc: string | null;
  // What an admin sends back as If-Match when editing.
  version: string;
}

export function getRoom(id: string): Promise<Room> {
  return apiGet<Room>(`/api/rooms/${encodeURIComponent(id)}`);
}

// --- Admin ---------------------------------------------------------------------

// The room form's values (CreateRoomRequest and UpdateRoomRequest have the same fields).
export interface RoomRequest {
  number: string;
  type: RoomType;
  adults: number;
  children: number;
  basePrice: number;
  // Three letters, e.g. "USD".
  currency: string;
}

// The API returns `type` as a name ("Luxury") but only accepts it back as the
// RoomType enum's number. ROOM_TYPES is in the enum's order, so the position
// in that list is the number: Standard = 0, Budget = 1, ...
function toBody(request: RoomRequest) {
  return { ...request, type: ROOM_TYPES.indexOf(request.type) };
}

// Admins only. Answers 404 for an unknown hotel and
// 409 Room.NumberAlreadyUsedInHotel.
export function createRoom(hotelId: string, request: RoomRequest): Promise<Room> {
  return apiRequest<Room>('POST', `/api/hotels/${encodeURIComponent(hotelId)}/rooms`, {
    body: toBody(request),
    auth: true,
  });
}

// Admins only.
export function updateRoom(id: string, request: RoomRequest, version: string): Promise<Room> {
  return apiRequest<Room>('PUT', `/api/rooms/${encodeURIComponent(id)}`, {
    body: toBody(request),
    auth: true,
    headers: ifMatch(version),
  });
}

// Admins only. Answers 409 Room.HasFutureBookings while guests still hold
// nights in it (today or later).
export function deleteRoom(id: string, version: string): Promise<void> {
  return apiRequest<void>('DELETE', `/api/rooms/${encodeURIComponent(id)}`, {
    auth: true,
    headers: ifMatch(version),
  });
}
