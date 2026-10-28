using UnityEngine;

public static class TimeFormat
{
    // mm:ss:cc where cc is hundredths of a second, e.g. 01:32:25 is 1 minute, 32 seconds and 250 milliseconds
    public static string Format(float seconds)
    {
        int hundredths = Mathf.FloorToInt(seconds * 100f);
        int minutes = hundredths / 6000;
        int secs = hundredths / 100 % 60;
        return $"{minutes:00}:{secs:00}:{hundredths % 100:00}";
    }
}
