using System;
using System.IO;
using System.Text;

namespace CookieBreaker.Persistence
{
    public enum SaveLoadStatus { Missing, Loaded, RecoveredBackup, ResetPending, Invalid, UnsupportedVersion }

    // Synchronous, small-file storage. Only this class touches the disk.
    public sealed class SaveFileStore
    {
        public const string FileName = "cookie-breaker-save.json";
        private readonly string primary, backup, reset;
        private readonly ISaveCodec codec;
        private readonly Action<string> report;
        private const int MaximumBytes = 65536;

        public SaveFileStore(string directory, ISaveCodec codec, Action<string> report)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("A save directory is required.", nameof(directory));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            this.report = report ?? (_ => { });
            primary = Path.Combine(directory, FileName);
            backup = primary + ".bak";
            reset = primary + ".reset";
        }

        public SaveLoadStatus Load(out SaveData data)
        {
            data = null;
            // A durable reset always takes priority over any old primary or backup.
            if (File.Exists(reset))
            {
                SaveLoadStatus status = Read(reset, out data);
                if (status != SaveLoadStatus.Loaded) return status;
                if (!data.IsInitial) { data = null; report("Reset marker is invalid; old progress will not be loaded."); return SaveLoadStatus.Invalid; }
                return SaveLoadStatus.ResetPending;
            }
            SaveLoadStatus first = Read(primary, out data);
            if (first == SaveLoadStatus.Loaded || first == SaveLoadStatus.UnsupportedVersion) return first;
            SaveLoadStatus second = Read(backup, out data);
            if (second == SaveLoadStatus.Loaded)
            {
                report("Recovered progress from the backup save.");
                return SaveLoadStatus.RecoveredBackup;
            }
            if (second == SaveLoadStatus.UnsupportedVersion) return second;
            return first == SaveLoadStatus.Missing && second == SaveLoadStatus.Missing ? SaveLoadStatus.Missing : SaveLoadStatus.Invalid;
        }

        public bool Save(SaveData data)
        {
            try
            {
                data.Validate();
                if (File.Exists(reset))
                {
                    SaveData marker = codec.Decode(ReadText(reset));
                    marker.Validate();
                    if (!marker.IsInitial) throw new InvalidDataException("Invalid reset marker.");
                    WriteCopies(marker);
                    File.Delete(reset);
                }
                WriteCopies(data);
                return true;
            }
            catch (Exception error) { report("Save failed: " + error); return false; }
        }

        public bool Reset()
        {
            SaveData initial = SaveData.Initial();
            try { WriteAtomic(reset, codec.Encode(initial)); }
            catch (Exception error) { report("Reset could not be committed; current progress is unchanged: " + error); return false; }
            // Once the marker is durable, reset has committed even if repairing copies fails.
            // The next launch reads the marker first and can never resurrect the old backup.
            try { WriteCopies(initial); File.Delete(reset); }
            catch (Exception error) { report("Reset committed; save copies will be repaired on the next save: " + error); }
            return true;
        }

        private SaveLoadStatus Read(string path, out SaveData data)
        {
            data = null;
            if (!File.Exists(path)) return SaveLoadStatus.Missing;
            try
            {
                data = codec.Decode(ReadText(path));
                if (data == null) throw new InvalidDataException("Missing save object.");
                data.Validate();
                return SaveLoadStatus.Loaded;
            }
            catch (UnsupportedSaveVersionException error)
            { report(path + ": " + error.Message); data = null; return SaveLoadStatus.UnsupportedVersion; }
            catch (Exception error)
            { report("Cannot read " + path + ": " + error); data = null; return SaveLoadStatus.Invalid; }
        }

        private static string ReadText(string path)
        {
            if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("Save file is too large.");
            return File.ReadAllText(path, Encoding.UTF8);
        }

        private void WriteCopies(SaveData data)
        {
            string json = codec.Encode(data);
            WriteAtomic(backup, json);
            WriteAtomic(primary, json);
        }

        private static void WriteAtomic(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            byte[] bytes = new UTF8Encoding(false).GetBytes(json);
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            // Same-directory rename/replacement: never delete a valid destination first.
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }
}
