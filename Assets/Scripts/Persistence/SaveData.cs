using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CookieBreaker.Placement;

namespace CookieBreaker.Persistence
{
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 2;
        public int version;
        public int coins;
        public int damageLevel;
        public int attackSpeedLevel;
        public int productionLevel;
        // Explicit flag: JsonUtility need not preserve the distinction between null and [].
        public bool layoutInitialized;
        public PlacedTurretData[] turrets;

        public static SaveData Initial() => new SaveData { version = CurrentVersion };
        public bool IsInitial => coins == 0 && damageLevel == 0 && attackSpeedLevel == 0 && productionLevel == 0 &&
            (version == 1 || !layoutInitialized);

        public void Validate()
        {
            if (version != 1 && version != CurrentVersion) throw new UnsupportedSaveVersionException(version);
            if (coins < 0 || damageLevel < 0 || attackSpeedLevel < 0 || productionLevel < 0 ||
                damageLevel > 256 || attackSpeedLevel > 256 || productionLevel > 256)
                throw new InvalidDataException("Invalid coins or upgrade levels in save data.");
            if (version == 2 && turrets != null && turrets.Length > 256) throw new InvalidDataException("Too many turret records.");
        }

        public string Checksum()
        {
            string canonical = string.Join("|", new[] { version.ToString(CultureInfo.InvariantCulture),
                coins.ToString(CultureInfo.InvariantCulture), damageLevel.ToString(CultureInfo.InvariantCulture),
                attackSpeedLevel.ToString(CultureInfo.InvariantCulture), productionLevel.ToString(CultureInfo.InvariantCulture) });
            if (version == 2)
            {
                var text = new StringBuilder(canonical);
                text.Append(!layoutInitialized ? "|default" : "|count:" + (turrets?.Length ?? 0).ToString(CultureInfo.InvariantCulture));
                if (layoutInitialized && turrets != null) foreach (PlacedTurretData turret in turrets)
                {
                    if (turret == null) { text.Append("|null"); continue; }
                    Append(text, turret.instanceId); Append(text, turret.typeId);
                    text.Append('|').Append(turret.gridX.ToString(CultureInfo.InvariantCulture));
                    text.Append('|').Append(turret.gridY.ToString(CultureInfo.InvariantCulture));
                    text.Append('|').Append(turret.paidCost.ToString(CultureInfo.InvariantCulture));
                }
                canonical = text.ToString();
            }
            using (SHA256 hash = SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
        }
        private static void Append(StringBuilder text, string value) => text.Append('|')
            .Append(value == null ? "-1" : value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
    }

    public sealed class UnsupportedSaveVersionException : IOException
    {
        public UnsupportedSaveVersionException(int version) : base("Unsupported save version: " + version) { }
    }

    public interface ISaveCodec
    {
        string Encode(SaveData data);
        SaveData Decode(string json);
    }
}
