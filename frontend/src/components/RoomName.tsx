import { useQuery } from '@tanstack/react-query';
import { getRoom } from '../api/rooms';

// "Luxury room 204" for a room id. Like HotelName, it falls back to plain
// "Room" while loading or when the room can't be read (it may have been removed
// since it was booked).
export function RoomName({ roomId }: { roomId: string }) {
  const { data: room } = useQuery({
    queryKey: ['rooms', roomId],
    queryFn: () => getRoom(roomId),
    staleTime: 5 * 60_000,
  });

  return <>{room ? `${room.type} room ${room.number}` : 'Room'}</>;
}
