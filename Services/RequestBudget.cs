using System.Runtime.CompilerServices;
using Assistant.Models;

namespace Assistant.Services;

/// <summary>One deadline covers discovery, connection, headers, and response reads.</summary>
public sealed class RequestBudget : IDisposable
{
    private readonly CancellationTokenSource _total;
    private readonly CancellationTokenSource _first;
    private readonly int _totalSeconds;
    private readonly int _firstSeconds;
    private bool _receivedText;
    public CancellationToken Token => _first.Token;
    public RequestBudget(AppSettings settings, CancellationToken caller, bool stream)
    {
        _totalSeconds = Math.Clamp(settings.RequestTimeoutSeconds, 5, 120);
        _firstSeconds = Math.Min(_totalSeconds, 20);
        _total = CancellationTokenSource.CreateLinkedTokenSource(caller);
        _total.CancelAfter(TimeSpan.FromSeconds(_totalSeconds));
        _first = CancellationTokenSource.CreateLinkedTokenSource(_total.Token);
        if (stream) _first.CancelAfter(TimeSpan.FromSeconds(_firstSeconds));
        else _receivedText = true;
    }
    public void MarkTextReceived() { _receivedText = true; _first.CancelAfter(Timeout.Infinite); }
    private GeminiApiException TimedOut() => new(_receivedText
        ? $"Response exceeded {_totalSeconds}s. Try a faster model or increase the request timeout in Settings."
        : $"No response text within {_firstSeconds}s. The provider may be thinking, queued, or unavailable. Try a faster model.", 408);
    public static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> action, AppSettings settings, CancellationToken caller)
    {
        using var budget = new RequestBudget(settings, caller, false);
        try { return await action(budget.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested) { throw budget.TimedOut(); }
    }
    public static async IAsyncEnumerable<string> StreamAsync(Func<CancellationToken, IAsyncEnumerable<string>> action, AppSettings settings, [EnumeratorCancellation] CancellationToken caller)
    {
        using var budget = new RequestBudget(settings, caller, true);
        await using var iterator = action(budget.Token).GetAsyncEnumerator(budget.Token);
        while (true)
        {
            bool next;
            try { next = await iterator.MoveNextAsync().ConfigureAwait(false); }
            catch (OperationCanceledException) when (!caller.IsCancellationRequested) { throw budget.TimedOut(); }
            if (!next) break;
            if (iterator.Current.Length > 0) budget.MarkTextReceived();
            yield return iterator.Current;
        }
    }
    public void Dispose() { _first.Dispose(); _total.Dispose(); }
}
