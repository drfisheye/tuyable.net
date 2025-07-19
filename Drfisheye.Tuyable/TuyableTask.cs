using System;

namespace Drfisheye.Tuyable;

public class TuyableTask
{
	public uint SeqNum { get; private set; }

	public Object? Context { get; private set; }

    public Func<TuyableNotification, CancellationToken, Task<bool>> ResponseHandler { get; private set; }

	public TuyableTask(uint seqNum, CancellationToken cancelationToken, Func<TuyableNotification, CancellationToken, Task<bool>> responseHandler, object? context = null)
    {
        this.CancellationToken = cancelationToken;
        this.SeqNum = seqNum;
        this.Context = context;
        this.ResponseHandler = responseHandler;
        cancelationToken.Register(() =>
        {
            this.TaskCompletionSource.TrySetResult(false);
        });
    }

	public TaskCompletionSource<bool> TaskCompletionSource { get; private set; } = new TaskCompletionSource<bool>();
	public CancellationToken CancellationToken { get; private set; }

	public void SetResult(bool result)
	{
		if (!CancellationToken.IsCancellationRequested)
		{
			TaskCompletionSource.SetResult(result);
		}
	}
}

