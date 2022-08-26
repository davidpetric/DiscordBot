namespace bot;
using BotService;
using CliWrap;
using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Net;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Search;
using YoutubeExplode.Videos.Streams;

public class PlayManager : ModuleBase<SocketCommandContext>
{
    private const string YoutubeUrlVideoParam = "v=";
    private const int YoutubeVideoIdLength = 11;

    private readonly ILogger<PlayManager> _logger;
    private readonly YoutubeClient _youtube;
    public static IAudioClient? _audioClient;
    public static CancellationTokenSource? _cts;

    public PlayManager(ILogger<PlayManager> logger)
    {
        _logger = logger;
        _youtube = new YoutubeClient();
    }

    [Command("a", RunMode = RunMode.Async)]
    public async Task AddToPlayQueue()
    {

    }

    [Command("disconnect", RunMode = RunMode.Async)]
    [Alias("d")]
    public async Task Disconnect()
    {
        IVoiceChannel? target = ((IVoiceState)Context.User).VoiceChannel;
        if (target == null)
        {
            await ReplyAsync(StringResources.VoiceChannelNotConnectedError);
            return;
        }

        _cts?.Cancel();
        _audioClient?.Dispose();

        _cts = new CancellationTokenSource();
    }

    [Command("stop", RunMode = RunMode.Async)]
    [Alias("s")]
    public async Task Stop()
    {
        if (State.IsPlaying)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
        }

        _logger.LogInformation("Requesting cancel current song..");
    }

    [Command("curata", RunMode = RunMode.Async)]
    public async Task Clean()
    {
        List<IReadOnlyCollection<IMessage>>? messages = await Context.Channel.GetMessagesAsync(100).ToListAsync();

        List<IMessage>? messagesToDelete = messages
            .SelectMany(x => x)
            .ToList();

        foreach (IMessage? message in messagesToDelete)
        {
            await Context.Channel.DeleteMessageAsync(message);
        }

        await ReplyAsync($"");
    }

    [Command("canta", RunMode = RunMode.Async)]
    [Alias("p", "play", "c", "l")]
    public async Task HandlePlaySong([Remainder] string userSong)
    {
        string songId = userSong.Contains(YoutubeUrlVideoParam) ? GetSongIdFromId(userSong) : userSong;
        IVoiceChannel? target = ((IVoiceState)Context.User).VoiceChannel;
        if (target == null)
        {
            await ReplyAsync(StringResources.VoiceChannelNotConnectedError);
            return;
        }

        IReadOnlyList<VideoSearchResult>? searched = await _youtube.Search.GetVideosAsync(songId);
        if (!searched.Any())
        {
            await ReplyAsync(string.Format(StringResources.SongNotFound, userSong));
        }

        VideoSearchResult? first = searched.First();

        StreamManifest? streamManifest = await _youtube.Videos.Streams.GetManifestAsync(first.Id).ConfigureAwait(false);
        IStreamInfo? audio = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
        using Stream? ytStream = await _youtube.Videos.Streams.GetAsync(audio).ConfigureAwait(false);

        if (_audioClient == null || _audioClient.ConnectionState == ConnectionState.Disconnected)
        {
            _audioClient = await target.ConnectAsync().ConfigureAwait(false);
            _audioClient.Disconnected += AudioClient_Disconnected;
        }

        using MemoryStream? buffStream = new();

        try
        {
            if (State.IsPlaying)
            {
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

            }
            else
            {
                _cts = new CancellationTokenSource();
            }

            using AudioOutStream? _discordAudioOut = _audioClient.CreatePCMStream(AudioApplication.Music);

            await Cli.Wrap("ffmpeg")
                    .WithArguments(" -hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000 pipe:1")
                    .WithStandardInputPipe(PipeSource.FromStream(ytStream, true))
                    .WithStandardOutputPipe(PipeTarget.ToStream(buffStream, true))
                    .ExecuteAsync(_cts.Token)
                    .ConfigureAwait(false);

            State.IsPlaying = true;

            await ReplyAsync($"🎧 {first.Title} 🎧");

            Memory<byte> buffer = buffStream.ToArray().AsMemory(0, (int)buffStream.Length);
            await _discordAudioOut.WriteAsync(buffer, _cts.Token).ConfigureAwait(false);

            State.IsPlaying = false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Playing was cancelled for some reason..");
        }
        catch (WebSocketClosedException)
        {
            _audioClient = await target.ConnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }
    }

    private string GetSongIdFromId(string song)
    {
        int start = song.IndexOf(YoutubeUrlVideoParam) + 2;
        string songId = song.Substring(start, YoutubeVideoIdLength);

        return songId;
    }

    private Task AudioClient_Disconnected(Exception arg)
    {
        _audioClient?.Dispose();
        State.IsPlaying = false;

        return Task.CompletedTask;
    }
}