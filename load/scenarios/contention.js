import { check, sleep } from 'k6';
import exec from 'k6/execution';
import http from 'k6/http';
import { Counter } from 'k6/metrics';

import { TREND_STATS, book, discoverRooms, firstNightOfRun, guestTokens, isoDate } from '../lib/api.js';

const EXPECTED = http.expectedStatuses(201, 409);

const CALLERS = 50;

const sold = new Counter('contention_sold');
const refused = new Counter('contention_refused');
const unexpected = new Counter('contention_unexpected');

export const options = {
  summaryTrendStats: TREND_STATS,
  scenarios: {
    rush: {
      executor: 'per-vu-iterations',
      vus: CALLERS,
      iterations: 1,
      maxDuration: '2m'
    }
  },
  thresholds: {
    contention_sold: ['count==1'],
    contention_refused: [`count==${CALLERS - 1}`],
    contention_unexpected: ['count==0'],
    http_req_failed: ['rate==0'],
    'http_req_duration{name:checkout}': ['p(99)<3000']
  }
};

export function setup() {
  const firstNight = firstNightOfRun();

  return {
    tokens: guestTokens(`contention-${Date.now()}`, CALLERS),
    room: discoverRooms()[0],
    checkIn: isoDate(firstNight),
    checkOut: isoDate(firstNight + 3),
    startAt: Date.now() + 5000
  };
}

export default function (data) {
  const wait = (data.startAt - Date.now()) / 1000;

  if (wait > 0) {
    sleep(wait);
  }

  const booked = book(data.tokens[exec.vu.idInTest - 1], data.room, data.checkIn, data.checkOut, EXPECTED);

  if (booked.status === 201) {
    sold.add(1);
  } else if (booked.status === 409 && booked.json('errorCode') === 'Booking.RoomUnavailable') {
    refused.add(1);
  } else {
    unexpected.add(1);
    console.error(`contention: unexpected ${booked.status} ${booked.body}`);
  }

  check(booked, {
    'answered, not crashed': (response) => response.status < 500
  });
}
