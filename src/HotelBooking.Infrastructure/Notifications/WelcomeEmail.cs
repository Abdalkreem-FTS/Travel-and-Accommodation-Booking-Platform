using System.Net;

namespace HotelBooking.Infrastructure.Notifications;

internal static class WelcomeEmail
{
    public const string Subject = "Welcome to Hotel Booking";

    public static string TextBody(string firstName) =>
        $"""
         Hello {firstName},

         Your account is ready. Search hotels, keep the stays you like in your cart, and check out
         when you are ready - the nights are held for you while you pay.

         You can cancel any booking free of charge up to 48 hours before check-in.

         Welcome aboard.
         """;

    public static string HtmlBody(string firstName) =>
        $"""
         <!DOCTYPE html>
         <html lang="en">
         <body style="font-family:system-ui,sans-serif;color:#1a1a1a">
           <p>Hello {WebUtility.HtmlEncode(firstName)},</p>
           <p>Your account is ready. Search hotels, keep the stays you like in your cart, and check out
           when you are ready &mdash; the nights are held for you while you pay.</p>
           <p>You can cancel any booking free of charge up to 48 hours before check-in.</p>
           <p>Welcome aboard.</p>
         </body>
         </html>
         """;
}
