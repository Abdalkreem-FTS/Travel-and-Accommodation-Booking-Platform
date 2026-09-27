import { useCurrentUser } from '../auth/session';
import { DealsSection } from './home/DealsSection';
import { RecentlyViewedSection } from './home/RecentlyViewedSection';
import { TrendingCitiesSection } from './home/TrendingCitiesSection';
import './HomePage.css';

// Each section loads its own data, so a failure in one leaves the others working.
export function HomePage() {
  const user = useCurrentUser();

  return (
    <>
      <h1>Find your next stay</h1>
      <DealsSection />
      {user && <RecentlyViewedSection userId={user.id} />}
      <TrendingCitiesSection />
    </>
  );
}
