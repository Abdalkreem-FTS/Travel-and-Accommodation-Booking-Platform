using System.Collections;
using System.Diagnostics;

namespace HotelBooking.Api.IntegrationTests.Observability;

public sealed class SpanCollection : ICollection<Activity>
{
    private readonly List<Activity> _spans = [];

    private readonly HashSet<ActivitySpanId> _seen = [];

    private readonly Lock _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _spans.Count;
            }
        }
    }

    public bool IsReadOnly => false;

    public void Add(Activity span)
    {
        lock (_gate)
        {
            if (_seen.Add(span.SpanId))
            {
                _spans.Add(span);
            }
        }
    }

    public IReadOnlyList<Activity> Snapshot()
    {
        lock (_gate)
        {
            return [.. _spans];
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _spans.Clear();
            _seen.Clear();
        }
    }

    public bool Contains(Activity span) => Snapshot().Contains(span);

    public void CopyTo(Activity[] array, int arrayIndex) => Snapshot().ToArray().CopyTo(array, arrayIndex);

    public bool Remove(Activity span)
    {
        lock (_gate)
        {
            _seen.Remove(span.SpanId);

            return _spans.Remove(span);
        }
    }

    public IEnumerator<Activity> GetEnumerator() => Snapshot().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
