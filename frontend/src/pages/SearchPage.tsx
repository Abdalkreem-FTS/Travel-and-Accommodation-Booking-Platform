import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { HOTEL_SORTS, searchHotels, type HotelSearch, type HotelSort } from '../api/hotels';
import { ApiError } from '../api/types';
import { formErrorMessage } from '../auth/formErrors';
import { HotelCard } from '../components/HotelCard';
import { LoadError } from '../components/LoadError';
import { Pagination } from '../components/Pagination';
import { SelectField } from '../components/SelectField';
import { SearchFilters } from './search/SearchFilters';
import { filtersKey, readHotelSearch, searchHref } from './search/searchParams';
import './SearchPage.css';

// The fields the filter form shows. A validation error for any other field
// (page, sort, ...) is shown above the results instead.
const FORM_FIELDS = ['checkIn', 'checkOut', 'adults', 'children', 'minPrice', 'maxPrice', 'roomType'];

const SORT_LABELS: Record<HotelSort, string> = {
  price: 'Lowest price',
  stars: 'Most stars',
  name: 'Name',
};

// /hotels?cityId=...&stars=4&page=2
//
// The URL is where the filters live. This component doesn't keep them in state:
// every render reads them from the URL, and changing them means going to a new
// URL. That is why Back, reload and shared links all show the same search.
export function SearchPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const search = readHotelSearch(searchParams);
  // The dates and guests go along to a hotel's page, so its rooms are priced for them.
  const stay = { checkIn: search.checkIn, checkOut: search.checkOut, adults: search.adults, children: search.children };

  const { data, isPending, isError, error, isPlaceholderData, refetch } = useQuery({
    // The filters are part of the key, so every search is cached on its own, and
    // a new key (new filters or page) starts a new request by itself.
    queryKey: ['hotels', 'search', search],
    queryFn: () => searchHotels(search),
    // While the next page or search loads, keep showing the current results
    // instead of blanking the list. `isPlaceholderData` is true meanwhile.
    placeholderData: keepPreviousData,
  });

  // A 400 means a filter broke a rule. Its messages go next to the form fields.
  const isInvalid = error instanceof ApiError && error.status === 400;
  const fieldErrors = isInvalid ? error.errors : {};
  const hasFormFieldErrors = FORM_FIELDS.some((field) => field in fieldErrors);
  // Messages the form has no field for, or null when there are none.
  const otherErrors = isInvalid ? formErrorMessage(error, FORM_FIELDS) : null;

  // New filters start again at page 1 and keep the chosen sort order.
  function handleSearch(filters: HotelSearch) {
    navigate(searchHref({ ...filters, sort: search.sort }));
  }

  function handleSort(sort: string) {
    navigate(searchHref(search, { sort: sort as HotelSort, page: undefined }));
  }

  return (
    <div className="search-page">
      <h1>Hotels</h1>

      <div className="search-layout">
        {/*
          `key` tells React which component this is. When the key changes, React
          throws the old SearchFilters away and makes a fresh one, which fills its
          form from the URL again. So pressing Back or "Clear filters" resets the
          form, while changing only the page or sort leaves what the user typed.
        */}
        <SearchFilters key={filtersKey(search)} initial={search} onSearch={handleSearch} errors={fieldErrors} />

        <section className="search-results" aria-labelledby="results-heading" aria-busy={isPlaceholderData}>
          <div className="search-results-header">
            <h2 id="results-heading" className="search-count">
              {data ? countText(data.totalCount) : 'Results'}
            </h2>
            <SelectField label="Sort by" value={search.sort ?? 'price'} onChange={handleSort}>
              {HOTEL_SORTS.map((sort) => (
                <option key={sort} value={sort}>
                  {SORT_LABELS[sort]}
                </option>
              ))}
            </SelectField>
          </div>

          {isPlaceholderData && <p role="status">Updating results…</p>}

          {isPending ? (
            <p role="status">Searching hotels…</p>
          ) : isError ? (
            isInvalid ? (
              <div role="alert" className="error-box">
                {hasFormFieldErrors && <p>Some filters need fixing. See the messages next to them.</p>}
                {otherErrors && <p>{otherErrors}</p>}
              </div>
            ) : (
              <LoadError what="the hotels" error={error} onRetry={() => refetch()} />
            )
          ) : data.totalCount === 0 ? (
            <p className="muted">
              No hotels match these filters. Try fewer filters, or <Link to="/hotels">clear them all</Link>.
            </p>
          ) : data.items.length === 0 ? (
            // The URL asks for a page past the last one (page=99, or results shrank).
            <p className="muted">
              There is no page {data.page}. <Link to={searchHref(search, { page: undefined })}>Go to page 1</Link>.
            </p>
          ) : (
            <>
              <ul className="card-grid">
                {data.items.map((hotel) => (
                  <li key={hotel.id}>
                    <HotelCard hotel={hotel} stay={stay} />
                  </li>
                ))}
              </ul>
              <Pagination
                page={data.page}
                totalPages={data.totalPages}
                hrefFor={(page) => searchHref(search, { page: page === 1 ? undefined : page })}
              />
            </>
          )}
        </section>
      </div>
    </div>
  );
}

function countText(totalCount: number): string {
  return totalCount === 1 ? '1 hotel' : `${totalCount} hotels`;
}
