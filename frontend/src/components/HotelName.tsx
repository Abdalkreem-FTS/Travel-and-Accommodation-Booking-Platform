import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router';
import { getHotel } from '../api/hotels';

// A hotel's name as a link to its page, for places that only have its id (the
// cart, a booking). While the name loads, or if it can't be loaded, a plain
// "Hotel" link is shown instead of a spinner or an error: the name is a nicety.
export function HotelName({ hotelId }: { hotelId: string }) {
  const { data: hotel } = useQuery({
    queryKey: ['hotels', hotelId, 'name'],
    queryFn: () => getHotel(hotelId, { countAsView: false }),
    staleTime: 5 * 60_000,
  });

  return <Link to={`/hotels/${hotelId}`}>{hotel?.name ?? 'Hotel'}</Link>;
}
