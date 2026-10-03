# Numeric Results &mdash; Fast Numeric Sampling

NumericResult channels perform high-speed acquisition on physical channels, computing summary statistics (mean, min, max, standard deviation) server-side. This avoids transferring large sample arrays over the network when only statistics are needed.

## Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `AcquireAsync(channel, target, samples = 1000, lsl?, usl?, ct?)` | `Task<NumericAcquisitionDto>` | Acquire and return the samples with their statistics in one call. |
| `GetChannelsAsync(ct?)` | `Task<List<NumericResultChannelDto>>` | All NumericResult channels with sampling capabilities. |
| `GetTargetsAsync(channelNetName, ct?)` | `Task<string[]>` | Physical channels that a NumericResult channel can sample. |
| `MeasureAsync(request, ct?)` | `Task<NumericMeasureResultDto>` | Trigger an acquisition (result cached server-side). |
| `GetMeanAsync(channelNetName, ct?)` | `Task<double>` | Mean of the last measurement. |
| `GetMinAsync(channelNetName, ct?)` | `Task<double>` | Minimum of the last measurement. |
| `GetMaxAsync(channelNetName, ct?)` | `Task<double>` | Maximum of the last measurement. |
| `GetStdDevAsync(channelNetName, ct?)` | `Task<double>` | Standard deviation of the last measurement. |
| `GetSamplesAsync(channelNetName, ct?)` | `Task<double[]>` | Raw sample array (only if `ReducedSet = false`). |

## One-Call Acquisition

`AcquireAsync` configures the NumericResult channel with the target and sample count, acquires, and returns every sample with the statistics in one call. It doesn't touch the cached result that `MeasureAsync` and the `Get…Async` statistics use. Give both spec limits (`lsl`, `usl`) to get `Cp` and `Cpk`.

```csharp
var acq = await client.NumericResults.AcquireAsync(
    channel: "0.4.ESH10000662.NumericResult",
    target:  "0.4.ESH10000662.VMON1",
    samples: 1000,
    lsl: 11.8, usl: 12.2);

Console.WriteLine($"{acq.Samples.Length} samples at {acq.SampleRate} Hz in {acq.DurationMs:0} ms");
Console.WriteLine($"mean={acq.Stats.Mean:F4} {acq.Unit}, stdev={acq.Stats.Stdev:F4}, Cpk={acq.Stats.Cpk:F2}");
```

- `samples` is 1 to 100 000, and `target` must be one of the channel's `PossibleTargetNames`; 400 otherwise. An acquisition longer than one call to the hardware app allows is refused with the largest sample count that fits.
- Acquisitions on one channel run one at a time. The call takes about `samples / SampleRate` plus the usual time.
- It reconfigures the channel, so while another client holds the [lease](lease.md) it is refused with 423.
- Statistics the samples can't give (the deviation of a single sample) are null, never NaN.

## Typical Workflow

```csharp
using AccordionQ2.WebApiClient.Models;

// 1. Discover available NumericResult channels
var channels = await client.NumericResults.GetChannelsAsync();
foreach (var ch in channels)
    Console.WriteLine($"{ch.NetName} (rate={ch.SampleRate} Hz, default={ch.DefaultSamples} samples)");

// 2. Check what a channel can sample
string[] targets = await client.NumericResults.GetTargetsAsync(channels[0].NetName);
Console.WriteLine("Available targets: " + string.Join(", ", targets));

// 3. Trigger acquisition (result cached server-side)
var meta = await client.NumericResults.MeasureAsync(new NumericMeasureRequest
{
    ChannelNetName = channels[0].NetName,
    TargetNetName  = targets[0],
    Samples        = 1000,
    ReducedSet     = true,
});
Console.WriteLine($"Acquired {meta.SampleCount} samples, duration={meta.Duration}");

// 4. Fetch summary statistics
double mean  = await client.NumericResults.GetMeanAsync(channels[0].NetName);
double stdev = await client.NumericResults.GetStdDevAsync(channels[0].NetName);
double min   = await client.NumericResults.GetMinAsync(channels[0].NetName);
double max   = await client.NumericResults.GetMaxAsync(channels[0].NetName);

Console.WriteLine($"Mean={mean:F6}, StdDev={stdev:F6}, Min={min:F6}, Max={max:F6}");
```

## Getting Raw Samples

When you need the full sample array, set `ReducedSet = false`:

```csharp
var meta = await client.NumericResults.MeasureAsync(new NumericMeasureRequest
{
    ChannelNetName = channels[0].NetName,
    TargetNetName  = targets[0],
    Samples        = 100,
    ReducedSet     = false,
});

double[] samples = await client.NumericResults.GetSamplesAsync(channels[0].NetName);
Console.WriteLine("First 5: " + string.Join(", ", samples.Take(5)));
```

> **Warning:** Calling `GetSamplesAsync()` after a measurement with `ReducedSet = true` throws `AccordionQ2ApiException` (HTTP 400) because raw samples are discarded when only statistics are computed. Re-measure with `ReducedSet = false` to retain the raw data.

## Models

### `NumericMeasureRequest`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ChannelNetName` | `string` | — | Net name of the NumericResult channel to use |
| `TargetNetName` | `string` | — | Net name of the physical channel to sample |
| `Samples` | `int` | `1000` | Number of samples to acquire |
| `ReducedSet` | `bool` | `true` | Discard raw samples after computing statistics |

### `NumericResultChannelDto`

| Property | Type | Description |
|----------|------|-------------|
| `NetName` | `string` | Net name of the NumericResult channel |
| `Alias` | `string` | Human-readable alias |
| `PossibleTargetNames` | `string[]` | Physical channels this channel can sample |
| `SampleRate` | `int` | Hardware sampling rate in Hz |
| `DefaultSamples` | `int` | Default number of samples configured on the channel |

### `NumericMeasureResultDto`

| Property | Type | Description |
|----------|------|-------------|
| `ChannelNetName` | `string` | NumericResult channel used |
| `TargetNetName` | `string` | Physical channel sampled |
| `SampleCount` | `int` | Number of raw samples retained (`0` when `ReducedSet` is `true`) |
| `SampleRate` | `int` | Actual sampling rate in Hz |
| `ReducedSet` | `bool` | Whether raw samples were discarded |
| `Started` | `DateTime` | Acquisition start timestamp |
| `Stopped` | `DateTime` | Acquisition stop timestamp |
| `Duration` | `TimeSpan` | Total acquisition duration |

### `NumericAcquisitionDto`

From `AcquireAsync`.

| Property | Type | Description |
|----------|------|-------------|
| `Channel` | `string` | NumericResult channel used |
| `Target` | `string` | Channel sampled |
| `Unit` | `string` | The target channel's unit |
| `SampleRate` | `int` | The sample rate the hardware reported, in Hz |
| `Started` | `DateTime` | Acquisition start |
| `DurationMs` | `double?` | How long the acquisition took |
| `Samples` | `double[]` | Every sample |
| `Stats` | `NumericStatsDto` | The statistics |

### `NumericStatsDto`

All but `Count` are `double?`, null where the samples can't give a value.

| Property | Description |
|----------|-------------|
| `Count` | Number of samples (`int`) |
| `Min`, `Max`, `Range` | Extremes and their difference |
| `Mean`, `Median` | Central values |
| `Stdev`, `Rms` | Standard deviation and root mean square |
| `Skewness`, `Kurtosis` | Shape of the distribution |
| `Cp`, `Cpk` | Process capability; set only when both `lsl` and `usl` were given |
