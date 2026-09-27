import http from 'k6/http';
import { check, sleep } from 'k6';

import { TREND_STATS, discoverCities, isoDate, url } from '../lib/api.js';

export const options = {
  summaryTrendStats: TREND_STATS,
  scenarios: {
    browse: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 50 },
        { duration: '2m', target: 50 },
        { duration: '10s', target: 0 }
      ]
    }
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    checks: ['rate==1'],
    'http_req_duration{name:cities}': ['p(95)<50'],
    'http_req_duration{name:search}': ['p(95)<50'],
    'http_req_duration{name:hotel}': ['p(95)<50'],
    'http_req_duration{name:rooms}': ['p(95)<100']
  }
};

export function setup() {
  return { cities: discoverCities() };
}

export default function (data) {
  const city = data.cities[(Math.random() * data.cities.length) | 0];
  const arrival = 30 + ((Math.random() * 60) | 0);
  const checkIn = isoDate(arrival);
  const checkOut = isoDate(arrival + 3);

  const cities = http.get(url('/cities?page=1&pageSize=10'), { tags: { name: 'cities' } });
  check(cities, { 'cities 200': (response) => response.status === 200 });

  const search = http.get(
    url(`/hotels?cityId=${city}&adults=2&page=1&pageSize=10`),
    { tags: { name: 'search' } });
  check(search, { 'search 200': (response) => response.status === 200 });

  const hotels = search.status === 200 ? search.json('items') : [];

  if (hotels.length > 0) {
    const hotelId = hotels[(Math.random() * hotels.length) | 0].id;

    const hotel = http.get(url(`/hotels/${hotelId}`), { tags: { name: 'hotel' } });
    check(hotel, { 'hotel 200': (response) => response.status === 200 });

    const rooms = http.get(
      url(`/hotels/${hotelId}/rooms?checkIn=${checkIn}&checkOut=${checkOut}&adults=2&page=1&pageSize=10`),
      { tags: { name: 'rooms' } });
    check(rooms, { 'rooms 200': (response) => response.status === 200 });
  }

  sleep(1);
}
