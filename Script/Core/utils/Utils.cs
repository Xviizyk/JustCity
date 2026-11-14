using System;
using System.Runtime.CompilerServices;
using Godot;

namespace JustCity.Script.Core.Utils;

public static class Utils
{
    public static string Tr(string text)
    {
        string trText = TranslationServer.Translate(text);
        if (Core.Instance.IsDebug) ExtendedPrint($"Translated: {text} -> {trText}");
        return trText;
    }

    public static void ExtendedPrint(string text, [CallerMemberName] string method = "", [CallerLineNumber] int line = 0, Exception e = null)
    {
        int h = Core.Instance.CurrentSaveData.PlayTime.Hours;
        int m = Core.Instance.CurrentSaveData.PlayTime.Minutes;
        int s = Core.Instance.CurrentSaveData.PlayTime.Seconds;
        string error = e == null ? "info" : "error";
        string message = $"{method}:{line} | {h}:{m}:{s} | {error}: ";
        GD.Print(message + text);
    }
}