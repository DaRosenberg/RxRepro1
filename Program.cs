using System;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

var random = new Random();

long result = 0; // Total size of all files

var numWorkers = 0;
var numFiles = 0;

await
	Directory.EnumerateFiles(
		path: Path.GetPathRoot(Environment.SystemDirectory),
		searchPattern: "*",
		new EnumerationOptions() { RecurseSubdirectories = true })
	.ToObservable(TaskPoolScheduler.Default)
	.Select(filePath =>
		Observable.Defer(() =>
			Observable.StartAsync(async () =>
			{
				Interlocked.Increment(ref numWorkers);

				try
				{
					var contentBytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
					var size = contentBytes.Length;
					return size;
				}
				catch
				{
					return 0;
				}
				finally
				{
					Interlocked.Decrement(ref numWorkers);
				}
			}, TaskPoolScheduler.Default))
		.SubscribeOn(TaskPoolScheduler.Default))
	.Merge(maxConcurrent: 24)
	.ForEachAsync(size =>
	{
		var currentCount = Interlocked.Increment(ref numFiles);
		var currentResult = Interlocked.Add(ref result, size);
		if (currentCount % 100 == 0)
			Console.Write($"{numWorkers} active workers, {numFiles} files processed, current total size is {currentResult}\r");
	})
	.ConfigureAwait(false);

Console.WriteLine($"Finished; {numFiles} processed, total size of all files is {result}.");
