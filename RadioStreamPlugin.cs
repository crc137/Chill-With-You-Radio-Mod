using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using BepInEx;
using Bulbul;
using HarmonyLib;
using UnityEngine;

namespace RadioStreamPlugin
{
    internal static class FileLog
    {
        static readonly string Path = System.IO.Path.Combine(Paths.PluginPath, "radiostream.log");

        static readonly object L = new object();

        public static void Write(string msg)
        {
            try
            {
                lock (L)
                {
                    System.IO.File.AppendAllText(Path, DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + "\n");
                }
            }
            catch { }
        }
    }

    [BepInPlugin("com.radio.streamplugin", "Radio Stream Plugin", "26.1.3")]
    public class RadioPlugin : BaseUnityPlugin
    {
        private void Awake()
        {
            FileLog.Write("Awake start");
            var harmony = new Harmony("com.radio.streamplugin");
            harmony.PatchAll(typeof(RadioPlugin));
            Logger.LogInfo("[RadioStream] Plugin loaded");
            FileLog.Write("Plugin loaded (patch applied)");
        }

        [HarmonyPatch(typeof(MusicService), nameof(MusicService.Load))]
        [HarmonyPrefix]
        [HarmonyPriority(900)]
        static bool OnLoad(MusicService __instance, IReadOnlyCollection<GameAudioInfo> musicItems)
        {
            FileLog.Write("OnLoad HIT musicItems=" + (musicItems?.Count.ToString() ?? "null"));
            var clearMethod = typeof(MusicService).GetMethod("ClearPlayingList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            clearMethod?.Invoke(__instance, null);
            FileLog.Write("ClearPlayingList done");
            RadioInjector.Inject(__instance);
            FileLog.Write("Inject called");

            return false;
        }
    }

    internal sealed class ForwardOnlyStream : Stream
    {
        private readonly Stream _inner;
        private long _position;

        public ForwardOnlyStream(Stream inner) {_inner = inner;}

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = _inner.Read(buffer, offset, count);
            if (n > 0) _position += n;
            return n;
        }

