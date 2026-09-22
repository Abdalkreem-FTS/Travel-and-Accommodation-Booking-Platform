using System.Text;
using HotelBooking.Domain.Results;

namespace HotelBooking.Api.Errors;

internal static class ErrorProblemMapping
{
    internal const string ValidationTitle = "One or more validation errors occurred.";

    private const string UnnamedFieldKey = "request";

    public static int StatusCode(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation or ErrorType.BadRequest => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
        ErrorType.PreconditionRequired => StatusCodes.Status428PreconditionRequired,
        ErrorType.PaymentRequired => StatusCodes.Status402PaymentRequired,
        ErrorType.BadGateway => StatusCodes.Status502BadGateway,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorType.Timeout => StatusCodes.Status504GatewayTimeout,
        ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status500InternalServerError
    };

    public static string Title(Error error)
    {
        var code = error.Code ?? string.Empty;
        var lastSegment = code[(code.LastIndexOf('.') + 1)..];
        var words = SplitWords(lastSegment);

        if (words.Count == 0)
        {
            return "Server error";
        }

        var first = char.ToUpperInvariant(words[0][0]) + words[0][1..];
        var rest = words.Skip(1).Select(word => IsAcronym(word) ? word : word.ToLowerInvariant());

        return string.Join(' ', [first, .. rest]);
    }

    public static Dictionary<string, string[]>? ValidationFailures(IReadOnlyCollection<Error> errors)
    {
        var validationErrors = errors.Where(error => error.Type == ErrorType.Validation).ToList();

        return validationErrors.Count == 0
            ? null
            : validationErrors
                .GroupBy(error => string.IsNullOrWhiteSpace(error.Field) ? UnnamedFieldKey : error.Field)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Description).ToArray(),
                    StringComparer.Ordinal);
    }

    private static List<string> SplitWords(string? code)
    {
        var words = new List<string>();

        if (string.IsNullOrEmpty(code))
        {
            return words;
        }

        var word = new StringBuilder();

        for (var i = 0; i < code.Length; i++)
        {
            var current = code[i];

            if (current is '.' or '-' or '_' or ' ')
            {
                Flush(words, word);
                continue;
            }

            if (char.IsUpper(current) && word.Length > 0 && StartsNewWord(code, i))
            {
                Flush(words, word);
            }

            word.Append(current);
        }

        Flush(words, word);

        return words;
    }

    private static bool StartsNewWord(string code, int index) =>
        !char.IsUpper(code[index - 1]) ||
        (index + 1 < code.Length && char.IsLower(code[index + 1]));

    private static void Flush(List<string> words, StringBuilder word)
    {
        if (word.Length <= 0)
        {
            return;
        }

        words.Add(word.ToString());
        word.Clear();
    }

    private static bool IsAcronym(string word) =>
        word.Length > 1 && word.All(char.IsUpper);
}
