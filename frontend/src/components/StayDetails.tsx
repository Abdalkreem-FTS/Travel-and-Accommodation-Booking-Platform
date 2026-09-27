import { formatDate, guestsText } from '../format';
import { RoomName } from './RoomName';
import './StayDetails.css';

interface StayDetailsProps {
  roomId: string;
  checkIn: string;
  checkOut: string;
  nights: number;
  adults: number;
  children: number;
}

// Which room, which nights, how many guests. A cart item and a booking line
// both have these fields, so the cart and the booking page share this.
export function StayDetails({ roomId, checkIn, checkOut, nights, adults, children }: StayDetailsProps) {
  return (
    <div className="stay-details">
      <strong>
        <RoomName roomId={roomId} />
      </strong>
      <span>
        {formatDate(checkIn)} – {formatDate(checkOut)}
        <span className="muted"> ({nights === 1 ? '1 night' : `${nights} nights`})</span>
      </span>
      <span className="muted">
        {guestsText(adults, 'adult')}
        {children > 0 && `, ${guestsText(children, 'child')}`}
      </span>
    </div>
  );
}
