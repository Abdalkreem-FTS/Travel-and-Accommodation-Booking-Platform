// The page number from the URL. "page=abc" or "page=0" means page 1, like no page at all.
export function readPage(value: string | null): number {
  const page = Number(value);
  return Number.isInteger(page) && page >= 1 ? page : 1;
}
