interface StarsProps {
  rating: number;
}

// "★★★★☆". A screen reader reads the label instead of five symbols.
export function Stars({ rating }: StarsProps) {
  return (
    <span className="stars" role="img" aria-label={`${rating} out of 5 stars`}>
      {'★'.repeat(rating)}
      {'☆'.repeat(5 - rating)}
    </span>
  );
}
