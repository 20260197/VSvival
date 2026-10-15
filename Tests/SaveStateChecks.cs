using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CookieBreaker.Combat;
using CookieBreaker.Persistence;
using CookieBreaker.Upgrades;

internal static class SaveStateChecks
{
    // The disk tests run outside Unity. JsonUtility import/serialization is tested locally in Play.
    private sealed class TestCodec : ISaveCodec
    {
        public sealed class Envelope { public SaveData data; public string checksum; }
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
        public string Encode(SaveData data)
        {
            data.Validate();
            return JsonSerializer.Serialize(new Envelope { data = data, checksum = data.Checksum() }, Options);
        }
        public SaveData Decode(string json)
        {
            Envelope envelope = JsonSerializer.Deserialize<Envelope>(json, Options);
            if (envelope == null || envelope.data == null) throw new InvalidDataException("Missing data");
            envelope.data.Validate();
            if (envelope.checksum != envelope.data.Checksum()) throw new InvalidDataException("Checksum mismatch");
            return envelope.data;
        }
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    private static SaveData Progress() => new SaveData
    { version = 1, coins = 123, damageLevel = 3, attackSpeedLevel = 2, productionLevel = 1 };

    private static void Main()
    {
        string directory = Path.Combine(Path.GetTempPath(), "cookie-breaker-save-checks-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var codec = new TestCodec();
            var messages = new List<string>();
            var store = new SaveFileStore(directory, codec, messages.Add);
            string file = Path.Combine(directory, SaveFileStore.FileName);
            Check(store.Load(out _) == SaveLoadStatus.Missing, "Fresh directory must use initial gameplay");
            SaveData progress = Progress();
            Check(store.Save(progress), "First save failed");
            Check(store.Load(out SaveData loaded) == SaveLoadStatus.Loaded && loaded.coins == 123 &&
                loaded.damageLevel == 3 && loaded.attackSpeedLevel == 2 && loaded.productionLevel == 1, "Round trip lost state");
            progress.coins = 456;
            Check(store.Save(progress) && store.Load(out loaded) == SaveLoadStatus.Loaded && loaded.coins == 456,
                "Atomic replacement failed");

            Directory.CreateDirectory(file + ".bak.tmp");
            SaveData unsaved = Progress(); unsaved.coins = 999;
            Check(!store.Save(unsaved), "Failed normal write must report failure");
            Check(store.Load(out loaded) == SaveLoadStatus.Loaded && loaded.coins == 456,
                "Failed normal write changed the last committed progress");
            Directory.Delete(file + ".bak.tmp");

            File.WriteAllText(file, "{ truncated");
            Check(store.Load(out loaded) == SaveLoadStatus.RecoveredBackup && loaded.coins == 456, "Corruption did not recover backup");
            Check(store.Save(progress), "Could not repair primary");
            File.WriteAllText(file, File.ReadAllText(file).Replace("456", "457"));
            Check(store.Load(out loaded) == SaveLoadStatus.RecoveredBackup && loaded.coins == 456, "Checksum damage not detected");
            File.WriteAllText(file + ".bak", "{}");
            Check(store.Load(out _) == SaveLoadStatus.Invalid, "Two broken copies must be reported");
            Check(store.Save(progress), "Recovery test could not seed files");
            File.WriteAllText(file, "{\"data\":{\"version\":999,\"coins\":777},\"checksum\":\"future\"}");
            string futureFile = File.ReadAllText(file);
            Check(store.Load(out _) == SaveLoadStatus.UnsupportedVersion, "Future primary must not silently load old backup");
            Check(File.ReadAllText(file) == futureFile, "Loading future data must never rewrite it");

            Check(store.Reset(), "Explicit reset must replace unsupported data");
            Check(store.Load(out loaded) == SaveLoadStatus.Loaded && loaded.IsInitial, "Reset did not persist zero state");
            File.WriteAllText(file, "broken");
            Check(store.Load(out loaded) == SaveLoadStatus.RecoveredBackup && loaded.IsInitial, "Reset left an old-progress backup");
            Check(store.Save(progress), "Could not reseed progress");

            Directory.CreateDirectory(file + ".reset.tmp");
            Check(!store.Reset(), "A reset with an unwritable marker must fail");
            Check(store.Load(out loaded) == SaveLoadStatus.Loaded && loaded.coins == 456, "Failed reset changed progress");
            Directory.Delete(file + ".reset.tmp");

            // Force failure after the reset marker and backup are durable, before primary replacement.
            Directory.CreateDirectory(file + ".tmp");
            Check(store.Reset(), "A durable reset marker must commit reset even if primary repair fails");
            Check(File.Exists(file + ".reset"), "Interrupted reset lost its durable marker");
            Check(store.Load(out loaded) == SaveLoadStatus.ResetPending && loaded.IsInitial, "Interrupted reset resurrected old primary");
            Directory.Delete(file + ".tmp");
            SaveData afterReset = SaveData.Initial(); afterReset.coins = 2;
            Check(store.Save(afterReset) && !File.Exists(file + ".reset"), "Next save failed to finish reset and resume progress");
            Check(store.Load(out loaded) == SaveLoadStatus.Loaded && loaded.coins == 2, "New progress after reset was lost");

            File.WriteAllText(file + ".reset", "invalid reset marker");
            Check(store.Load(out _) == SaveLoadStatus.Invalid, "Corrupt reset marker must never load an old primary");
            Check(store.Reset(), "Explicit reset should recover a damaged marker");
            File.WriteAllText(file, new string('x', 65537));
            Check(store.Load(out loaded) == SaveLoadStatus.RecoveredBackup && loaded.IsInitial, "Oversized save was not rejected");
            Check(messages.Count > 0, "Storage errors/recovery must be logged");

            var wallet = new CoinBalance(900);
            var upgrades = new UpgradeState(wallet, 1, 0.4, 0.9);
            for (int repeat = 0; repeat < 5; repeat++)
            {
                upgrades.RestoreLevels(3, 2, 1);
                wallet.Restore(123);
                Check(upgrades.Damage == 4 && Math.Abs(upgrades.FireInterval - 0.4 / 1.21) < 1e-9 &&
                    upgrades.Offer(UpgradeKind.Damage).Cost == 16 && wallet.Value == 123,
                    "Restore stacked effects, changed costs, or lost shared wallet identity");
            }
            Check(upgrades.TryPurchase(UpgradeKind.Damage) && wallet.Value == 107 && upgrades.Damage == 5,
                "Purchase after restore did not spend from the same restored wallet");
            try { upgrades.RestoreLevels(1, 16, 1); throw new Exception("Impossible speed level was accepted"); }
            catch (ArgumentOutOfRangeException) { }
            Check(upgrades.Damage == 5 && upgrades.Offer(UpgradeKind.AttackSpeed).Level == 2, "Rejected restore partially changed levels");
            try { upgrades.RestoreLevels(int.MaxValue, 0, 0); throw new Exception("Impossible damage level accepted"); }
            catch (ArgumentOutOfRangeException) { }
            try { upgrades.RestoreLevels(0, 0, 12); throw new Exception("Impossible production level accepted"); }
            catch (ArgumentOutOfRangeException) { }
            try { wallet.Restore(-1); throw new Exception("Negative saved wallet accepted"); }
            catch (ArgumentOutOfRangeException) { }
            Check(wallet.Value == 107, "Rejected wallet restore changed coins");
            upgrades.RestoreLevels(0, 0, 0); wallet.Restore(0);
            Check(upgrades.Damage == 1 && upgrades.FireInterval == 0.4 && upgrades.SpawnInterval == 0.9 && wallet.Value == 0,
                "Reset failed to restore baseline effects");
            Check(codec.Encode(codec.Decode(codec.Encode(Progress()))) == codec.Encode(Progress()), "JSON/checksum round trip changed data");
            Console.WriteLine("PASS: M3 save/replace, corrupt/checksum/oversize recovery, future versions, reset failures/interruption, backup reset, repeated restore and post-load purchases.");
        }
        finally { Directory.Delete(directory, true); }
    }
}
