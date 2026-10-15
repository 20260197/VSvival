using System;
using System.IO;
using UnityEngine;

namespace CookieBreaker.Persistence
{
    public sealed class JsonSaveCodec : ISaveCodec
    {
        [Serializable]
        private sealed class Envelope
        {
            public SaveData data;
            public string checksum;
        }

        public string Encode(SaveData data)
        {
            data.Validate();
            return JsonUtility.ToJson(new Envelope { data = data, checksum = data.Checksum() }, true);
        }

        public SaveData Decode(string json)
        {
            Envelope envelope = JsonUtility.FromJson<Envelope>(json);
            if (envelope == null || envelope.data == null) throw new InvalidDataException("Missing save data.");
            envelope.data.Validate();
            if (string.IsNullOrEmpty(envelope.checksum) || envelope.checksum != envelope.data.Checksum())
                throw new InvalidDataException("Save checksum does not match.");
            return envelope.data;
        }
    }
}
