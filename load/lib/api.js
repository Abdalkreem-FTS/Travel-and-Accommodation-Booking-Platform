import http from 'k6/http';
import { fail } from 'k6';

const BASE = `${__ENV.BASE_URL || 'http://gateway:8080'}/api`;

const PASSWORD = 'LoadRun!2026';

export const TREND_STATS = ['min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max', 'count'];

export function url(path) {
  return `${BASE}${path}`;
}

export function uuid() {
  const hex = '0123456789abcdef';
  let out = '';

  for (let i = 0; i < 36; i++) {
    if (i === 8 || i === 13 || i === 18 || i === 23) {
      out += '-';
    } else if (i === 14) {
      out += '4';
    } else if (i === 19) {
      out += hex[8 + ((Math.random() * 4) | 0)];
    } else {
      out += hex[(Math.random() * 16) | 0];
    }
  }

  return out;
}

export function isoDate(daysFromToday) {
  const day = new Date();
  day.setUTCDate(day.getUTCDate() + daysFromToday);

  return day.toISOString().slice(0, 10);
}

export function firstNightOfRun() {
  return 400 + (Math.floor(Date.now() / 1000) % 5000);
}

export function guestToken(runId) {
  const email = `load-${runId}@hotelbooking.local`;
  const guest = {
    email,
    password: PASSWORD,
    firstName: 'Abdalkreem',
    lastName: 'Bzoor'
  };

  const registered = http.post(url('/users'), JSON.stringify(guest), json());

  if (registered.status !== 201) {
    fail(`register: expected 201, got ${registered.status} ${registered.body}`);
  }

  const session = http.post(
    url('/sessions'),
    JSON.stringify({ email, password: PASSWORD }),
    json());

  if (session.status !== 200) {
    fail(`login: expected 200, got ${session.status} ${session.body}`);
  }

  return session.json('accessToken');
}

export function discoverCities() {
  const cities = http.get(url('/cities?page=1&pageSize=50'));

  if (cities.status !== 200) {
    fail(`cities: expected 200, got ${cities.status} ${cities.body}`);
  }

  const ids = cities.json('items').map((city) => city.id);

  if (ids.length === 0) {
    fail('The catalogue has no cities. Is the stack seeded? (docker compose up -d --build)');
  }

  return ids;
}

export function discoverRooms() {
  const hotels = http.get(url('/hotels?page=1&pageSize=50'));

  if (hotels.status !== 200) {
    fail(`hotels: expected 200, got ${hotels.status} ${hotels.body}`);
  }

  const rooms = [];

  for (const hotel of hotels.json('items')) {
    const listed = http.get(url(`/hotels/${hotel.id}/rooms?adults=1&page=1&pageSize=50`));

    if (listed.status !== 200) {
      fail(`rooms: expected 200, got ${listed.status} ${listed.body}`);
    }

    for (const room of listed.json('items')) {
      rooms.push(room.id);
    }
  }

  if (rooms.length === 0) {
    fail('The catalogue has no rooms. Is the stack seeded? (docker compose up -d --build)');
  }

  return rooms;
}

export function book(token, roomId, checkIn, checkOut, expected) {
  const body = {
    items: [{ roomId, checkIn, checkOut, adults: 1, children: 0 }]
  };

  return http.post(url('/bookings'), JSON.stringify(body), {
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      'Idempotency-Key': uuid()
    },
    tags: { name: 'checkout' },
    responseCallback: expected
  });
}

function json() {
  return { headers: { 'Content-Type': 'application/json' } };
}
