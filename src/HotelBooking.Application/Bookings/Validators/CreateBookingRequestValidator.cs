using FluentValidation;

using HotelBooking.Application.Bookings.Dtos;
using HotelBooking.Domain.Bookings;

namespace HotelBooking.Application.Bookings.Validators;

public sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        When(request => request.Items is not null, () =>
        {
            RuleFor(request => request.Items!)
                .Must(items => items.Count > 0)
                .WithErrorCode("Booking.ItemsRequired")
                .WithMessage(
                    "Name at least one stay, or send no items at all to check out your cart.");

            RuleFor(request => request.Items!)
                .Must(items => items.Count <= Booking.MaxLines)
                .WithErrorCode("Booking.TooManyItems")
                .WithMessage($"A booking holds at most {Booking.MaxLines} stays.");

            RuleFor(request => request.Items!)
                .Must(NoStayAskedForTwice)
                .WithErrorCode("Booking.DuplicateItem")
                .WithMessage("The same room over the same nights is listed more than once.");

            RuleForEach(request => request.Items!).ChildRules(item =>
                item.RuleFor(stay => stay.RoomId)
                    .NotEmpty()
                    .WithErrorCode("Booking.RoomRequired")
                    .WithMessage("A room is required."));
        });
    }

    private static bool NoStayAskedForTwice(IReadOnlyList<BookingItemRequest> items) =>
        items.DistinctBy(item => (item.RoomId, item.CheckIn, item.CheckOut)).Count() == items.Count;
}
