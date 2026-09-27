import { check } from 'k6';
import exec from 'k6/execution';
import http from 'k6/http';

import { TREND_STATS, book, cancel, discoverRooms, firstNightOfRun, guestTokens, isoDate } from '../lib/api.js';

const EXPECTED = http.expectedStatuses(201);

const MAX_VUS = 60;

export const options = {
  summaryTrendStats: TREND_STATS,
  scenarios: {
    checkout: {
      executor: 'constant-arrival-rate',
      rate: 10,
      timeUnit: '1s',
      duration: '2m',
      preAllocatedVUs: 20,
      maxVUs: MAX_VUS
    }
  },
  thresholds: {
    http_req_failed: ['rate==0'],
    checks: ['rate==1'],
    'http_req_duration{name:checkout}': ['p(95)<150']
  }
};

export function setup() {
  return {
    tokens: guestTokens(`checkout-${Date.now()}`, MAX_VUS),
    rooms: discoverRooms(),
    firstNight: firstNightOfRun()
  };
}

export default function (data) {
  const iteration = exec.scenario.iterationInTest;
  const room = data.rooms[iteration % data.rooms.length];
  const cycle = Math.floor(iteration / data.rooms.length);
  const arrival = data.firstNight + (cycle * 4);

  const token = data.tokens[exec.vu.idInTest - 1];

  const booked = book(token, room, isoDate(arrival), isoDate(arrival + 3), EXPECTED);

  check(booked, {
    'booked 201': (response) => response.status === 201
  });

  if (booked.status !== 201) {
    console.error(`checkout: expected 201, got ${booked.status} ${booked.body}`);

    return;
  }

  const cancelled = cancel(token, booked.json('id'));

  check(cancelled, {
    'cancelled 201': (response) => response.status === 201
  });
}
