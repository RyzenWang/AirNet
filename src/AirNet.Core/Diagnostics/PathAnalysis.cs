namespace AirNet.Core.Diagnostics;

/// <summary>One answered hop on the route to a target.</summary>
public sealed record TraceHop(int Index, string Address, double? LatencyMs, bool Responded)
{
    public bool TimedOut => !Responded;
}

/// <summary>The route, plus the hop where latency jumped the most.</summary>
public sealed record TraceResult(
    string Target,
    IReadOnlyList<TraceHop> Hops,
    bool Succeeded,
    int ReachedHop,
    string? Note = null)
{
    public static TraceResult Unavailable(string target, string note) =>
        new(target, [], false, 0, note);

    public int? SpikeIndex
    {
        get
        {
            int? index = null;
            var worst = 0d;
            double? previous = null;

            foreach (var hop in Hops)
            {
                if (!hop.Responded || hop.LatencyMs is not { } current)
                {
                    continue;   // silent hops must not break the comparison chain
                }

                if (previous is { } last)
                {
                    var delta = current - last;
                    if (delta > worst && delta >= 15)
                    {
                        worst = delta;
                        index = hop.Index;
                    }
                }

                previous = current;
            }

            return index;
        }
    }
}

/// <summary>Path MTU as discovered by the don't-fragment probe.</summary>
public sealed record MtuResult(int? MtuBytes, int PayloadBytes, bool Succeeded, string Note)
{
    public static MtuResult Unavailable(string note) => new(null, 0, false, note);
}

/// <summary>
/// Where the problem actually sits. Produced either from the main scan (coarse: gateway vs
/// reference) or from a trace (precise: per-hop deltas).
/// </summary>
public sealed record Attribution(
    string Culprit,
    string Headline,
    string Detail,
    Severity Severity,
    IReadOnlyList<PathSegment> Segments,
    bool IsPrecise);

/// <summary>One link of the chain: local network, carrier, or the far end.</summary>
public sealed record PathSegment(
    string Name,
    string LatencyText,
    string DeltaText,
    Severity Severity,
    string Note);
