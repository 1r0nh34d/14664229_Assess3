using UnityEngine;

public static class TimeFormat
{
    public static string Format(float seconds)
    {
        int hundredths = Mathf.FloorToInt(seconds * 100f);
        int minutes = hundredths / 6000;
        int secs = hundredths / 100 % 60;
        return $"{minutes:00}:{secs:00}:{hundredths % 100:00}";
    }
}
