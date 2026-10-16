using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CookieBreaker.Persistence
{
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;
        public int version;
        public int coins;
        public int damageLevel;
        public int attackSpeedLevel;
        public int productionLevel;

        public static SaveData Initial() => new SaveData { version = CurrentVersion };
        public bool IsInitial => coins == 0 && damageLevel == 0 && attackSpeedLevel == 0 && productionLevel == 0;

        public void Validate()
        {
            if (version != CurrentVersion) throw new UnsupportedSaveVersionException(version);
            if (coins < 0 || damageLevel < 0 || attackSpeedLevel < 0 || productionLevel < 0 ||
                damageLevel > 256 || attackSpeedLevel > 256 || productionLevel > 256)
                throw new InvalidDataException("Invalid coins or upgrade levels in save data.");
        }

        public string Checksum()
        {
            string canonical = string.Join("|", new[] { version.ToString(CultureInfo.InvariantCulture),
                coins.ToString(CultureInfo.InvariantCulture), damageLevel.ToString(CultureInfo.InvariantCulture),
                attackSpeedLevel.ToString(CultureInfo.InvariantCulture), productionLevel.ToString(CultureInfo.InvariantCulture) });
            using (SHA256 hash = SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(canonical)));
        }
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
