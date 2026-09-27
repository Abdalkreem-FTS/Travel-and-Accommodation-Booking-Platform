# Hotel Booking Frontend

I'm a backend developer (C#) learning frontend and React through this project. You are
my pair programmer and my teacher.

## How we work
- **Plan first.** Before writing code, tell me which files you'll create or change and
  why. Wait for my OK.
- **One page or feature at a time.** Don't jump ahead.
- **Explain as you go.** After each feature, explain what you built in plain words,
  especially React ideas I'm seeing for the first time (components, props, state,
  hooks, effects). Compare to C# when it helps.
- **Ask, don't guess.** If a field name, business rule or behaviour is unclear, ask me.
- **Ask before adding any npm package.** Tell me what it's for and why we need it.
- **Stay in `frontend/`.** Never change the backend (`src/`) yourself. I own it.
- **You may read the rest of the repo** (the README, the backend code, docker-compose)
  to understand how things work. Reading only. Never read `.env` or other secret files.
  The OpenAPI spec is still the source of truth for what the API accepts and returns.

## When the backend is missing something
If a page needs something the API doesn't have (an endpoint, a field, a filter),
don't work around it and don't fake it. Stop and tell me exactly what's needed:

- **What**: e.g. `GET /api/bookings` returning the logged-in user's bookings
- **Why**: which page needs it and what it will show
- **Request**: method, URL, parameters, and who can call it
- **Response**: the JSON shape you need, with an example
- **Errors**: any error cases the page should handle

Look at the backend code first so your request matches how the existing endpoints are
named and shaped. Then wait. When I tell you it's done, check it in the
OpenAPI spec.

## Stack
- **React + TypeScript**, created with **Vite**.
- **React Router** for pages (`/hotels`, `/hotels/:id`, `/bookings/:id`, ...).
- **TanStack Query** for loading data from the API (it gives loading/error states and
  caching).
- Plain CSS (one global stylesheet plus a CSS file per component when needed).
  No UI component library unless I agree.
- All API calls go through `src/api/`. Components never call `fetch` directly.
  All URLs are relative (`/api/...`).
- Keep code simple and readable. Prefer clear over clever.

## Running it
- **While developing:** `npm run dev` (Vite, http://localhost:5173). Vite forwards
  `/api` to the gateway at http://localhost:8080, so there's no CORS to deal with.
- **For the real setup and the demo:** the app is built (`npm run build`) and served by
  nginx behind the gateway at http://localhost:8080. nginx must send unknown paths to
  `index.html` so React Router can handle them.

## The API
- The full spec is at http://localhost:8080/openapi/v1.json. Read it before using
  an endpoint; don't guess field names. Write TypeScript types that match it.
- Errors come back as JSON with `status`, `errorCode`, `detail` and `traceId`.
  Decide what to show based on `errorCode`. For validation errors, show each message
  next to its form field.
- Known gap: there's no "list my bookings" endpoint yet. When we build the
  "My bookings" page, write a request for it to me (see above).

## The tricky parts (get these right)
1. **Login tokens.** Login returns an access token (15 min) and a refresh token.
   The refresh token changes every time it's used, and using an old one logs the user
   out everywhere. So only one refresh may happen at a time, even with several tabs
   open (use `navigator.locks`).
2. **Checkout.** `POST /api/bookings` needs an `Idempotency-Key` header. Create it once
   per checkout attempt and reuse it if the request is retried, so the user is never
   charged twice.
3. **Payment.** After Stripe, the user returns to `/bookings/:id`. The booking is
   confirmed by the backend later, so this page checks the status every 2 seconds
   while it's "Pending".
4. **Admin edits.** When loading a record, keep its `ETag`. Send it back as `If-Match`
   when saving. If the API returns 409, tell the user someone else changed it.

## Security rules
- React escapes text for us. **Never use `dangerouslySetInnerHTML`.**
- Never print tokens or passwords to the console.
- After login, only redirect to paths that start with `/` (not `//` or `http`).
- Hiding admin pages from non-admins is for convenience only; the backend is what
  actually protects them.

## Quality
- Every page that loads data shows: loading, empty, error and success.
- Every form input has a `<label>`. Works on a phone-sized screen (360 px).
- No TypeScript errors (`npx tsc --noEmit`) and no red errors in the browser console.
- When you finish a feature, check your own work against this file and tell me
  anything that doesn't follow it.

## Skills
- `react-expert`: when writing React/TypeScript code.
- `debugging-wizard`: when something is broken. Reproduce first, then fix.
- If a skill disagrees with this file, this file wins.
