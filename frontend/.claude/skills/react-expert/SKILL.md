---
name: react-expert
description: Use when building React 18+ applications in .jsx or .tsx files (this project: Vite + React + TypeScript single-page app). Creates components, implements custom hooks, debugs rendering issues, and implements state management and data fetching. Use whenever writing or changing any .tsx/.ts file in frontend/src.
license: MIT
metadata:
  author: https://github.com/Jeffallan
  version: "1.1.0"
  domain: frontend
  triggers: React, JSX, hooks, useState, useEffect, useContext, React 19, Suspense, TanStack Query, Redux, Zustand, component, frontend
  role: specialist
  scope: implementation
  output-format: code
  related-skills: fullstack-guardian, playwright-expert, test-master
---

# React Expert

Senior React specialist. This project is a Vite single-page app with React Router and TanStack Query. It does NOT use Next.js or Server Components. CLAUDE.md wins over this file.

## When to Use This Skill

- Building new React components or features
- Implementing state management (local state, Context; ask before adding a library)
- Optimizing React performance
- Setting up React project architecture
- Implementing forms with React 19 actions
- Data fetching patterns with TanStack Query or `use()`

## Core Workflow

1. **Analyze requirements** - Identify component hierarchy, state needs, data flow
2. **Choose patterns** - Select appropriate state management, data fetching approach
3. **Implement** - Write TypeScript components with proper types
4. **Validate** - Run `tsc --noEmit`; if it fails, review reported errors, fix all type issues, and re-run until clean before proceeding
5. **Optimize** - Ensure accessibility; add memoization only for real performance problems; if new type errors are introduced, return to step 4
6. **Check** - Open the page in the browser and check the DevTools console. Only write tests when the human asks (see CLAUDE.md)

## Reference Guide

Load detailed guidance based on context:

| Topic | Reference | Load When |
|-------|-----------|-----------|
| React 19 | `references/react-19-features.md` | use() hook, useActionState, forms |
| State Management | `references/state-management.md` | Context, Zustand, Redux, TanStack |
| Hooks | `references/hooks-patterns.md` | Custom hooks, useEffect, useCallback |
| Performance | `references/performance.md` | memo, lazy, virtualization |
| Testing | `references/testing-react.md` | Testing Library, mocking |

## Key Patterns

### React 19 Form with `useActionState`
```tsx
import { useActionState } from 'react';

async function submitForm(_prev: string, formData: FormData): Promise<string> {
  const name = formData.get('name') as string;
  // call the API through src/api here
  return `Hello, ${name}!`;
}

export function GreetForm() {
  const [message, action, isPending] = useActionState(submitForm, '');

  return (
    <form action={action}>
      <input name="name" required />
      <button type="submit" disabled={isPending}>
        {isPending ? 'Submitting…' : 'Submit'}
      </button>
      {message && <p>{message}</p>}
    </form>
  );
}
```

### Custom Hook with Cleanup
```tsx
import { useState, useEffect } from 'react';

function useWindowWidth(): number {
  const [width, setWidth] = useState(() => window.innerWidth);

  useEffect(() => {
    const handler = () => setWidth(window.innerWidth);
    window.addEventListener('resize', handler);
    return () => window.removeEventListener('resize', handler); // cleanup
  }, []);

  return width;
}
```

## Constraints

### MUST DO
- Use TypeScript with strict mode
- Implement error boundaries for graceful failures
- Use `key` props correctly (stable, unique identifiers)
- Clean up effects (return cleanup function)
- Use semantic HTML and ARIA for accessibility
- Only add memoization (useMemo, useCallback, memo) when there is a real performance problem
- Show a loading state for every async operation (e.g. TanStack Query's isPending)

### MUST NOT DO
- Mutate state directly
- Use array index as key for dynamic lists
- Forget useEffect cleanup (memory leaks)
- Ignore React strict mode warnings
- Skip error boundaries in production

## Output Templates

When implementing React features, provide:
1. Component file with TypeScript types
2. Brief explanation of key decisions, in plain words for a React beginner

## Knowledge Reference

React 19, use() hook, Suspense, TypeScript, TanStack Query, Zustand, Redux Toolkit, React Router, React Testing Library, Vitest/Jest, accessibility (WCAG)

[Documentation](https://jeffallan.github.io/claude-skills/skills/frontend/react-expert/)
