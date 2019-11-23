namespace StixGames.NatureCore.Utility.Localization
{
    [System.Serializable]
    public class LocalizationData
    {
        public LocalizationItem[] Items;
    }

    [System.Serializable]
    public class LocalizationItem
    {
        public string Key;
        public string Value;

        public LocalizationItem()
        {
        }

        public LocalizationItem(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public override string ToString()
        {
            return string.Format("Key: {0}, Value: {1}", Key, Value);
        }
    }
}