using HotelBooking.Domain.Common;
using HotelBooking.Domain.Results;

namespace HotelBooking.Domain.Hotels;

public sealed record HotelImage
{
    public const int MaxUrlLength = 2048;

    public const int MaxCaptionLength = 200;

    private HotelImage(string url, string? caption, int position)
    {
        Url = url;
        Caption = caption;
        Position = position;
    }

    public string Url { get; private set; }

    public string? Caption { get; private set; }

    public int Position { get; private set; }

    public static Result<HotelImage> Create(string? url, string? caption)
    {
        List<Error> errors = [];

        var trimmedUrl = TextField.RequireUrl(
            url, MaxUrlLength, HotelErrors.ImageUrlRequired, HotelErrors.ImageUrlInvalid, errors);

        var trimmedCaption = TextField.Optional(
            caption, MaxCaptionLength, HotelErrors.ImageCaptionTooLong, errors);

        return errors.Count > 0 ? errors : new HotelImage(trimmedUrl, trimmedCaption, 0);
    }

    internal HotelImage AtPosition(int position) => new(Url, Caption, position);
}