        public override int ReadByte()
        {
            int b = _inner.ReadByte();
            if (b >= 0) _position++;
            return b;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    internal sealed class PcmRingBuffer
    {
        private readonly float[] _buf;
        private readonly object _lock = new object();
        private int _front;
        private int _count;

        public PcmRingBuffer(int capacity) {_buf = new float[capacity];}

        public int Write(float[] data, int offset, int n)
        {
            lock (_lock)
            {
                int written = 0;
                while (n > 0 && _count < _buf.Length)
                {
                    int toWrite = Math.Min(Math.Min(n, _buf.Length - _count), _buf.Length - ((_front + _count) % _buf.Length));
                    Array.Copy(data, offset, _buf, (_front + _count) % _buf.Length, toWrite);
                    _count += toWrite;
                    n -= toWrite;
                    written += toWrite;
                }
                return written;
            }
        }

        public int Read(float[] data, int offset, int n)
        {
            lock (_lock)
            {
                int read = 0;
                while (n > 0 && _count > 0)
                {
                    int toRead = Math.Min(Math.Min(n, _count), _buf.Length - _front);
                    Array.Copy(_buf, _front, data, offset, toRead);
                    _front = (_front + toRead) % _buf.Length;
                    _count -= toRead;
                    offset += toRead;
                    n -= toRead;
                    read += toRead;
                }
                return read;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _front = 0;
                _count = 0;
            }
        }

        public int Available => _count;
    }

    internal sealed class RadioStation
    {
        public string Uuid;
        public string Url;
        public string Title;
        public string Author;
        public string Description;

        public RadioStreamer Streamer;
        public AudioClip Clip;
        public int Reads;
    }

    internal static class RadioStationConfig
    {
        static readonly string ConfigPath = System.IO.Path.Combine(Paths.PluginPath, "radiostations.txt");
        const int RelayPort = 3000;
        static string RewriteUrl(string url)
        {
            string u = url.Trim();
            int idx = u.IndexOf("watch?v=", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                string id = u.Substring(idx + "watch?v=".Length);
                int amp = id.IndexOfAny(new[] { '&', '#', '?' });
                if (amp >= 0) id = id.Substring(0, amp);
                if (id.Length == 11)
                {
                    FileLog.Write("youtube rewritten: " + id);
                    return "http://127.0.0.1:" + RelayPort + "/" + id;
                }
            }
            idx = u.IndexOf("youtu.be/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                string id = u.Substring(idx + "youtu.be/".Length);
                int amp = id.IndexOfAny(new[] { '&', '#', '?' });
                if (amp >= 0) id = id.Substring(0, amp);
                if (id.Length == 11)
                {
                    FileLog.Write("youtube rewritten: " + id);
                    return "http://127.0.0.1:" + RelayPort + "/" + id;
                }
            }
            return u;
        }

        public static List<RadioStation> Load()
        {
            List<RadioStation> list = new List<RadioStation>();

            FileLog.Write("config path: " + ConfigPath);
            bool haveFile = false;
            try {haveFile = File.Exists(ConfigPath);}
            catch { }

            if (haveFile)
            {
                try
                {
                    foreach (string rawLine in File.ReadAllLines(ConfigPath))
                    {
                        string line = rawLine.Trim();
                        if (line.Length == 0) continue;
                        if (line.StartsWith("#")) continue;

                        string[] parts = line.Split('|');
                        if (parts.Length < 2) continue;

                        string url = parts[0].Trim();
                        string title = (parts.Length > 1 ? parts[1].Trim() : url);
                        string author = (parts.Length > 2 ? parts[2].Trim() : "");
                        string desc = (parts.Length > 3 ? parts[3].Trim() : "");

                        if (url.Length == 0) continue;

                        list.Add(new RadioStation
                        {
                            Uuid = "crc137-radio-" + list.Count,
                            Url = RewriteUrl(url),
                            Title = title,
                            Author = author,
                            Description = desc
                        });
                    }
                }
                catch (Exception ex) {FileLog.Write("config read error: " + ex.Message);}
            }

            if (list.Count == 0)
            {
                FileLog.Write("config empty/missing, using defaults");
            }

            FileLog.Write("total stations: " + list.Count);
            for (int i = 0; i < list.Count; i++) {FileLog.Write("  [" + i + "] " + list[i].Title + " | " + list[i].Author + " | " + list[i].Url);}
            return list;
        }

        static void AddDefault(List<RadioStation> list, string url, string title, string author, string desc)
        {
            list.Add(new RadioStation
            {
                Uuid = "crc137-radio-" + list.Count,
                Url = url,
                Title = title,
                Author = author,
                Description = desc
            });
        }
    }

    internal class RadioStreamer : IDisposable
    {
        readonly string _url;
        readonly object _stateLock = new object();

        Thread _thread;
        volatile bool _started;
        volatile object _token = new object();
        volatile PcmRingBuffer _buffer;
        HttpWebRequest _activeRequest;

        volatile int _lastReadAtTicks;

        const int IdleParkMs = 3000;
        const int BufferSeconds = 5;
        const int PrebufferSamples = 44100 * 2 * 3;

        public int SampleRate { get; private set; }
        public int Channels { get; private set; }
        public string LastError { get; private set; }
        public long TotalSamples { get; private set; }
        public long DecodedSamples { get; private set; }
        public long PlayedSamples { get; private set; }

        public bool IsRunning { get { lock (_stateLock) { return _started; } } }

        public int LastReadAt { get { return _lastReadAtTicks; } }

        public int BufferedMilliseconds
        {
            get
            {
                PcmRingBuffer b = _buffer;
                if (SampleRate <= 0 || Channels <= 0 || b == null)
                    return 0;
                return b.Available / Channels * 1000 / SampleRate;
            }
        }

        public RadioStreamer(string url)
        {
            _url = url;
            _lastReadAtTicks = Environment.TickCount;
        }

        public void SetFormat(int sampleRate, int channels)
        {
            lock (_stateLock)
            {
                SampleRate = sampleRate;
                Channels = channels;
            }
        }

        public void EnsureStarted()
        {
            lock (_stateLock)
            {
                if (_started) return;
                _started = true;
                if (_buffer == null)
                    _buffer = new PcmRingBuffer(44100 * 2 * BufferSeconds);
                _lastReadAtTicks = Environment.TickCount;

                var token = new object();
                _token = token;
                _thread = new Thread(() => DecodeLoop(token))
                {
                    IsBackground = true,
                    Name = "RadioStream Decoder"
                };
                _thread.Start();
            }
        }

        public void StopAndRelease()
        {
            PcmRingBuffer old;
            lock (_stateLock)
            {
                if (!_started) return;
                _started = false;
                _token = new object();
                HttpWebRequest req = _activeRequest;
                _activeRequest = null;
                if (req != null) { try { req.Abort(); } catch { } }
                old = _buffer;
                _buffer = null;
            }
            old?.Clear();
            lock (_stateLock)
            {
                _thread = null;
                SampleRate = 0;
                Channels = 0;
                TotalSamples = 0;
                DecodedSamples = 0;
                PlayedSamples = 0;
                LastError = null;
            }
        }

        public void Dispose() { StopAndRelease(); }

        void DecodeLoop(object token)
        {
            while (true)
            {
                if (!ReferenceEquals(token, _token)) return;

                HttpWebRequest request = null;
                try
                {
                    request = (HttpWebRequest)WebRequest.Create(_url);
                    request.Method = "GET";
                    request.Timeout = 6000;
                    request.ReadWriteTimeout = 6000;
                    request.UserAgent = "Mozilla/5.0 (X11; Linux x86_64)";
                    request.Accept = "audio/mpeg,*/*";

                    lock (_stateLock)
                    {
                        if (!ReferenceEquals(token, _token))
                        {
                            try { request.Abort(); } catch { }
                            return;
                        }
                        _activeRequest = request;
                    }

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    using (Stream stream = response.GetResponseStream())
                    {
                        lock (_stateLock) { if (ReferenceEquals(_activeRequest, request)) _activeRequest = null; }
                        if (!ReferenceEquals(token, _token)) return;
                        LastError = null;
                        DecodeStream(stream, token);
                    }
                }
                catch (Exception ex)
                {
                    if (!ReferenceEquals(token, _token)) return;
                    LastError = ex.Message;
                    Debug.LogError("[RadioStream] stream error: " + ex.Message);
                    try { if (request != null) request.Abort(); } catch { }
                    Thread.Sleep(500);
                }
            }
        }

        internal bool IsIdleParked()
        {
            PcmRingBuffer b = _buffer;
            if (b == null || b.Available < PrebufferSamples)
                return false;
            int sinceRead = Environment.TickCount - _lastReadAtTicks;
            if (sinceRead < 0) sinceRead = 0;
            return sinceRead >= IdleParkMs;
        }

        void DecodeStream(Stream httpStream, object token)
        {
            NLayer.MpegFile mpeg = null;
            PcmRingBuffer buf = _buffer;
            try
            {
                using (Stream fwd = new ForwardOnlyStream(httpStream))
                {
                    mpeg = new NLayer.MpegFile(fwd);
                    var info = mpeg;
                    int sr = info.SampleRate;
                    int ch = info.Channels;
                    if (sr <= 0 || ch <= 0) throw new InvalidDataException("bad format " + sr + "/" + ch);
                    lock (_stateLock)
                    {
                        SampleRate = sr;
                        Channels = ch;
                    }
                    Debug.Log("[RadioStream] format: " + sr + "Hz " + ch + "ch");

                    int framesPerRead = 4096;
                    float[] chunk = new float[framesPerRead * ch];
                    while (ReferenceEquals(token, _token))
                    {
                        if (buf != null && IsIdleParked())
                        {
                            Thread.Sleep(100);
                            continue;
                        }

                        int read = info.ReadSamples(chunk, 0, chunk.Length);
                        if (read <= 0) {break;}
                        int n = (read / ch) * ch;
                        if (n > 0 && buf != null)
                        {
                            int writtenTotal = 0;
                            while (writtenTotal < n && ReferenceEquals(token, _token))
                            {
                                int written = buf.Write(chunk, writtenTotal, n - writtenTotal);
                                writtenTotal += written;
                                if (written == 0)
                                    Thread.Sleep(10);
                            }
                            TotalSamples += writtenTotal / ch;
                            DecodedSamples += writtenTotal;
                        }
                    }
                }
            }
            catch (ThreadAbortException) { }
            catch (Exception ex)
            {
                if (!ReferenceEquals(token, _token)) return;
                LastError = ex.Message;
                Debug.LogError("[RadioStream] decode error: " + ex.Message);
            }
            finally
            {
                mpeg?.Dispose();
            }
        }

        public int ReadPcm(float[] data, int offset, int count)
        {
            _lastReadAtTicks = Environment.TickCount;
            EnsureStarted();
            PcmRingBuffer b = _buffer;
            if (b == null) return 0;
            int read = b.Read(data, offset, count);
            PlayedSamples += read;
            return read;
        }

        public void ClearBuffer() { _buffer?.Clear(); }
    }

    internal class RadioInjector : MonoBehaviour
    {
        static RadioInjector _instance;
        MusicService _service;
        List<RadioStation> _stations = new List<RadioStation>();
        bool _bootstrapped;
        MusicService _addedService;

        public static void Inject(MusicService service)
        {
            FileLog.Write("Inject called");
            if (_instance == null)
            {
                var go = new GameObject("[RadioStream]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<RadioInjector>();
                FileLog.Write("RadioInjector component created");
            }

            lock (_instance)
            {
                if (_instance._bootstrapped)
                {
                    if (!ReferenceEquals(_instance._addedService, service))
                    {
                        FileLog.Write("re-adding clips to new MusicService instance");
                        _instance._service = service;
                        _instance.AddAllItems();
                    }
                    else {FileLog.Write("Inject skipped (already running)");}
                    return;
                }
                _instance._service = service;
                _instance.StartCoroutine(_instance.Bootstrap());
            }
        }

        IEnumerator Bootstrap()
        {
            FileLog.Write("Bootstrap START");
            Debug.Log("[RadioStream] Registering radio stations");
            _stations = RadioStationConfig.Load();

            foreach (RadioStation st in _stations)
            {
                st.Streamer = new RadioStreamer(st.Url);
                st.Streamer.SetFormat(44100, 2);
                CreateClip(st);
                RegisterItem(st);
            }

            _bootstrapped = true;
            StartCoroutine(DebugStream());
            StartCoroutine(Reap());
            Debug.Log("[RadioStream] " + _stations.Count + " stations registered");
            FileLog.Write("Bootstrap DONE, " + _stations.Count + " stations registered (streams start lazily)");
            yield break;
        }

        void CreateClip(RadioStation st)
        {
            int sr = 44100;
            int ch = 2;
            int lengthSamples = sr * 60 * 60;
            RadioStation captured = st;
            st.Clip = AudioClip.Create(st.Title, lengthSamples, ch, sr, true,data => OnAudioRead(captured, data),OnAudioPosition);
            st.Clip.name = st.Title;
            FileLog.Write("clip created: " + st.Title + " " + sr + "Hz " + ch + "ch len=" + lengthSamples);
        }

        void OnAudioRead(RadioStation st, float[] data)
        {
            st.Reads++;
            if (st.Streamer == null)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }
            int read = st.Streamer.ReadPcm(data, 0, data.Length);
            if (read < data.Length) {Array.Clear(data, read, data.Length - read);}
        }

        void OnAudioPosition(int position) {}
        void RegisterItem(RadioStation st)
        {
            GameAudioInfo info = GameAudioInfo.CreateNormal(st.Clip, AudioTag.Local, st.Title, st.Author,st.Uuid, false, "", st.Description);
            info.IsUnlocked = true;
            bool added = _service.AddMusicItem(info);
            FileLog.Write("registered " + st.Title + " added=" + added);
        }

        void AddAllItems()
        {
            foreach (RadioStation st in _stations) {RegisterItem(st);}
            _addedService = _service;
            FileLog.Write("AddAllItems done");
        }

        IEnumerator DebugStream()
        {
            int tick = 0;
            while (_instance != null && _stations != null)
            {
                tick++;
                yield return new WaitForSeconds(1f);
                if (tick % 5 != 0) continue;

                StringBuilder sb = new StringBuilder();
                sb.Append("status");
                int run = 0;
                for (int i = 0; i < _stations.Count; i++)
                {
                    RadioStation st = _stations[i];
                    RadioStreamer s = st.Streamer;
                    if (s == null || !s.IsRunning) continue;
                    run++;
                    sb.Append(" | ").Append(st.Title).Append(" running decoded=").Append(s.DecodedSamples).Append(" played=").Append(s.PlayedSamples).Append(" buffered=").Append(s.BufferedMilliseconds).Append("ms idle=").Append(s.IsIdleParked()).Append(" err=").Append(s.LastError ?? "none");
                }
                sb.Insert(6, " running=" + run);
                FileLog.Write(sb.ToString());
            }
        }

        const int IdleTeardownMs = 20000;

        IEnumerator Reap()
        {
            while (_instance != null && _stations != null)
            {
                yield return new WaitForSeconds(5f);
                try { ReapIdle(); }
                catch (Exception ex) { FileLog.Write("reap error: " + ex.Message); }
            }
        }

        void ReapIdle()
        {
            if (_stations == null) return;

            RadioStreamer warm = null;
            int freshest = int.MinValue;
            foreach (RadioStation s in _stations)
            {
                RadioStreamer st = s.Streamer;
                if (st == null || !st.IsRunning) continue;
                int lr = st.LastReadAt;
                if (lr > freshest) { freshest = lr; warm = st; }
            }

            int now = Environment.TickCount;
            foreach (RadioStation s in _stations)
            {
                RadioStreamer st = s.Streamer;
                if (st == null || !st.IsRunning || ReferenceEquals(st, warm)) continue;
                int since = now - st.LastReadAt;
                if (since < 0) since = 0;
                if (since > IdleTeardownMs)
                {
                    st.StopAndRelease();
                    FileLog.Write("idle stream released: " + s.Title);
                }
            }
        }

        void OnDestroy()
        {
            foreach (RadioStation st in _stations){st.Streamer?.Dispose();}
            _stations.Clear();
        }
    }
}