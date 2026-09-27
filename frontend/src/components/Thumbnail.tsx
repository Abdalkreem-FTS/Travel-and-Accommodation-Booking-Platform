interface ThumbnailProps {
  url: string | null;
}

// A card's picture, or a plain grey box of the same size when there is none,
// so cards with and without a picture line up.
export function Thumbnail({ url }: ThumbnailProps) {
  if (url === null) {
    return <div className="thumbnail thumbnail-empty" aria-hidden="true" />;
  }
  // alt="" because the card's text already names the hotel or city; a screen
  // reader would only read the name twice.
  return <img className="thumbnail" src={url} alt="" loading="lazy" />;
}
