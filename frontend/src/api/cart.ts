import { apiGet, apiRequest } from './client';

// The logged-in user's cart. Holding a room here reserves nothing: someone else
// can still book it until this user checks out.

// One held stay (CartItemDto). Prices are the quote from when it was added.
export interface CartItem {
  // Used to remove the stay from the cart.
  id: string;
  roomId: string;
  hotelId: string;
  // Dates without a time, e.g. "2026-09-30".
  checkIn: string;
  checkOut: string;
  nights: number;
  adults: number;
  children: number;
  nightlyRate: number;
  total: number;
  discount: number;
  currency: string;
}

// The whole cart (CartDto). `currency` is null while the cart is empty.
// `degraded` is true when the cart store couldn't be reached, so an empty cart
// may not really be empty.
export interface Cart {
  items: CartItem[];
  itemCount: number;
  totalAmount: number;
  currency: string | null;
  degraded: boolean;
}

// The body of POST /api/cart/items.
export interface AddCartItemRequest {
  roomId: string;
  checkIn: string;
  checkOut: string;
  adults: number;
  children: number;
}

// Holds a room for a stay and returns the whole cart. Needs login.
// Adding the same room for the same nights twice updates it, so a double click
// is harmless. Errors, by errorCode:
//   400 Cart.RoomCannotHostParty, or bad dates    409 Cart.Full, Cart.CurrencyMismatch
//   404 Room.NotFound                             503 Cart.Unavailable
export function addCartItem(request: AddCartItemRequest): Promise<Cart> {
  return apiRequest<Cart>('POST', '/api/cart/items', { body: request, auth: true });
}

// The logged-in user's cart. Stays whose check-in has passed are dropped by the
// server. Never fails because the cart store is down: it answers an empty cart
// with `degraded: true` instead.
export function getCart(): Promise<Cart> {
  return apiGet<Cart>('/api/cart', { auth: true });
}

// Drops one stay, by the `id` getCart gave for it. 404 Cart.ItemNotFound when
// the cart no longer holds it (removed in another tab, or checked out).
export function removeCartItem(itemId: string): Promise<void> {
  return apiRequest<void>('DELETE', `/api/cart/items/${encodeURIComponent(itemId)}`, { auth: true });
}

// Drops every stay. Safe to repeat: an empty cart is still a success.
export function emptyCart(): Promise<void> {
  return apiRequest<void>('DELETE', '/api/cart', { auth: true });
}
