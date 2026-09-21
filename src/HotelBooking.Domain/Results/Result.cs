using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HotelBooking.Domain.Results;

public static class Result
{
    public static Success Success => default;
    public static Created Created => default;
    public static Deleted Deleted => default;
    public static Updated Updated => default;
}

public sealed class Result<TValue> : IResult<TValue>
{
    private readonly List<Error>? _errors;
    private readonly TValue? _value;

    [JsonConstructor]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Result(TValue? value, List<Error>? errors, bool isSuccess)
    {
        if (isSuccess)
        {
            if (value is null)
            {
                _errors = [Error.Unexpected("Result.NullValue", "Cannot create a successful result with a null value.")];
                _value = default!;
                IsSuccess = false;
            }
            else
            {
                _value = value;
                _errors = [];
                IsSuccess = true;
            }
        }
        else
        {
            _errors = NormalizeErrors(errors);
            _value = default!;
            IsSuccess = false;
        }
    }

    private Result(Error error) => _errors = [error];

    private Result(List<Error> errors)
    {
        _errors = NormalizeErrors(errors);
        IsSuccess = false;
    }

    private Result(TValue value)
    {
        if (value is null)
        {
            _errors = [Error.Unexpected("Result.NullValue", "Cannot create a successful result with a null value.")];
            IsSuccess = false;
            return;
        }

        _value = value;

        IsSuccess = true;
    }

    public bool IsError => !IsSuccess;

    public Error TopError => _errors?.Count > 0 ? _errors[0] : default;

    public bool IsSuccess { get; }

    public List<Error> Errors => IsError ? _errors! : [];

    public TValue Value => IsSuccess ? _value! : default!;

    public static Result<TValue> From(TValue value) => new(value);

    public static implicit operator Result<TValue>(TValue value) => new(value);

    public static implicit operator Result<TValue>(Error error) => new(error);

    public static implicit operator Result<TValue>(List<Error> errors) => new(errors);

    public TNextValue Match<TNextValue>(Func<TValue, TNextValue> onValue, Func<List<Error>, TNextValue> onError)
    {
        return IsSuccess ? onValue(Value) : onError(Errors);
    }

    private static List<Error> NormalizeErrors(List<Error>? errors) =>
        errors is { Count: > 0 }
            ? errors
            : [Error.Unexpected("Result.EmptyErrors", "A failed result must contain at least one error.")];
}

public readonly record struct Success;
public readonly record struct Created;
public readonly record struct Deleted;
public readonly record struct Updated;
