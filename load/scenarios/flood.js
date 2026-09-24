import { check, fail } from 'k6';
import http from 'k6/http';

import { TREND_STATS, isoDate, url } from '../lib/api.js';

const RATE = Number(__ENV.RATE || 1000);
const DURATION = __ENV.DURATION || '100s';

export const options = {
  summaryTrendStats: TREND_STATS,
  scenarios: {
    flood: {
      executor: 'constant-arrival-rate',
      rate: RATE,
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: 200,
      maxVUs: 1000
    }
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    dropped_iterations: ['count==0'],
    'http_req_duration{name:rooms}': ['p(99)<200']
  }
};

export function setup() {
  const hotels = http.get(url('/hotels?page=1&pageSize=50'));

  if (hotels.status !== 200) {
    fail(`hotels: expected 200, got ${hotels.status} ${hotels.body}`);
  }

  const ids = hotels.json('items').map((hotel) => hotel.id);

  if (ids.length === 0) {
    fail('The catalogue has no hotels. Is the stack seeded? (docker compose up -d --build)');
  }

  return { hotels: ids };
}

export default function (data) {
  const hotelId = data.hotels[(Math.random() * data.hotels.length) | 0];
  const arrival = 1 + ((Math.random() * 365) | 0);
  const nights = 1 + ((Math.random() * 7) | 0);

  const rooms = http.get(
    url(`/hotels/${hotelId}/rooms?checkIn=${isoDate(arrival)}&checkOut=${isoDate(arrival + nights)}&adults=2&page=1&pageSize=10`),
    { tags: { name: 'rooms' } });

  check(rooms, { 'rooms 200': (response) => response.status === 200 });
}
